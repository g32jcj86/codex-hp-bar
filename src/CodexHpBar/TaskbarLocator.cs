using System.Runtime.InteropServices;
using System.Text;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class TaskbarLocator : ITaskbarLocator
{
    public IReadOnlyList<TaskbarPlacement> LocateAll()
    {
        var placements = new List<TaskbarPlacement>();
        EnumWindows((handle, _) =>
        {
            var className = GetClass(handle);
            if (className is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
            if (!GetWindowRect(handle, out var taskbar)) return true;

            nint trayHandle = 0;
            EnumChildWindows(handle, (child, _) =>
            {
                if (GetClass(child) == "TrayNotifyWnd") trayHandle = child;
                return true;
            }, 0);

            var tray = default(Rect);
            var hasTray = trayHandle != 0 && GetWindowRect(trayHandle, out tray);
            var anchorLeft = hasTray ? tray.Left : taskbar.Left;
            var dpi = (int)Math.Max(96, GetDpiForWindow(handle));
            placements.Add(new TaskbarPlacement(handle, anchorLeft, taskbar.Top, taskbar.Right, taskbar.Bottom, dpi, hasTray));
            return true;
        }, 0);
        return placements.OrderBy(item => item.Left).ToArray();
    }

    private static string GetClass(nint handle)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private delegate bool EnumWindowsProc(nint handle, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint handle, StringBuilder name, int maximum);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint handle);
}
