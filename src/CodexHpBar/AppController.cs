using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using CodexHpBar.Core;

namespace CodexHpBar;

public enum DemoMode { None, Single, Dual, Offline, Low }

public static class DemoModeExtensions
{
    public static DemoMode Parse(string[] args)
    {
        var index = Array.FindIndex(args, value => value.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return DemoMode.None;
        return Enum.TryParse<DemoMode>(args[index + 1], true, out var mode) ? mode : DemoMode.None;
    }
}

public sealed class AppController : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly TaskbarLocator _locator = new();
    private readonly DispatcherTimer _watchTimer;
    private readonly DispatcherTimer _syncTimer;
    private readonly DispatcherTimer _memoryTimer;
    private readonly DispatcherTimer _zOrderTimer;
    private readonly ForegroundWatcher _foregroundWatcher = new();
    private readonly List<MainWindow> _windows = [];
    private readonly DemoMode _demo;
    private AppSettings _settings;
    private AppServerQuotaSource? _quotaSource;
    private QuotaSnapshot _snapshot = QuotaSnapshot.Offline();
    private bool _refreshing;
    private int _failureCount;

    public AppController(SettingsService settingsService, AppSettings settings, DemoMode demo)
    {
        _settingsService = settingsService;
        _settings = settings.Normalize();
        _demo = demo;
        _watchTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, async (_, _) => await RunGuardedAsync(WatchAsync), Application.Current.Dispatcher);
        _syncTimer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, async (_, _) => await RunGuardedAsync(RefreshAsync), Application.Current.Dispatcher);
        _memoryTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.ApplicationIdle, (_, _) => MemoryTrimmer.Trim(), Application.Current.Dispatcher);
        _zOrderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Send, (_, _) =>
        {
            foreach (var window in _windows) window.BringAboveTaskbar();
        }, Application.Current.Dispatcher);
        _foregroundWatcher.ForegroundChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var window in _windows) window.BringAboveTaskbar();
        }, DispatcherPriority.Send);
    }

    public async Task StartAsync()
    {
        if (_demo != DemoMode.None)
        {
            _snapshot = CreateDemo(_demo);
            EnsureWindows();
            _memoryTimer.Start();
            _zOrderTimer.Start();
            _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => Application.Current.Dispatcher.Invoke(MemoryTrimmer.Trim));
            return;
        }

        _watchTimer.Start();
        _memoryTimer.Start();
        _zOrderTimer.Start();
        await WatchAsync();
    }

    private async Task WatchAsync()
    {
        if (!CodexDetector.IsDesktopAppRunning())
        {
            await FadeOutWindowsAsync();
            _syncTimer.Stop();
            if (_quotaSource is not null)
            {
                await _quotaSource.DisposeAsync();
                _quotaSource = null;
            }
            if (!_settings.BackgroundMode) Application.Current.Shutdown();
            return;
        }

        EnsureWindows();
        if (!_syncTimer.IsEnabled) _syncTimer.Start();
        if (_quotaSource is null) await RefreshAsync();
    }

    private void EnsureWindows()
    {
        var taskbars = _locator.LocateAll();
        while (_windows.Count < taskbars.Count)
        {
            var window = new MainWindow(_settings.Mascot);
            window.RefreshRequested += async (_, _) => await RefreshAsync();
            window.SettingsRequested += (_, _) => ShowSettings(window);
            window.ExitRequested += (_, _) => Application.Current.Shutdown();
            _windows.Add(window);
            window.Show();
        }

        for (var index = 0; index < _windows.Count; index++)
        {
            if (index >= taskbars.Count)
            {
                _windows[index].Hide();
                continue;
            }
            var taskbar = taskbars[index];
            if (taskbar.Bottom - taskbar.Top < 10 || FullscreenDetector.IsFullscreenOn(taskbar))
            {
                _windows[index].Hide();
                continue;
            }
            _windows[index].UpdateSnapshot(_snapshot);
            if (!_windows[index].IsVisible) _windows[index].Show();
            _windows[index].Place(taskbar);
        }
    }

    private void HideWindows()
    {
        foreach (var window in _windows) window.Hide();
    }

    private async Task RefreshAsync()
    {
        if (_demo != DemoMode.None || _refreshing) return;
        _refreshing = true;
        try
        {
            _quotaSource ??= CreateSource();
            _snapshot = await _quotaSource.ReadAsync();
            if (_snapshot.Error is null)
            {
                _failureCount = 0;
                _syncTimer.Interval = TimeSpan.FromSeconds(15);
            }
            else
            {
                var delays = new[] { 5, 15, 30, 60 };
                _syncTimer.Interval = TimeSpan.FromSeconds(delays[Math.Min(_failureCount, delays.Length - 1)]);
                _failureCount++;
            }
            foreach (var window in _windows) window.UpdateSnapshot(_snapshot);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task RunGuardedAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            _snapshot = _snapshot.OrderedWindows.Count > 0
                ? _snapshot with { IsStale = true, Error = exception.Message }
                : QuotaSnapshot.Offline(exception.Message);
            foreach (var window in _windows) window.UpdateSnapshot(_snapshot);
        }
    }

    private async Task FadeOutWindowsAsync()
    {
        var visible = _windows.Where(window => window.IsVisible).ToArray();
        if (visible.Length == 0) return;
        foreach (var window in visible) window.Opacity = 0;
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        foreach (var window in visible)
        {
            window.Hide();
            window.Opacity = 1;
        }
    }

    private AppServerQuotaSource CreateSource()
    {
        var source = new AppServerQuotaSource();
        source.SnapshotChanged += (_, snapshot) => Application.Current.Dispatcher.Invoke(() =>
        {
            _snapshot = snapshot;
            foreach (var window in _windows) window.UpdateSnapshot(snapshot);
        });
        return source;
    }

    private void ShowSettings(MainWindow owner)
    {
        var dialog = new OnboardingWindow(_settings)
        {
            Title = "Codex HP Bar 設定",
            Owner = owner,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Loaded += (_, _) =>
        {
            dialog.Activate();
            dialog.Focus();
        };
        if (dialog.ShowDialog() != true) return;
        _settings = dialog.Settings.Normalize();
        _settingsService.Save(_settings);
        StartupManager.Apply(_settings.StartWithWindows);
        foreach (var window in _windows) window.UpdateMascot(_settings.Mascot!);
    }

    private static QuotaSnapshot CreateDemo(DemoMode mode)
    {
        var reset = DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();
        return mode switch
        {
            DemoMode.Single => new(new RateLimitWindow(17, 10080, reset), null, null, DateTimeOffset.UtcNow),
            DemoMode.Dual => new(new RateLimitWindow(24, 300, reset), new RateLimitWindow(41, 10080, reset), null, DateTimeOffset.UtcNow),
            DemoMode.Low => new(new RateLimitWindow(92, 300, reset), new RateLimitWindow(83, 10080, reset), null, DateTimeOffset.UtcNow),
            _ => QuotaSnapshot.Offline("示範離線狀態")
        };
    }

    public async ValueTask DisposeAsync()
    {
        _watchTimer.Stop();
        _syncTimer.Stop();
        _memoryTimer.Stop();
        _zOrderTimer.Stop();
        _foregroundWatcher.Dispose();
        foreach (var window in _windows) window.Close();
        if (_quotaSource is not null) await _quotaSource.DisposeAsync();
    }
}

internal sealed class ForegroundWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutofcontext = 0x0000;
    private const uint WineventSkipownprocess = 0x0002;
    private readonly WinEventDelegate _callback;
    private readonly nint _hook;

    public ForegroundWatcher()
    {
        _callback = (_, _, _, _, _, _, _) => ForegroundChanged?.Invoke(this, EventArgs.Empty);
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, _callback, 0, 0,
            WineventOutofcontext | WineventSkipownprocess);
    }

    public event EventHandler? ForegroundChanged;

    public void Dispose()
    {
        if (_hook != 0) _ = UnhookWinEvent(_hook);
    }

    private delegate void WinEventDelegate(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint eventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventDelegate callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hook);
}

internal static class MemoryTrimmer
{
    public static void Trim()
    {
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
        _ = EmptyWorkingSet(GetCurrentProcess());
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(nint process);
}
