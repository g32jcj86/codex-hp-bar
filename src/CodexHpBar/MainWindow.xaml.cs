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
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private ContextMenu? _activeMenu;

    public event EventHandler? RefreshRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public MainWindow(MascotSettings? mascot = null)
    {
        InitializeComponent();
        if (mascot is not null) Widget.SetMascot(mascot);
        SourceInitialized += (_, _) => ApplyWindowStyles();
        PreviewMouseRightButtonUp += OnRightClick;
    }

    public void UpdateSnapshot(QuotaSnapshot snapshot)
    {
        Widget.Snapshot = snapshot;
        ToolTip = TooltipBuilder.Build(snapshot);
    }

    public void UpdateMascot(MascotSettings settings) => Widget.SetMascot(settings);

    public void Place(TaskbarPlacement taskbar)
    {
        var bounds = TaskbarGeometry.Calculate(taskbar);
        SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoactivate | SwpShowwindow);
    }

    public void BringAboveTaskbar()
    {
        if (!IsVisible) return;
        _ = SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), 0, 0, 0, 0,
            SwpNoactivate | SwpNosize | SwpNomove);
    }

    private void ApplyWindowStyles()
    {
        SetNoActivate(true);
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        _activeMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        SetNoActivate(false);
        var menu = new ContextMenu { StaysOpen = false };
        menu.Items.Add(CreateItem("立即更新", () => RefreshRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateItem("設定", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateItem("關閉監測器", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        menu.PlacementTarget = Widget;
        menu.Closed += (_, _) =>
        {
            _activeMenu = null;
            SetNoActivate(true);
        };
        menu.Opened += (_, _) => menu.Focus();
        _activeMenu = menu;
        _ = SetForegroundWindow(new WindowInteropHelper(this).Handle);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void SetNoActivate(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64() | WsExToolwindow;
        style = enabled ? style | WsExNoactivate : style & ~WsExNoactivate;
        SetWindowLongPtr(handle, GwlExstyle, new nint(style));
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
}
