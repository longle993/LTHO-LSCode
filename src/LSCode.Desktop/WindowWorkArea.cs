using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace LSCode.Desktop;
// Borderless windows must maximize to the current monitor's work area, including taskbar/DPI changes.
internal static class WindowWorkArea
{
    internal static void Attach(Window window) => window.SourceInitialized += (_, _) =>
    {
        var source = (HwndSource)PresentationSource.FromVisual(window);
        FitInitialBounds(source.Handle);
        source.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
        {
            if (message != 0x0024) return 0; // WM_GETMINMAXINFO
            var monitor = ReadMonitor(hwnd);
            if (monitor is not { } m) return 0;
            var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            limits.MaxPosition = new(m.Work.Left - m.Monitor.Left, m.Work.Top - m.Monitor.Top);
            limits.MaxSize = new(m.Work.Right - m.Work.Left, m.Work.Bottom - m.Work.Top);
            var scale = source.CompositionTarget.TransformToDevice;
            limits.MinTrackSize = new(
                Math.Min(limits.MaxSize.X, (int)Math.Ceiling(window.MinWidth * scale.M11)),
                Math.Min(limits.MaxSize.Y, (int)Math.Ceiling(window.MinHeight * scale.M22)));
            Marshal.StructureToPtr(limits, lParam, false); handled = true; return 0;
        });
    };
    private static void FitInitialBounds(nint hwnd)
    {
        if (ReadMonitor(hwnd) is not { } monitor || !GetWindowRect(hwnd, out var rect)) return;
        var work = monitor.Work;
        var width = Math.Min(rect.Right - rect.Left, work.Right - work.Left);
        var height = Math.Min(rect.Bottom - rect.Top, work.Bottom - work.Top);
        var left = Math.Clamp(rect.Left, work.Left, work.Right - width);
        var top = Math.Clamp(rect.Top, work.Top, work.Bottom - height);
        if (left != rect.Left || top != rect.Top || width != rect.Right - rect.Left || height != rect.Bottom - rect.Top)
            SetWindowPos(hwnd, 0, left, top, width, height, 0x0014); // SWP_NOZORDER | SWP_NOACTIVATE
    }
    internal static bool FitsMonitor(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return ReadMonitor(hwnd) is { } m && GetWindowRect(hwnd, out var rect)
            && rect.Left >= m.Work.Left && rect.Top >= m.Work.Top && rect.Right <= m.Work.Right && rect.Bottom <= m.Work.Bottom;
    }
    private static MonitorInfo? ReadMonitor(nint hwnd)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info) ? info : null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point(int x, int y) { public int X = x, Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
