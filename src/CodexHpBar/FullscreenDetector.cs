using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using CodexHpBar.Core;

namespace CodexHpBar;

public static class FullscreenDetector
{
    public static bool IsFullscreenOn(TaskbarPlacement taskbar)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0 || foreground == taskbar.Handle || !GetWindowRect(foreground, out var window)) return false;
        _ = GetWindowThreadProcessId(foreground, out var processId);
        try
        {
            using var process = processId == 0 ? null : Process.GetProcessById((int)processId);
            if (process?.ProcessName.Equals("LINE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        var className = new StringBuilder(128);
        _ = GetClassName(foreground, className, className.Capacity);
        if (className.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") return false;

        var monitor = MonitorFromWindow(taskbar.Handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return false;
        return window.Left <= info.Monitor.Left && window.Top <= info.Monitor.Top &&
               window.Right >= info.Monitor.Right && window.Bottom >= info.Monitor.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint handle, StringBuilder name, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
}
