using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Devices.Enumeration;

namespace Pastille;

public partial class MainWindow : Window
{
    private readonly Settings settings;
    private readonly bool persistSettings;
    private readonly OverlayWindow overlay;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Camera? camera;
    private bool ready, cameraBusy, closing, canClose, hotkeyRegistered, exitRequested, started;
    private HwndSource? hwndSource;
    private nint windowHandle;
    private TrayIcon? tray;
    internal TrayIcon Tray => tray ?? throw new InvalidOperationException("L'icône Pastille n'est pas initialisée.");

    public MainWindow() : this(LoadSettings(), true) { }

    internal MainWindow(Settings settings, bool persistSettings)
    {
        InitializeComponent();
        this.settings = settings;
        this.persistSettings = persistSettings;
        overlay = new OverlayWindow(settings);
        overlay.Changed += SyncOverlay;
        overlay.ShowControls += ShowControls;
        overlay.HideRequested += () => SetOverlayVisibility(false);
        WidthSlider.Value = settings.Width;
        HeightSlider.Value = settings.Height;
        OvalChoice.IsChecked = settings.Oval;
        RectangleChoice.IsChecked = !settings.Oval;
        MirrorChoice.IsChecked = settings.Mirror;
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            try { settings.Save(); } catch (Exception ex) { App.Report(ex); }
        };
        SourceInitialized += (_, _) =>
        {
            windowHandle = new WindowInteropHelper(this).Handle;
            hwndSource = HwndSource.FromHwnd(windowHandle);
            hwndSource?.AddHook(WindowMessage);
            hotkeyRegistered = Native.RegisterHotKey(windowHandle, 1, 0x4006, 0x78);
            if (!hotkeyRegistered)
            {
                var error = Marshal.GetLastWin32Error();
                HotkeyLabel.Text = "Raccourci Ctrl + Maj + F9 indisponible. Fermez l'autre instance de Pastille, l'ancienne application Visage ou l'application qui l'utilise, puis relancez Pastille. Le bouton d'affichage reste disponible.";
                HotkeyLabel.Foreground = Brushes.Firebrick;
                App.Log(new Win32Exception(error, "Impossible d'enregistrer le raccourci global Ctrl + Maj + F9."));
            }
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !closing) Hide();
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            saveTimer.Stop();
            tray?.Dispose();
            tray = null;
            if (hotkeyRegistered) Native.UnregisterHotKey(windowHandle, 1);
            hwndSource?.RemoveHook(WindowMessage);
            overlay.Close();
        };
    }

    internal async Task StartInTrayAsync()
    {
        if (started) return;
        started = true;
        new WindowInteropHelper(this).EnsureHandle();
        tray = new TrayIcon(hwndSource ?? throw new InvalidOperationException("Fenêtre Windows indisponible."),
            ShowControls, () => SetOverlayVisibility(!overlay.IsVisible), RequestExit, () => overlay.IsVisible,
            ex => { App.Report(ex); ShowControls(); });
        overlay.Show();
        ready = true;
        SyncOverlay();
        try { await RefreshDevicesAsync(); }
        catch (Exception ex) { App.Report(ex); }
    }

    private static Settings LoadSettings()
    {
        try { return Settings.Load(); }
        catch (Exception ex)
        {
            App.Report(new InvalidOperationException("Les préférences n'ont pas pu être lues. Les valeurs par défaut seront utilisées.", ex));
            return new Settings();
        }
    }

    private nint WindowMessage(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x0312 && wparam == 1 && !closing)
        {
            handled = true;
            SetOverlayVisibility(!overlay.IsVisible);
        }
        return 0;
    }

    private void ShowControls()
    {
        if (closing || exitRequested) return;
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void HideConfigurationClick(object sender, RoutedEventArgs e) => Close();

    private async Task RefreshDevicesAsync()
    {
        var previousCamera = (CameraChoice.SelectedItem as DeviceInformation)?.Id ?? settings.CameraId;
        var devices = await Camera.DevicesAsync();
        CameraChoice.ItemsSource = devices;
        CameraChoice.SelectedItem = devices.FirstOrDefault(x => x.Id == previousCamera) ?? devices.FirstOrDefault();
        if (devices.Count == 0) StatusLabel.Text = "Aucune webcam détectée. Branchez-la puis actualisez.";
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        try { await RefreshDevicesAsync(); }
        catch (Exception ex) { App.Report(ex); }
        finally { RefreshButton.IsEnabled = true; }
    }

    private async void CameraClick(object sender, RoutedEventArgs e)
    {
        if (cameraBusy || closing) return;
        cameraBusy = true;
        CameraButton.IsEnabled = CameraChoice.IsEnabled = RefreshButton.IsEnabled = false;
        try
        {
            if (camera is not null)
            {
                var previous = camera;
                camera = null;
                await previous.DisposeAsync();
                overlay.ClearFrame();
                CameraButton.Content = "Activer la webcam";
                StatusLabel.Text = "Webcam arrêtée.";
            }
            else
            {
                if (CameraChoice.SelectedItem is not DeviceInformation device)
                    throw new InvalidOperationException("Sélectionnez une webcam. Vérifiez aussi les autorisations dans Paramètres > Confidentialité et sécurité > Caméra.");
                var next = new Camera(Dispatcher);
                next.Frame += overlay.ShowFrame;
                next.Failed += CameraFailed;
                await next.StartAsync(device.Id);
                camera = next;
                settings.CameraId = device.Id;
                QueueSave();
                CameraButton.Content = "Arrêter la webcam";
                SetOverlayVisibility(true);
                StatusLabel.Text = "Webcam active.";
            }
        }
        catch (Exception ex)
        {
            App.Report(new InvalidOperationException("Webcam : " + ex.Message +
                "\nAutorisez les applications de bureau à accéder à la caméra dans les paramètres Windows.", ex));
        }
        finally
        {
            cameraBusy = false;
            CameraButton.IsEnabled = RefreshButton.IsEnabled = true;
            CameraChoice.IsEnabled = camera is null;
            if (exitRequested) RequestExit();
        }
    }

    private async void CameraFailed(string message)
    {
        if (closing || camera is null) return;
        var failed = camera;
        camera = null;
        try { await failed.DisposeAsync(); }
        catch (Exception ex) { App.Log(ex); }
        overlay.ClearFrame();
        CameraButton.Content = "Activer la webcam";
        CameraChoice.IsEnabled = true;
        App.Report(new InvalidOperationException("La webcam a été interrompue : " + message));
    }

    private void ShapeChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.Oval = OvalChoice.IsChecked == true;
        overlay.ApplyAppearance();
        SyncOverlay();
    }

    private void MirrorChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.Mirror = MirrorChoice.IsChecked == true;
        overlay.ApplyAppearance();
        QueueSave();
    }

    private void OutlineChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.OutlineEnabled = OutlineChoice.IsChecked == true;
        overlay.ApplyAppearance();
        SyncOverlay();
    }

    private void OutlineThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.OutlineThickness = OutlineThicknessSlider.Value;
        overlay.ApplyAppearance();
        SyncOverlay();
    }

    private void OutlineColorClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Settings.TryParseColor(settings.OutlineColor, out var initial))
                throw new InvalidOperationException("Couleur du liseré invalide.");
            var color = Native.PickColor(this, initial);
            if (color is null) return;
            settings.OutlineColor = $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
            overlay.ApplyAppearance();
            SyncOverlay();
        }
        catch (Exception ex) { App.Report(ex); }
    }

    private void CornerRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.CornerRadius = CornerRadiusSlider.Value;
        overlay.ApplyAppearance();
        SyncOverlay();
    }

    private void SizeChangedByUser(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        overlay.Width = WidthSlider.Value;
        overlay.Height = HeightSlider.Value;
        overlay.StoreBounds();
    }

    private void FramingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.Zoom = ZoomSlider.Value;
        settings.PanX = PanXSlider.Value;
        settings.PanY = PanYSlider.Value;
        overlay.ApplyFraming();
        SyncOverlay();
    }

    private void ResetFramingClick(object sender, RoutedEventArgs e) => overlay.ResetFraming();

    private void SyncOverlay()
    {
        if (!ready) return;
        ready = false;
        WidthSlider.Value = overlay.Width;
        HeightSlider.Value = overlay.Height;
        WidthLabel.Text = $"{overlay.Width:0}";
        HeightLabel.Text = $"{overlay.Height:0}";
        OvalChoice.IsChecked = settings.Oval;
        RectangleChoice.IsChecked = !settings.Oval;
        ZoomSlider.Value = settings.Zoom;
        PanXSlider.Value = settings.PanX;
        PanYSlider.Value = settings.PanY;
        ZoomLabel.Text = $"{settings.Zoom:P0}";
        PanXLabel.Text = settings.PanX == 0 ? "Centre" : $"{settings.PanX:+0%;-0%}";
        PanYLabel.Text = settings.PanY == 0 ? "Centre" : $"{settings.PanY:+0%;-0%}";
        OutlineChoice.IsChecked = settings.OutlineEnabled;
        OutlineThicknessSlider.Value = settings.OutlineThickness;
        OutlineThicknessLabel.Text = $"{settings.OutlineThickness:0}";
        OutlineThicknessSlider.IsEnabled = OutlineColorButton.IsEnabled = settings.OutlineEnabled;
        OutlineColorLabel.Text = settings.OutlineColor;
        CornerRadiusSlider.Value = settings.CornerRadius;
        CornerRadiusSlider.IsEnabled = !settings.Oval;
        CornerRadiusLabel.Text = $"{settings.CornerRadius:0}";
        if (Settings.TryParseColor(settings.OutlineColor, out var color))
            OutlineColorSwatch.Background = new SolidColorBrush(color);
        ready = true;
        QueueSave();
    }

    private void QueueSave()
    {
        if (!persistSettings) return;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void SetOverlayVisibility(bool visible)
    {
        if (visible) { overlay.Show(); overlay.KeepOnScreen(); }
        else { overlay.ContextMenu.IsOpen = false; overlay.Hide(); }
        VisibleChoice.IsChecked = visible;
        ToggleVisibilityButton.Content = visible ? "Cacher la pastille" : "Afficher la pastille";
        tray?.RefreshVisibility();
    }

    private void VisibilityChanged(object sender, RoutedEventArgs e)
    {
        if (ready) SetOverlayVisibility(VisibleChoice.IsChecked == true);
    }

    private void ToggleVisibilityClick(object sender, RoutedEventArgs e) => SetOverlayVisibility(!overlay.IsVisible);

    private void CenterOverlayClick(object sender, RoutedEventArgs e)
    {
        overlay.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - overlay.Width) / 2;
        overlay.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - overlay.Height) / 2;
        overlay.StoreBounds();
        SetOverlayVisibility(true);
    }

    internal void RequestExit()
    {
        if (closing) return;
        exitRequested = true;
        IsEnabled = false;
        if (!cameraBusy) Close();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (canClose) return;
        e.Cancel = true;
        if (closing) return;
        if (!exitRequested)
        {
            Hide();
            return;
        }
        if (cameraBusy)
        {
            return;
        }
        closing = true;
        IsEnabled = false;
        try
        {
            if (camera is not null) await camera.DisposeAsync();
            overlay.StoreBounds();
            saveTimer.Stop();
            if (persistSettings) settings.Save();
        }
        catch (Exception ex) { App.Report(ex); }
        finally
        {
            canClose = true;
            _ = Dispatcher.BeginInvoke(Close);
        }
    }
}
