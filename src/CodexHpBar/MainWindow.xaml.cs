using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using CodexHpBar.Core;

namespace CodexHpBar;

public partial class MainWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;

    public event EventHandler? RefreshRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindowStyles();
        MouseRightButtonUp += OnRightClick;
    }

    public void UpdateSnapshot(QuotaSnapshot snapshot)
    {
        Widget.Snapshot = snapshot;
        ToolTip = TooltipBuilder.Build(snapshot);
    }

    public void Place(TaskbarPlacement taskbar)
    {
        var bounds = TaskbarGeometry.Calculate(taskbar);
        SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoactivate | SwpShowwindow);
    }

    private void ApplyWindowStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        SetWindowLongPtr(handle, GwlExstyle, new nint(style | WsExToolwindow | WsExNoactivate));
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateItem("立即更新", () => RefreshRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateItem("設定", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem("結束", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static MenuItem CreateItem(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        return item;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
