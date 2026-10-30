using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexHpBar;

public partial class App : Application
{
    private readonly Action<string> _log = message => File.AppendAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexHpBar")).FullName, "exit.log"), $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
    private AppController? _controller;
    private Mutex? _mutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => _log($"dispatcher-unhandled-exception {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _log($"unhandled-exception {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) => _log($"unobserved-task-exception {args.Exception}");
        SessionEnding += (_, args) => _log($"session-ending reason={args.ReasonSessionEnding}");

        base.OnStartup(e);
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        _mutex = new Mutex(true, "Local\\CodexHpBar.SingleInstance", out var created);
        if (!created)
        {
            _log("duplicate-instance");
            Shutdown();
            return;
        }

        _log($"start pid={Environment.ProcessId}");

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
        _log($"exit code={e.ApplicationExitCode}");
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
