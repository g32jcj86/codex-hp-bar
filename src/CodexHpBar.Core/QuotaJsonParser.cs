using System.Text.Json;

namespace CodexHpBar.Core;

public static class QuotaJsonParser
{
    public static bool TryParseResponse(string json, out QuotaSnapshot snapshot)
    {
        snapshot = QuotaSnapshot.Offline("無法解析額度資料");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement rateLimits;

            if (root.TryGetProperty("result", out var result) && result.TryGetProperty("rateLimits", out var resultLimits))
            {
                rateLimits = resultLimits;
            }
            else if (root.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("rateLimits", out var updateLimits))
            {
                rateLimits = updateLimits;
            }
            else
            {
                return false;
            }

            snapshot = new QuotaSnapshot(
                ParseWindow(rateLimits, "primary"),
                ParseWindow(rateLimits, "secondary"),
                ReadString(rateLimits, "rateLimitReachedType"),
                DateTimeOffset.UtcNow);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static QuotaSnapshot Merge(QuotaSnapshot current, QuotaSnapshot update)
    {
        return new QuotaSnapshot(
            update.Primary ?? current.Primary,
            update.Secondary ?? current.Secondary,
            update.RateLimitReachedType ?? current.RateLimitReachedType,
            update.UpdatedAt,
            false,
            null);
    }

    private static RateLimitWindow? ParseWindow(JsonElement limits, string name)
    {
        if (!limits.TryGetProperty(name, out var window) || window.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (!TryReadDouble(window, "usedPercent", out var usedPercent) ||
            !TryReadInt(window, "windowDurationMins", out var duration) ||
            !TryReadLong(window, "resetsAt", out var resetsAt))
        {
            return null;
        }

        return new RateLimitWindow(usedPercent, duration, resetsAt);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryReadDouble(JsonElement element, string name, out double value)
    {
        value = default;
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out value);
    }

    private static bool TryReadInt(JsonElement element, string name, out int value)
    {
        value = default;
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out value);
    }

    private static bool TryReadLong(JsonElement element, string name, out long value)
    {
        value = default;
        return element.TryGetProperty(name, out var property) && property.TryGetInt64(out value);
    }
}
