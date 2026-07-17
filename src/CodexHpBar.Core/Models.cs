namespace CodexHpBar.Core;

public sealed record RateLimitWindow(double UsedPercent, int WindowDurationMins, long ResetsAt)
{
    public int RemainingPercent => (int)Math.Clamp(
        Math.Round(100d - UsedPercent, MidpointRounding.AwayFromZero), 0d, 100d);

    public DateTimeOffset ResetTime => DateTimeOffset.FromUnixTimeSeconds(ResetsAt);
}

public sealed record QuotaSnapshot(
    RateLimitWindow? Primary,
    RateLimitWindow? Secondary,
    string? RateLimitReachedType,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? Error = null)
{
    public IReadOnlyList<RateLimitWindow> OrderedWindows =>
        new[] { Primary, Secondary }
            .Where(window => window is not null)
            .Cast<RateLimitWindow>()
            .OrderBy(window => window.WindowDurationMins)
            .ToArray();

    public static QuotaSnapshot Offline(string? error = null) =>
        new(null, null, null, DateTimeOffset.UtcNow, true, error);
}

public sealed record AppSettings(bool BackgroundMode, bool StartWithWindows)
{
    public static AppSettings Default { get; } = new(false, false);

    public AppSettings Normalize() => StartWithWindows && !BackgroundMode
        ? this with { BackgroundMode = true }
        : this;
}

public sealed record TaskbarPlacement(nint Handle, int Left, int Top, int Right, int Bottom, int Dpi, bool HasTray)
{
    public double Scale => Dpi / 96d;
}

public sealed record OverlayBounds(int Left, int Top, int Width, int Height);

public static class TaskbarGeometry
{
    private const int SecondaryStatusAreaWidth = 120;

    public static OverlayBounds Calculate(TaskbarPlacement taskbar)
    {
        var scale = taskbar.Scale;
        var width = (int)Math.Round(150 * scale);
        var height = (int)Math.Round(38 * scale);
        var gap = (int)Math.Round(6 * scale);
        var statusArea = taskbar.HasTray ? 0 : (int)Math.Round(SecondaryStatusAreaWidth * scale);
        var left = taskbar.HasTray ? taskbar.Left - width - gap : taskbar.Right - width - gap - statusArea;
        var top = taskbar.Top + Math.Max(0, (taskbar.Bottom - taskbar.Top - height) / 2);
        return new OverlayBounds(left, top, width, height);
    }
}

public interface IQuotaSource : IAsyncDisposable
{
    event EventHandler<QuotaSnapshot>? SnapshotChanged;

    Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

public interface ITaskbarLocator
{
    IReadOnlyList<TaskbarPlacement> LocateAll();
}
