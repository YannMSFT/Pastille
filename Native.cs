using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pastille;

internal sealed record Display(string Name, Native.Rect Bounds)
{
    public override string ToString() => $"{Name} — {Bounds.Width} × {Bounds.Height}";
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    private delegate bool MonitorCallback(nint monitor, nint dc, ref Rect rect, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);

    internal static List<Display> Displays()
    {
        var displays = new List<Display>();
        var error = 0;
        MonitorCallback callback = (nint monitor, nint dc, ref Rect rect, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (!GetMonitorInfo(monitor, ref info))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }
            displays.Add(new Display(info.Device, info.Monitor));
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception(error);
        return displays;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ColorDialog
    {
        public uint Size;
        public nint Owner, Instance;
        public uint Result;
        public nint CustomColors;
        public uint Flags;
        public nint Data, Hook, Template;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ChooseColor(ref ColorDialog dialog);
    [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();
    private static readonly int[] customColors = Enumerable.Repeat(0xFFFFFF, 16).ToArray();

    internal static Color? PickColor(Window owner, Color initial)
    {
        var colors = GCHandle.Alloc(customColors, GCHandleType.Pinned);
        try
        {
            var dialog = new ColorDialog
            {
                Size = (uint)Marshal.SizeOf<ColorDialog>(),
                Owner = new WindowInteropHelper(owner).Handle,
                Result = (uint)(initial.R | initial.G << 8 | initial.B << 16),
                CustomColors = colors.AddrOfPinnedObject(),
                Flags = 0x103
            };
            if (ChooseColor(ref dialog))
                return Color.FromRgb((byte)dialog.Result, (byte)(dialog.Result >> 8), (byte)(dialog.Result >> 16));
            var error = CommDlgExtendedError();
            if (error != 0) throw new Win32Exception((int)error, Localization.Text("ColorDialogError"));
            return null;
        }
        finally { colors.Free(); }
    }
}
