namespace CodexHpBar;

public static class SelfTestRunner
{
    public static async Task<bool> RunAsync()
    {
        try
        {
            if (CodexDetector.FindAppServerExecutable() is null) return false;
            if (new TaskbarLocator().LocateAll().Count == 0) return false;
            await using var source = new AppServerQuotaSource();
            var snapshot = await source.ReadAsync();
            return snapshot.OrderedWindows.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
