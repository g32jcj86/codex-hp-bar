using System.Text.Json;

namespace CodexHpBar.Core;

public static class ClaudeUsageParser
{
    public static QuotaSnapshot Parse(string json, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                (!root.TryGetProperty("five_hour", out _) && !root.TryGetProperty("seven_day", out _)))
                return QuotaSnapshot.Offline("無法解析 Claude Code 額度資料");

            RateLimitWindow? ReadWindow(string name, int duration)
            {
                if (!root.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object ||
                    !window.TryGetProperty("utilization", out var used) || used.ValueKind != JsonValueKind.Number ||
                    !used.TryGetDouble(out var percent) || !double.IsFinite(percent) ||
                    !window.TryGetProperty("resets_at", out var reset) || reset.ValueKind != JsonValueKind.String ||
                    !reset.TryGetDateTimeOffset(out var resetTime) || resetTime <= now) return null;
                return new RateLimitWindow(percent, duration, resetTime.ToUnixTimeSeconds());
            }

            return new QuotaSnapshot(ReadWindow("five_hour", 300), ReadWindow("seven_day", 10080), null, now);
        }
        catch (JsonException)
        {
            return QuotaSnapshot.Offline("無法解析 Claude Code 額度資料");
        }
    }
}