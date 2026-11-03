using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Pastille;

internal sealed class OverlayWindow : Window
{
    private readonly Settings settings;
    private readonly Grid surface = new();
    private readonly Canvas viewport = new() { ClipToBounds = true };
    private readonly Image image = new() { Stretch = Stretch.Fill };
    private readonly TextBlock placeholder = new()
    {
        Text = "PASTILLE\nWebcam arrêtée", TextAlignment = TextAlignment.Center,
        Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 17
    };
    private readonly Grid content = new() { Background = new SolidColorBrush(Color.FromRgb(28, 38, 48)) };
    private readonly Ellipse oval = new() { StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Path rectangle = new() { StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Thumb grip = new()
    {
        Width = 32, Height = 14, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5),
        Cursor = Cursors.SizeNWSE, Opacity = 0, ToolTip = "Glisser pour redimensionner"
    };
    public event Action? Changed;
    public event Action? ShowControls;
    public event Action? HideRequested;

    public OverlayWindow(Settings settings)
    {
        this.settings = settings;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Title = "Pastille — webcam";
        MinWidth = 120; MinHeight = 90;
        MaxWidth = 960; MaxHeight = 720;
        Left = settings.Left; Top = settings.Top;
        Width = settings.Width; Height = settings.Height;
        viewport.Children.Add(image);
        content.Children.Add(viewport);
        content.Children.Add(placeholder);
        surface.Children.Add(content);
        surface.Children.Add(oval);
        surface.Children.Add(rectangle);
        surface.Children.Add(grip);
        Content = surface;
        var menu = new ContextMenu();
        AddMenu(menu, "Afficher les commandes", () => ShowControls?.Invoke());
        AddMenu(menu, "Cacher la pastille (Ctrl + Maj + F9)", () => HideRequested?.Invoke());
        AddMenu(menu, "Ovale / rectangle", () =>
        {
            settings.Oval = !settings.Oval;
            ApplyAppearance();
            Changed?.Invoke();
        });
        AddMenu(menu, "Réinitialiser le cadrage", ResetFraming);
        ContextMenu = menu;
        content.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { ShowControls?.Invoke(); return; }
            DragMove();
            StoreBounds();
        };
        grip.DragDelta += (_, e) =>
        {
            Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, MaxWidth);
            Height = Math.Clamp(Height + e.VerticalChange, MinHeight, MaxHeight);
        };
        grip.DragCompleted += (_, _) => StoreBounds();
        MouseEnter += (_, _) => grip.Opacity = 0.9;
        MouseLeave += (_, _) => grip.Opacity = 0;
        MouseWheel += (_, e) =>
        {
            settings.Zoom = Math.Clamp(settings.Zoom * Math.Pow(1.1, e.Delta / 120d), 0.5, 4);
            ApplyFraming();
            Changed?.Invoke();
            e.Handled = true;
        };
        SizeChanged += (_, _) => ApplyAppearance();
        Loaded += (_, _) => KeepOnScreen();
        ApplyAppearance();
    }

    private static void AddMenu(ContextMenu menu, string title, Action action)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    public void ShowFrame(BitmapSource frame)
    {
        var changed = image.Source != frame;
        image.Source = frame;
        placeholder.Visibility = Visibility.Collapsed;
        if (changed) ApplyFraming();
    }

    public void ClearFrame()
    {
        image.Source = null;
        placeholder.Visibility = Visibility.Visible;
    }

    public void ApplyAppearance()
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var radius = Math.Min(settings.CornerRadius, Math.Min(width, height) / 2);
        content.Clip = settings.Oval
            ? new EllipseGeometry(new Point(width / 2, height / 2), width / 2, height / 2)
            : new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        var inset = settings.OutlineThickness / 2;
        var strokeRadius = Math.Max(0, radius - inset);
        rectangle.Data = new RectangleGeometry(
            new Rect(inset, inset, Math.Max(0, width - inset * 2), Math.Max(0, height - inset * 2)),
            strokeRadius, strokeRadius);
        ApplyFraming();
        oval.Visibility = settings.OutlineEnabled && settings.Oval ? Visibility.Visible : Visibility.Collapsed;
        rectangle.Visibility = settings.OutlineEnabled && !settings.Oval ? Visibility.Visible : Visibility.Collapsed;
        if (!Settings.TryParseColor(settings.OutlineColor, out var color))
            throw new InvalidOperationException("Couleur du liseré invalide.");
        oval.Stroke = rectangle.Stroke = new SolidColorBrush(color);
        oval.StrokeThickness = rectangle.StrokeThickness = settings.OutlineThickness;
    }

    public void ApplyFraming()
    {
        if (image.Source is not BitmapSource frame) return;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        // Explicit canvas bounds prevent a wide camera image from enlarging its Grid layout slot.
        var scale = Math.Max(width / frame.PixelWidth, height / frame.PixelHeight) * settings.Zoom;
        image.Width = frame.PixelWidth * scale;
        image.Height = frame.PixelHeight * scale;
        Canvas.SetLeft(image, (width - image.Width) / 2 + settings.PanX * width / 2);
        Canvas.SetTop(image, (height - image.Height) / 2 + settings.PanY * height / 2);
        image.RenderTransformOrigin = new Point(0.5, 0.5);
        image.RenderTransform = new ScaleTransform(settings.Mirror ? -1 : 1, 1);
    }

    public void ResetFraming()
    {
        settings.Zoom = 1;
        settings.PanX = settings.PanY = 0;
        ApplyFraming();
        Changed?.Invoke();
    }

    public void KeepOnScreen()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null) return;
        var transform = source.CompositionTarget.TransformToDevice;
        var x = Left * transform.M11;
        var y = Top * transform.M22;
        var intersects = Native.Displays().Any(d =>
            x + Width * transform.M11 > d.Bounds.Left + 40 &&
            x < d.Bounds.Right - 40 && y + Height * transform.M22 > d.Bounds.Top + 40 &&
            y < d.Bounds.Bottom - 40);
        if (!intersects)
        {
            Left = SystemParameters.WorkArea.Left + 32;
            Top = SystemParameters.WorkArea.Top + 32;
            StoreBounds();
        }
    }

    public void StoreBounds()
    {
        settings.Left = Left; settings.Top = Top;
        settings.Width = Width; settings.Height = Height;
        Changed?.Invoke();
    }
}
