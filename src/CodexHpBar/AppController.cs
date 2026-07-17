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
        _settings = settings;
        _demo = demo;
        _watchTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, async (_, _) => await WatchAsync(), Application.Current.Dispatcher);
        _syncTimer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, async (_, _) => await RefreshAsync(), Application.Current.Dispatcher);
        _memoryTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.ApplicationIdle, (_, _) => MemoryTrimmer.Trim(), Application.Current.Dispatcher);
    }

    public async Task StartAsync()
    {
        if (_demo != DemoMode.None)
        {
            _snapshot = CreateDemo(_demo);
            EnsureWindows();
            _memoryTimer.Start();
            _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => Application.Current.Dispatcher.Invoke(MemoryTrimmer.Trim));
            return;
        }

        _watchTimer.Start();
        _memoryTimer.Start();
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
            var window = new MainWindow();
            window.RefreshRequested += async (_, _) => await RefreshAsync();
            window.SettingsRequested += (_, _) => ShowSettings();
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

    private void ShowSettings()
    {
        var dialog = new OnboardingWindow(_settings) { Title = "Codex HP Bar 設定" };
        if (dialog.ShowDialog() != true) return;
        _settings = dialog.Settings.Normalize();
        _settingsService.Save(_settings);
        StartupManager.Apply(_settings.StartWithWindows);
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
        foreach (var window in _windows) window.Close();
        if (_quotaSource is not null) await _quotaSource.DisposeAsync();
    }
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
