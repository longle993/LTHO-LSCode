using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LSCode.Shell;
public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    private nint WindowBoundsHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0024) return 0; // WM_GETMINMAXINFO: native pixels, including per-monitor DPI.
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor)) return 0;
        var bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxPosition = new NativePoint { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
        bounds.MaxSize = new NativePoint { X = monitor.Work.Right - monitor.Work.Left, Y = monitor.Work.Bottom - monitor.Work.Top };
        Marshal.StructureToPtr(bounds, lParam, false); handled = true;
        return 0;
    }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    internal static Rect FitStartupBounds(Rect work, Size desired)
    {
        var width = Math.Min(desired.Width, Math.Max(1, work.Width - 24));
        var height = Math.Min(desired.Height, Math.Max(1, work.Height - 24));
        return new Rect(work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2, width, height);
    }
    private void FitWindowToScreen()
    {
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowBoundsHook);
        var work = SystemParameters.WorkArea;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(this).Handle, 2), ref info))
        {
            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
            work = new Rect(transform.Transform(new Point(info.Work.Left, info.Work.Top)), transform.Transform(new Point(info.Work.Right, info.Work.Bottom)));
        }
        var bounds = FitStartupBounds(work, new Size(Width, Height));
        MinWidth = Math.Min(MinWidth, bounds.Width); MinHeight = Math.Min(MinHeight, bounds.Height);
        Width = bounds.Width; Height = bounds.Height; Left = bounds.Left; Top = bounds.Top;
        WindowState = WindowState.Maximized;
    }
}
