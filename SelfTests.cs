using System.IO;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pastille;

internal static class SelfTests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint wparam, nint lparam);
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, nint process);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, System.Text.StringBuilder name, int maximum);
    [StructLayout(LayoutKind.Sequential)]
    private struct IconIdentifier
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public Guid Guid;
    }
    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref IconIdentifier icon, out Native.Rect rect);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string name);

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("TEST: " + message);
    }

    internal static async Task RunAsync()
    {
        var results = new List<string>();
        var target = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RuntimeIdentifier").Value;
        var expectedArchitecture = target switch
        {
            "win-arm64" => Architecture.Arm64,
            "win-x64" => Architecture.X64,
            _ => throw new InvalidOperationException($"Unsupported test target: {target}")
        };
        Assert(RuntimeInformation.ProcessArchitecture == expectedArchitecture, $"{target} process");
        results.Add($"PASS {target} process architecture ({expectedArchitecture})");
        Assert(typeof(App).Assembly.GetName().Name == "Pastille", "application assembly renamed");
        results.Add("PASS Pastille application identity");
        CheckTrayArtwork();
        results.Add("PASS Unbranded vertical octagonal lozenge icon at 16/20/24/32/48/64 pixels; transparent corners, white face and contrast outline");
        CheckLegacyMigration();
        results.Add("PASS Legacy Visage preferences migrated once; Pastille preferences take priority; legacy file unchanged");
        if (Environment.GetEnvironmentVariable("PASTILLE_REQUIRE_BUNDLED_RUNTIME") == "1")
        {
            var extraction = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")
                ?? throw new InvalidOperationException("Bundle extraction test directory missing.");
            #pragma warning disable IL3000 // An empty location is exactly what this bundle verification asserts.
            Assert(typeof(object).Assembly.Location.Length == 0, "CoreLib must load from the executable bundle");
            #pragma warning restore IL3000
            Assert(GetModuleHandle("coreclr.dll") == 0, "single-file host must use its integrated CoreCLR, not installed .NET");
            Assert(Directory.GetFiles(extraction, "PresentationNative_cor3.dll", SearchOption.AllDirectories).Length >= 1,
                "native WPF dependency extracted from bundle");
            results.Add("PASS Self-contained integrated CoreCLR, bundled CoreLib and extracted native WPF dependency");
        }
        var settings = new Settings
        {
            Width = -10, Height = double.NaN, Left = double.PositiveInfinity,
            Zoom = double.NaN, PanX = double.NegativeInfinity, PanY = 3, OutlineThickness = double.NaN,
            CornerRadius = double.NaN
        };
        settings.Normalize();
        Assert(settings.Width == 120 && settings.Height == 210 && settings.Left == 60 &&
            settings.Zoom == 1 && settings.PanX == 0 && settings.PanY == 1 && settings.OutlineThickness == 2 &&
            settings.CornerRadius == 12,
            "preference bounds and finite defaults");
        var legacy = JsonSerializer.Deserialize<Settings>(
            """{"Width":320,"Mirror":false,"Zoom":2.5,"PanX":0.4,"OutputDirectory":"C:\\OldVideos"}""")!;
        legacy.Normalize();
        Assert(legacy.Zoom == 2.5 && legacy.PanX == 0.4 && legacy.OutlineEnabled &&
            legacy.OutlineColor == "#40E0D0" && legacy.OutlineThickness == 2 && legacy.CornerRadius == 12,
            "legacy preference compatibility");
        legacy.OutlineEnabled = false; legacy.OutlineColor = "#1234EF"; legacy.OutlineThickness = 13;
        legacy.CornerRadius = 57;
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(legacy))!;
        Assert(!restored.OutlineEnabled && restored.OutlineColor == "#1234EF" && restored.OutlineThickness == 13 &&
            restored.CornerRadius == 57,
            "outline preference roundtrip");
        restored.CornerRadius = 500; restored.Normalize();
        Assert(restored.CornerRadius == 200, "corner radius upper bound");
        restored.CornerRadius = -5; restored.Normalize();
        Assert(restored.CornerRadius == 0, "corner radius lower bound");
        Assert(!JsonSerializer.Serialize(restored).Contains("OutputDirectory"), "removed recording preference");
        Assert(Settings.TryParseColor("#12aBef", out var color) && color == Color.FromRgb(18, 171, 239),
            "RGB color parsing");
        Assert(!Settings.TryParseColor("#GG0000", out _) && !Settings.TryParseColor("#123", out _) &&
            !Settings.TryParseColor(null, out _), "invalid colors rejected");
        results.Add("PASS Preference migration, validation and outline persistence");
        Assert(Native.Displays().Count > 0, "display enumeration");
        await Camera.DevicesAsync();
        results.Add("PASS Display and camera enumeration (webcam not activated)");
        await CheckCameraFramingAsync();
        results.Add("PASS Wide live-camera centering, 50%-400% zoom, two-axis pan, mirror, both shapes and resizing");
        await CheckOutlineAsync();
        results.Add("PASS Outline on/off, RGB colors and 1/2/6/20-unit thickness rendered for oval and rectangle");
        await CheckCornerRadiusAsync();
        results.Add("PASS Rectangle radius 0/12/40/200, clipped image and matching outline, resize clamp and oval independence");
        await CheckLocalizationAsync(results);
        Assert(typeof(App).Assembly.GetType("Pastille.ScreenRecorder") is null &&
            typeof(App).Assembly.GetType("Pastille.Microphone") is null &&
            typeof(App).Assembly.GetType("Pastille.DesktopCapture") is null, "recording implementation removed");
        results.Add("PASS Screen recorder, desktop capture and microphone removed from application");
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), results);
    }

    private static async Task CheckLocalizationAsync(List<string> results)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        Assert(System.Windows.Markup.XmlLanguage.GetLanguage(originalUiCulture.IetfLanguageTag).Equals(
            FrameworkElement.LanguageProperty.GetMetadata(typeof(FrameworkElement)).DefaultValue),
            "WPF language initialized from the selected Windows UI culture before windows are created");
        try
        {
            var neutral = Localization.Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)
                ?? throw new InvalidOperationException("Neutral English resources missing.");
            var keys = neutral.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToHashSet();
            foreach (var language in Localization.SupportedLanguages)
            {
                var culture = CultureInfo.GetCultureInfo(language);
                var manager = new ResourceManager("Pastille.Strings", typeof(App).Assembly);
                var resources = manager.GetResourceSet(culture, true, false)
                    ?? throw new InvalidOperationException($"{language}: resource set missing without parent fallback");
                Assert(keys.SetEquals(resources.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)),
                    $"{language}: exactly the same resource keys as English");
                foreach (var key in keys)
                {
                    var value = resources.GetString(key)
                        ?? throw new InvalidOperationException($"{language}: resource {key} missing");
                    Assert(!string.IsNullOrWhiteSpace(value), $"{language}: nonempty resource {key}");
                    var expectedFormat = CompositeFormat.Parse(neutral.GetString(key)!);
                    var actualFormat = CompositeFormat.Parse(value);
                    Assert(actualFormat.MinimumArgumentCount == expectedFormat.MinimumArgumentCount,
                        $"{language}: format arguments preserved for {key}");
                    var arguments = Enumerable.Range(0, actualFormat.MinimumArgumentCount)
                        .Select(index => (object)$"argument-{index}").ToArray();
                    var formatted = string.Format(culture, value, arguments);
                    Assert(arguments.All(argument => formatted.Contains((string)argument, StringComparison.Ordinal)),
                        $"{language}: all diagnostic arguments present for {key}");
                }
                Assert(resources.GetString("TrayTip")!.Length < 128, $"{language}: native tray tooltip limit");
                results.Add($"PASS {language}: {keys.Count} complete resources, format arguments and tray tooltip length");
            }
            foreach (var name in new[] { "en-US", "en-GB", "fr-FR", "fr-CA", "es-ES", "es-MX",
                "de-DE", "de-AT", "it-IT", "it-CH" })
            {
                var culture = CultureInfo.GetCultureInfo(name);
                Assert(Localization.SelectCulture(culture).Name == name, $"{name}: regional culture retained");
            }
            foreach (var name in new[] { "ja-JP", "pt-BR", "ar-SA", "" })
                Assert(Localization.SelectCulture(CultureInfo.GetCultureInfo(name)).Name == "en",
                    $"{name}: unsupported or invariant UI culture falls back to English");
            results.Add("PASS Windows UI language selection, regional variants and unsupported/invariant English fallback");

            var cases = new[]
            {
                ("en-GB", "Settings…"), ("fr-CA", "Configuration…"), ("es-MX", "Configuración…"),
                ("de-AT", "Einstellungen…"), ("it-CH", "Impostazioni…"), ("ja-JP", "Settings…")
            };
            foreach (var (name, expectedConfigure) in cases)
            {
                var culture = Localization.SelectCulture(CultureInfo.GetCultureInfo(name));
                Localization.ApplyCulture(culture);
                Assert(Localization.Text("Configure") == expectedConfigure, $"{name}: expected translated text");
                Assert(CultureInfo.CurrentCulture.Name == culture.Name &&
                    CultureInfo.CurrentUICulture.Name == culture.Name, $"{name}: formatting and UI cultures agree");
                Assert(await Task.Run(() => CultureInfo.CurrentUICulture.Name == culture.Name &&
                    CultureInfo.CurrentCulture.Name == culture.Name), $"{name}: worker culture");
                var invalid = new Settings { OutlineColor = "invalid" };
                try
                {
                    invalid.Normalize();
                    Assert(false, $"{name}: invalid color must throw");
                }
                catch (InvalidDataException ex)
                {
                    Assert(ex.Message == Localization.Text("ColorFormatError"), $"{name}: localized validation error");
                }
                Assert(Localization.Format("CameraError", "diagnostic").Contains("diagnostic", StringComparison.Ordinal) &&
                    Localization.Format("CameraStartError", "DeviceNotAvailable").Contains("DeviceNotAvailable",
                        StringComparison.Ordinal), $"{name}: localized camera errors retain technical details");
                CheckLegacyMigration();
                var json = JsonSerializer.Serialize(new Settings { Zoom = 1.25, PanX = -0.5 });
                Assert(json.Contains("\"Zoom\":1.25", StringComparison.Ordinal) &&
                    JsonSerializer.Deserialize<Settings>(json)!.PanX == -0.5,
                    $"{name}: JSON names and numbers remain culture-independent");
                await CheckVisibilityAsync(results);
                results.Add($"PASS {name} -> {culture.Name}: localized WPF controls, overlay and real tray, " +
                    "numeric formatting, errors, hide/show, hotkey, native color picker and exit");
            }
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static void CheckTrayArtwork()
    {
        foreach (var size in new[] { 16, 20, 24, 32, 48, 64 })
        {
            var bitmap = TrayIcon.RenderIcon(size);
            var pixels = new byte[size * size * 4];
            bitmap.CopyPixels(pixels, size * 4, 0);
            var minX = size; var minY = size; var maxX = -1; var maxY = -1;
            var outlinePixels = 0;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = (y * size + x) * 4;
                if (pixels[p + 3] < 128) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (pixels[p + 3] > 220 && pixels[p + 2] < 190) outlinePixels++;
            }
            Assert(maxY - minY > (maxX - minX) * 1.2, $"vertical icon aspect at {size}px");
            Assert(Math.Abs(minX + maxX - size + 1) <= 1, $"icon horizontally centered at {size}px");
            Assert(pixels[3] == 0 && pixels[(size * size - 1) * 4 + 3] == 0,
                $"transparent icon background at {size}px");
            Assert(ColorAt(pixels, size, size / 2, size / 2, 248, 248, 243),
                $"white unmarked lozenge face at {size}px");
            Assert(outlinePixels >= size / 4, $"contrast outline on light background at {size}px");
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(TrayIcon.RenderIcon(256)));
        using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "pastille-icon-preview.png"));
        encoder.Save(output);
    }

    private static void CheckLegacyMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pastille-migration-test-" + Guid.NewGuid().ToString("N"));
        var current = Path.Combine(root, "Pastille");
        var legacy = Path.Combine(root, "Visage");
        Directory.CreateDirectory(legacy);
        try
        {
            Assert(Settings.Load(current, legacy).Zoom == 1 && !Directory.Exists(current),
                "first installation without legacy preferences");
            var oldPath = Path.Combine(legacy, "settings.json");
            var oldJson = JsonSerializer.Serialize(new Settings
            {
                Zoom = 2.5, PanX = -0.3, PanY = 0.4, Width = 430, Height = 290, Left = 120, Top = 160,
                Oval = false, Mirror = false, OutlineEnabled = false, OutlineColor = "#123456",
                OutlineThickness = 7, CornerRadius = 45, CameraId = "test-camera"
            });
            File.WriteAllText(oldPath, oldJson);
            var migrated = Settings.Load(current, legacy);
            Assert(migrated.Zoom == 2.5 && migrated.PanX == -0.3 && migrated.PanY == 0.4 &&
                migrated.Width == 430 && migrated.Height == 290 && migrated.Left == 120 && migrated.Top == 160 &&
                !migrated.Oval && !migrated.Mirror && !migrated.OutlineEnabled &&
                migrated.OutlineColor == "#123456" && migrated.OutlineThickness == 7 &&
                migrated.CornerRadius == 45 && migrated.CameraId == "test-camera",
                "all legacy preferences preserved");
            Assert(File.Exists(Path.Combine(current, "settings.json")) && File.ReadAllText(oldPath) == oldJson,
                "migration persists new preferences without modifying legacy file");
            migrated.Zoom = 1.2;
            migrated.Save(current);
            Assert(Settings.Load(current, legacy).Zoom == 1.2 && File.ReadAllText(oldPath) == oldJson,
                "new settings take priority over legacy on later launches");
            File.WriteAllText(Path.Combine(current, "settings.json"), "{bad json");
            var rejected = false;
            try { Settings.Load(current, legacy); }
            catch (JsonException) { rejected = true; }
            Assert(rejected, "corrupt Pastille preferences must not silently revert to legacy");
        }
        finally { Directory.Delete(root, true); }
    }

    private static byte[] Render(OverlayWindow window, int width, int height)
    {
        window.UpdateLayout();
        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render((Visual)window.Content);
        var pixels = new byte[width * height * 4];
        rendered.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static bool ColorAt(byte[] pixels, int width, int x, int y, int r, int g, int b, int tolerance = 4)
    {
        var p = (y * width + x) * 4;
        return p >= 0 && p + 2 < pixels.Length && Math.Abs(pixels[p] - b) <= tolerance &&
            Math.Abs(pixels[p + 1] - g) <= tolerance && Math.Abs(pixels[p + 2] - r) <= tolerance;
    }

    private static async Task CheckOutlineAsync()
    {
        var settings = new Settings { Width = 240, Height = 180 };
        var window = new OverlayWindow(settings);
        var data = new byte[240 * 180 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 100; data[i + 1] = 80; data[i + 2] = 60; data[i + 3] = 255;
        }
        try
        {
            window.Show();
            window.ShowFrame(BitmapSource.Create(240, 180, 96, 96, PixelFormats.Bgra32, null, data, 240 * 4));
            await Task.Delay(100);
            Assert(window.Topmost && !window.ShowInTaskbar && window.AllowsTransparency &&
                window.WindowStyle == WindowStyle.None, "transparent topmost overlay");
            foreach (var oval in new[] { true, false })
            foreach (var hex in new[] { "#FF0000", "#0000FF", "#FFFFFF" })
            foreach (var thickness in new[] { 1, 2, 6, 20 })
            {
                settings.Oval = oval; settings.OutlineColor = hex; settings.OutlineThickness = thickness;
                settings.OutlineEnabled = true;
                window.ApplyAppearance();
                var pixels = Render(window, 240, 180);
                Settings.TryParseColor(hex, out var color);
                var count = Enumerable.Range(0, 30).Count(y =>
                    ColorAt(pixels, 240, 120, y, color.R, color.G, color.B, 12));
                Assert(Math.Abs(count - thickness) <= 1, $"outline pixel thickness {thickness}, {hex}, oval={oval}, actual={count}");
                Assert(ColorAt(pixels, 240, 120, 35, 60, 80, 100), "outline must not cover image interior");
                Assert(pixels[3] == 0, "transparent outer corner");
                settings.OutlineEnabled = false;
                window.ApplyAppearance();
                pixels = Render(window, 240, 180);
                Assert(ColorAt(pixels, 240, 120, 2, 60, 80, 100), "disabled outline must leave image, not a colored ring");
                Assert(settings.OutlineColor == hex && settings.OutlineThickness == thickness, "outline values preserved while disabled");
            }
        }
        finally { window.Close(); }
    }

    private static async Task CheckVisibilityAsync(List<string> results)
    {
        var settings = new Settings { Zoom = 1.8, PanX = 0.3, PanY = -0.4, OutlineColor = "#123456" };
        var window = new MainWindow(settings, false);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        var receiver = new Window { Width = 160, Height = 100, Title = "Pastille hotkey test" };
        try
        {
            await window.StartInTrayAsync();
            await Task.Delay(300);
            var overlay = Application.Current.Windows.OfType<OverlayWindow>().Single();
            Assert(window.Title == Localization.Text("ConfigurationTitle") &&
                window.CameraButton.Content.ToString() == Localization.Text("StartCamera") &&
                window.RefreshButton.Content.ToString() == Localization.Text("Refresh") &&
                window.VisibleChoice.Content.ToString() == Localization.Text("ShowOverlay") &&
                window.OutlineChoice.Content.ToString() == Localization.Text("ShowOutline"),
                "localized XAML window title and controls");
            Assert(overlay.Title == Localization.Text("OverlayTitle") &&
                ((Grid)overlay.Content).Children.OfType<Grid>().Single().Children.OfType<TextBlock>().Single().Text ==
                Localization.Text("Placeholder"), "localized overlay title and stopped-camera placeholder");
            Assert(overlay.ContextMenu.Items.OfType<MenuItem>().Select(item => item.Header.ToString()).SequenceEqual(
                new[] { "ShowControls", "HideOverlayHotkey", "ToggleShape", "ResetFraming" }.Select(Localization.Text)),
                "all overlay menu entries localized");
            Assert(window.ZoomLabel.Text == settings.Zoom.ToString("P0", CultureInfo.CurrentCulture) &&
                window.PanXLabel.Text == CultureInfo.CurrentCulture.NumberFormat.PositiveSign +
                    settings.PanX.ToString("P0", CultureInfo.CurrentCulture) &&
                window.PanYLabel.Text == settings.PanY.ToString("P0", CultureInfo.CurrentCulture),
                "percentage labels use regional percent spacing, symbols and positive/negative signs");
            var previousPanX = settings.PanX;
            window.PanXSlider.Value = 0;
            Assert(window.PanXLabel.Text == Localization.Text("Center"), "localized centered framing label");
            window.PanXSlider.Value = previousPanX;
            var left = overlay.Left; var top = overlay.Top;
            var handle = new WindowInteropHelper(window).Handle;
            Assert(!window.IsVisible && !window.ShowInTaskbar && !closed, "startup in tray without configuration/taskbar button");
            var identifier = new IconIdentifier
            {
                Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = handle, Id = 1
            };
            Assert(Shell_NotifyIconGetRect(ref identifier, out _) >= 0, "real icon registered in Windows notification area");
            SendMessage(handle, TrayIcon.CallbackMessage, 0, (nint)((1 << 16) | 0x7B));
            Assert(window.Tray.Menu.IsOpen, "tray right-click opens context menu");
            var trayConfig = window.Tray.Menu.Items.OfType<MenuItem>().Single(x =>
                x.Header.ToString() == Localization.Text("Configure"));
            trayConfig.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(window.IsVisible, "tray configuration command opens controls");
            window.Close();
            Assert(!window.IsVisible && !closed && overlay.IsVisible, "closing configuration hides it without exiting");
            SendMessage(handle, TrayIcon.CallbackMessage, 0, (nint)((1 << 16) | 0x401));
            await Task.Delay(50);
            Assert(window.IsVisible, "tray keyboard activation opens configuration");
            results.Add("PASS Real system tray icon, hidden startup, configuration from menu/keyboard and close-to-tray");
            Assert(overlay.IsVisible && window.VisibleChoice.IsChecked == true, "initial visibility");
            var registrationAvailable = window.HotkeyLabel.Text == Localization.Text("Hotkey");
            Assert(registrationAvailable || window.HotkeyLabel.Text == Localization.Text("HotkeyUnavailable"),
                "localized hotkey availability or conflict message");
            Assert(window.FindName("RecordButton") is null && window.FindName("MicrophoneChoice") is null,
                "recording controls removed");
            foreach (var accept in new[] { false, true })
            {
                var response = Task.Run(async () =>
                {
                    var thread = GetWindowThreadProcessId(handle, 0);
                    for (var i = 0; i < 100; i++)
                    {
                        await Task.Delay(50);
                        nint popup = 0;
                        EnumThreadWindows(thread, (candidate, _) =>
                        {
                            var name = new System.Text.StringBuilder(64);
                            GetClassName(candidate, name, 64);
                            if (name.ToString() == "#32770" && GetWindow(candidate, 4) == handle)
                                popup = candidate;
                            return true;
                        }, 0);
                        if (popup == 0) continue;
                        SendMessage(popup, 0x0111, accept ? 1 : 2, 0);
                        return;
                    }
                    throw new InvalidOperationException("Windows color picker did not open.");
                });
                var picked = Native.PickColor(window, Color.FromRgb(18, 52, 86));
                await response;
                Assert(accept ? picked == Color.FromRgb(18, 52, 86) : picked is null,
                    "Windows color picker accepts RGB or cancels without changing the color");
            }
            results.Add("PASS Native Windows color picker opens, accepts RGB and cancels correctly");
            window.OutlineChoice.IsChecked = false;
            Assert(!settings.OutlineEnabled && !window.OutlineThicknessSlider.IsEnabled &&
                !window.OutlineColorButton.IsEnabled, "outline UI disabled when off");
            window.OutlineChoice.IsChecked = true;
            window.OutlineThicknessSlider.Value = 9;
            Assert(settings.OutlineEnabled && settings.OutlineThickness == 9 &&
                window.OutlineColorButton.IsEnabled, "outline UI changes applied");
            Assert(!window.CornerRadiusSlider.IsEnabled, "radius control disabled for oval");
            window.RectangleChoice.IsChecked = true;
            window.CornerRadiusSlider.Value = 48;
            Assert(window.CornerRadiusSlider.IsEnabled && settings.CornerRadius == 48 &&
                !settings.Oval, "rectangle radius UI applies value");
            window.OvalChoice.IsChecked = true;
            Assert(!window.CornerRadiusSlider.IsEnabled && settings.CornerRadius == 48,
                "radius retained when switching to oval");
            window.WindowState = WindowState.Minimized;
            Assert(!window.IsVisible && !closed, "minimize goes to tray");
            receiver.Show();
            receiver.Activate();
            await Task.Delay(100);
            SendMessage(handle, 0x0312, 1, (nint)((0x78 << 16) | 6));
            Assert(!overlay.IsVisible && window.VisibleChoice.IsChecked == false &&
                window.ToggleVisibilityButton.Content.ToString() == Localization.Text("ShowOverlay"), "hotkey hides and syncs UI");
            SendMessage(handle, 0x0312, 1, (nint)((0x78 << 16) | 6));
            Assert(overlay.IsVisible && window.VisibleChoice.IsChecked == true &&
                !window.IsVisible, "hotkey restores overlay without restoring controls");
            Assert(overlay.Left == left && overlay.Top == top && settings.Zoom == 1.8 &&
                settings.PanX == 0.3 && settings.PanY == -0.4 && settings.OutlineColor == "#123456",
                "visibility toggle preserves placement, framing and outline");
            window.VisibleChoice.IsChecked = false;
            Assert(!overlay.IsVisible, "checkbox hides");
            window.ToggleVisibilityButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert(overlay.IsVisible && window.VisibleChoice.IsChecked == true, "button restores and syncs checkbox");
            var hideMenu = overlay.ContextMenu.Items.OfType<MenuItem>().Single(x =>
                x.Header.ToString() == Localization.Text("HideOverlayHotkey"));
            hideMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(!overlay.IsVisible && window.VisibleChoice.IsChecked == false, "context menu hides");
            SendMessage(handle, 0x0312, 1, (nint)((0x78 << 16) | 6));
            Assert(overlay.IsVisible, "hotkey restores after context menu hide");
            var trayVisibility = window.Tray.Menu.Items.OfType<MenuItem>().Single(x =>
                x.Header.ToString() == Localization.Text("HideOverlay"));
            trayVisibility.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(!overlay.IsVisible && !window.IsVisible &&
                trayVisibility.Header.ToString() == Localization.Text("ShowOverlay"), "tray hides and updates menu label");
            trayVisibility.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(overlay.IsVisible && !window.IsVisible, "tray restores without opening configuration");
            var receiverHandle = new WindowInteropHelper(receiver).Handle;
            if (registrationAvailable)
            {
                var duplicate = Native.RegisterHotKey(receiverHandle, 2, 0x4006, 0x78);
                if (duplicate) Native.UnregisterHotKey(receiverHandle, 2);
                Assert(!duplicate, "global shortcut is registered exclusively");
            }
            results.Add(registrationAvailable
                ? "PASS Global Ctrl+Shift+F9 registration and WM_HOTKEY hide/show while controls minimized"
                : "PASS WM_HOTKEY hide/show; shortcut conflict explicitly reported (another application owns it)");
            results.Add("PASS Checkbox, button and context-menu visibility synchronization; framing preserved");
            var quit = window.Tray.Menu.Items.OfType<MenuItem>().Single(x => x.Header.ToString() == Localization.Text("Quit"));
            quit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Task.Delay(150);
            Assert(closed && !overlay.IsVisible, "tray quit closes configuration and overlay");
            Assert(Shell_NotifyIconGetRect(ref identifier, out _) < 0, "tray icon removed on quit");
            results.Add("PASS Tray visibility and quit commands remove icon and close all app windows");
            if (registrationAvailable)
            {
                var released = Native.RegisterHotKey(receiverHandle, 2, 0x4006, 0x78);
                if (released) Native.UnregisterHotKey(receiverHandle, 2);
                Assert(released, "shortcut released at shutdown");
            }
        }
        finally
        {
            if (!closed) window.RequestExit();
            receiver.Close();
        }
    }

    private static async Task CheckCornerRadiusAsync()
    {
        var settings = new Settings { Width = 240, Height = 180, Oval = false, OutlineColor = "#FF0000" };
        var window = new OverlayWindow(settings);
        var data = new byte[240 * 180 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 100; data[i + 1] = 80; data[i + 2] = 60; data[i + 3] = 255;
        }
        try
        {
            window.Show();
            window.ShowFrame(BitmapSource.Create(240, 180, 96, 96, PixelFormats.Bgra32, null, data, 240 * 4));
            await Task.Delay(100);
            foreach (var size in new[] { (240, 180), (120, 90), (640, 180) })
            foreach (var radius in new[] { 0, 12, 40, 200 })
            foreach (var thickness in new[] { 1, 6, 20 })
            foreach (var outlined in new[] { false, true })
            {
                window.Width = size.Item1; window.Height = size.Item2;
                settings.CornerRadius = radius; settings.OutlineThickness = thickness;
                settings.OutlineEnabled = outlined;
                window.UpdateLayout(); window.ApplyAppearance();
                var pixels = Render(window, size.Item1, size.Item2);
                var effectiveRadius = Math.Min(radius, Math.Min(size.Item1, size.Item2) / 2d);
                var geometry = new RectangleGeometry(new Rect(0, 0, size.Item1, size.Item2),
                    effectiveRadius, effectiveRadius);
                foreach (var point in new[] { (1, 1), (6, 6), (20, 20), (35, 10), (size.Item1 / 2, size.Item2 / 2) })
                {
                    var inside = geometry.FillContains(new Point(point.Item1 + 0.5, point.Item2 + 0.5));
                    var alpha = pixels[(point.Item2 * size.Item1 + point.Item1) * 4 + 3];
                    Assert(inside ? alpha > 220 : alpha < 35,
                        $"corner clipping: size={size}, radius={radius}, thickness={thickness}, outlined={outlined}, point={point}, alpha={alpha}");
                }
                Assert(ColorAt(pixels, size.Item1, size.Item1 / 2, size.Item2 / 2, 60, 80, 100),
                    "radius does not affect centered image");
                if (outlined)
                    Assert(ColorAt(pixels, size.Item1, size.Item1 / 2, Math.Max(0, thickness / 2), 255, 0, 0, 15),
                        "rounded outline preserves configured color/thickness");
            }
            settings.Oval = true; settings.CornerRadius = 0;
            window.ApplyAppearance();
            var oval = Render(window, 640, 180);
            settings.CornerRadius = 200;
            window.ApplyAppearance();
            Assert(oval.SequenceEqual(Render(window, 640, 180)), "radius must not change oval rendering");
        }
        finally { window.Close(); }
    }

    private static async Task CheckCameraFramingAsync()
    {
        var data = new byte[960 * 540 * 4];
        for (var y = 0; y < 540; y++)
        for (var x = 0; x < 960; x++)
        {
            var p = (y * 960 + x) * 4;
            data[p] = 100;
            data[p + 1] = (byte)(y * 255 / 539);
            data[p + 2] = (byte)(x * 255 / 959);
            data[p + 3] = 255;
        }
        var frame = new WriteableBitmap(960, 540, 96, 96, PixelFormats.Pbgra32, null);
        frame.WritePixels(new Int32Rect(0, 0, 960, 540), data, 960 * 4, 0);
        var settings = new Settings { Width = 500, Height = 460, Mirror = true };
        var window = new OverlayWindow(settings);
        try
        {
            window.Show();
            await Task.Delay(100);
            window.ShowFrame(frame);
            await Task.Delay(100);
            Assert(ColorAt(Render(window, 500, 460), 500, 250, 230, 127, 127, 100),
                "wide mirrored live-camera frame must be centered");
            foreach (var size in new[] { (500, 460), (240, 360), (640, 180) })
            foreach (var oval in new[] { true, false })
            foreach (var mirror in new[] { true, false })
            foreach (var framing in new[] { (0.5, 0d, 0d), (1d, 0d, 0d), (1.75, 0.6, -0.4), (4d, -0.8, 0.7) })
            {
                window.Width = size.Item1; window.Height = size.Item2;
                settings.Oval = oval; settings.Mirror = mirror;
                settings.Zoom = framing.Item1; settings.PanX = framing.Item2; settings.PanY = framing.Item3;
                window.UpdateLayout();
                window.ApplyAppearance();
                var pixels = Render(window, size.Item1, size.Item2);
                foreach (var point in new[] { (0.5, 0.5), (0.2, 0.5), (0.8, 0.5), (0.5, 0.2), (0.5, 0.8) })
                {
                    var x = (int)(point.Item1 * size.Item1);
                    var y = (int)(point.Item2 * size.Item2);
                    var expected = FramingColor(settings, size.Item1, size.Item2, x + 0.5, y + 0.5);
                    Assert(ColorAt(pixels, size.Item1, x, y, expected.R, expected.G, expected.B),
                        $"framing pixels: size {size}, oval {oval}, mirror {mirror}, zoom/pan {framing}, point {point}");
                }
            }
            var oldLeft = window.Left; var oldTop = window.Top;
            window.ResetFraming();
            Assert(settings.Zoom == 1 && settings.PanX == 0 && settings.PanY == 0 &&
                window.Left == oldLeft && window.Top == oldTop && window.Width == 640 && window.Height == 180,
                "framing reset must not change overlay position or dimensions");
        }
        finally { window.Close(); }
    }

    private static (int R, int G, int B) FramingColor(Settings settings, double width, double height, double x, double y)
    {
        var scale = Math.Max(width / 960, height / 540) * settings.Zoom;
        var imageWidth = 960 * scale;
        var imageHeight = 540 * scale;
        var u = (x - (width - imageWidth) / 2 - settings.PanX * width / 2) / imageWidth;
        var v = (y - (height - imageHeight) / 2 - settings.PanY * height / 2) / imageHeight;
        if (u < 0 || u > 1 || v < 0 || v > 1) return (28, 38, 48);
        if (settings.Mirror) u = 1 - u;
        return ((int)(u * 255), (int)(v * 255), 100);
    }
}
