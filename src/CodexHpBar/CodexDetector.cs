using System.Diagnostics;
using System.IO;

namespace CodexHpBar;

public static class CodexDetector
{
    public static bool IsDesktopAppRunning()
    {
        foreach (var process in Process.GetProcessesByName("ChatGPT"))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (path?.Contains("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore protected or terminating processes.
                }
            }
        }

        return false;
    }

    public static string? FindAppServerExecutable()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }
}
