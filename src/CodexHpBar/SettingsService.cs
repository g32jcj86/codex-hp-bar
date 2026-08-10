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
            if (!File.Exists(PathName)) return null;

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName))?.Normalize();
            if (loaded is null) return null;

            var prepared = PrepareMascotAsset(loaded);
            if (prepared != loaded)
            {
                try
                {
                    Save(prepared);
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    // The copied asset is still usable for this session even if the settings file is read-only.
                }
            }

            return prepared;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(AppSettings settings)
    {
        settings = PrepareMascotAsset(settings.Normalize());
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PathName, JsonSerializer.Serialize(settings, Options));
    }

    private static AppSettings PrepareMascotAsset(AppSettings settings)
    {
        var mascot = settings.Mascot!;
        if (mascot.Mode == MascotAssetMode.BuiltInMushroom) return settings;

        try
        {
            return settings with { Mascot = MascotAssetStorage.EnsureLocalCopy(mascot) };
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            return settings;
        }
    }

    private static bool IsStorageException(Exception exception) => exception is IOException
        or UnauthorizedAccessException
        or ArgumentException
        or NotSupportedException;

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
