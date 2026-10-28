using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexHpBar;

public partial class App : Application
{
    private AppController? _controller;
    private Mutex? _mutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        _mutex = new Mutex(true, "Local\\CodexHpBar.SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        var settingsService = new SettingsService();
        if (e.Args.Contains("--reset-settings", StringComparer.OrdinalIgnoreCase))
        {
            settingsService.Reset();
            Shutdown();
            return;
        }

        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Shutdown(await SelfTestRunner.RunAsync() ? 0 : 1);
            return;
        }

        var demo = DemoModeExtensions.Parse(e.Args);
        var settings = new CodexHpBar.Core.AppSettings(true, false).Normalize();
        if (demo == DemoMode.None)
        {
            var loaded = settingsService.Load();
            settings = ((loaded ?? CodexHpBar.Core.AppSettings.Default) with
            {
                BackgroundMode = true,
                StartWithWindows = true
            }).Normalize();
            if (settings != loaded) settingsService.Save(settings);
            StartupManager.RepairIfNeeded(true);
        }

        _controller = new AppController(settings, demo);
        await _controller.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
