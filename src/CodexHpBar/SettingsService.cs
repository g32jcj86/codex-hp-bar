using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexHpBar");
    private string PathName => Path.Combine(_directory, "settings.json");

    public AppSettings? Load()
    {
        try
        {
            return File.Exists(PathName)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName))?.Normalize()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PathName, JsonSerializer.Serialize(settings.Normalize(), Options));
    }

    public void Reset()
    {
        if (File.Exists(PathName))
        {
            File.Delete(PathName);
        }

        StartupManager.Apply(false);
    }
}

public static class StartupManager
{
    private const string ShortcutName = "Codex HP Bar.lnk";
    private static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);
    private static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CodexHpBar.exe");

    public static void Apply(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            return;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("無法建立開機啟動捷徑");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = ExecutablePath;
        shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(ExecutablePath);
        shortcut.Description = "Codex 菇菇寶貝 HP 額度監測器 — github.com/g32jcj86/codex-hp-bar";
        shortcut.Save();
    }

    public static void RepairIfNeeded(bool enabled)
    {
        if (enabled) Apply(true);
        if (!enabled && File.Exists(ShortcutPath)) Apply(false);
    }
}
