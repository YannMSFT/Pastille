using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pastille;

internal sealed class TrayIcon : IDisposable
{
    internal const int CallbackMessage = 0x8001;
    private const uint IconId = 1;
    private readonly HwndSource source;
    private readonly Action configure;
    private readonly Action<Exception> failed;
    private readonly Func<bool> overlayVisible;
    private readonly uint taskbarCreated;
    private NotifyIconData data;
    private nint icon;
    private bool disposed;
    internal ContextMenu Menu { get; } = new() { Placement = PlacementMode.MousePoint };
    private readonly MenuItem visibilityItem = new();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, Callback;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateIconFromResourceEx(byte[] bytes, uint size, bool isIcon,
        uint version, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);

    public TrayIcon(HwndSource source, Action configure, Action toggle, Action quit,
        Func<bool> overlayVisible, Action<Exception> failed)
    {
        this.source = source;
        this.configure = configure;
        this.overlayVisible = overlayVisible;
        this.failed = failed;
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        if (taskbarCreated == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        AddItem(Localization.Text("Configure"), configure);
        visibilityItem.Click += (_, _) => { Menu.IsOpen = false; toggle(); };
        Menu.Items.Add(visibilityItem);
        Menu.Items.Add(new Separator());
        AddItem(Localization.Text("Quit"), quit);
        Menu.Closed += (_, _) =>
        {
            if (!disposed) Shell_NotifyIcon(3, ref data);
        };
        try
        {
            icon = CreateIcon();
            data = new NotifyIconData
            {
                Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = source.Handle, Id = IconId,
                Flags = 0x87, Callback = CallbackMessage, Icon = icon,
                Tip = Localization.Text("TrayTip"), Info = "", Title = ""
            };
            source.AddHook(WindowMessage);
            Add();
            RefreshVisibility();
        }
        catch { Dispose(); throw; }
    }

    private void AddItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => { Menu.IsOpen = false; action(); };
        Menu.Items.Add(item);
    }

    private void Add()
    {
        if (!Shell_NotifyIcon(0, ref data))
            throw new InvalidOperationException(Localization.Text("TrayAddError"));
        data.Version = 4;
        if (!Shell_NotifyIcon(4, ref data))
            throw new InvalidOperationException(Localization.Text("TrayInitError"));
    }

    internal void RefreshVisibility() =>
        visibilityItem.Header = Localization.Text(overlayVisible() ? "HideOverlay" : "ShowOverlay");

    private nint WindowMessage(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (disposed) return 0;
        if ((uint)message == taskbarCreated)
        {
            try { Add(); }
            catch (Exception ex) { failed(ex); }
        }
        if (message != CallbackMessage || ((long)lparam >> 16 & 0xFFFF) != IconId) return 0;
        handled = true;
        var notification = (int)((long)lparam & 0xFFFF);
        if (notification is 0x400 or 0x401 or 0x203)
        {
            _ = source.Dispatcher.BeginInvoke(configure);
        }
        else if (notification == 0x7B)
        {
            SetForegroundWindow(hwnd);
            RefreshVisibility();
            Menu.IsOpen = true;
        }
        return 0;
    }

    internal static RenderTargetBitmap RenderIcon(int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 32d, size / 32d));
            var body = Geometry.Parse("M 11,2 L 21,2 26,8 26,24 21,30 11,30 6,24 6,8 Z");
            var fill = new LinearGradientBrush(Color.FromRgb(255, 255, 253),
                Color.FromRgb(196, 205, 211), new Point(0.2, 0), new Point(0.8, 1));
            dc.DrawGeometry(fill, new Pen(new SolidColorBrush(Color.FromRgb(83, 101, 114)), 1.25), body);
            var face = Geometry.Parse("M 12,5 L 20,5 23,9 23,23 20,27 12,27 9,23 9,9 Z");
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(248, 248, 243)),
                new Pen(Brushes.White, 0.75), face);
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static nint CreateIcon()
    {
        var bitmap = RenderIcon(32);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();
        var result = CreateIconFromResourceEx(bytes, (uint)bytes.Length, true, 0x30000, 32, 32, 0);
        if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), Localization.Text("TrayCreateError"));
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Menu.IsOpen = false;
        source.RemoveHook(WindowMessage);
        if (data.Window != 0 && !Shell_NotifyIcon(2, ref data))
            App.Log(new InvalidOperationException(Localization.Text("TrayRemoveError")));
        if (icon != 0) { DestroyIcon(icon); icon = 0; }
    }
}
