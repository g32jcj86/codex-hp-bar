using System.Text.Json;

namespace CodexHpBar.Core;

public sealed class ClaudeCredentials
{
    public string AccessToken { get; }

    private ClaudeCredentials(string accessToken) => AccessToken = accessToken;

    public static ClaudeCredentials? Parse(string json, DateTimeOffset now, out string? error)
    {
        error = "無法解析 Claude Code 登入資料";
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object ||
                !oauth.TryGetProperty("accessToken", out var token) || token.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(token.GetString()) ||
                !oauth.TryGetProperty("expiresAt", out var expires) || expires.ValueKind != JsonValueKind.Number ||
                !expires.TryGetInt64(out var milliseconds)) return null;

            if (milliseconds <= now.ToUnixTimeMilliseconds())
            {
                error = "Claude Code 權杖已過期，開啟 Claude Code 後會自動更新";
                return null;
            }

            error = null;
            return new ClaudeCredentials(token.GetString()!);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
