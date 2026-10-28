using CodexHpBar.Core;

namespace CodexHpBar.Tests;

public sealed class ClaudeCredentialsTests
{
    [Fact]
    public void ReadsAccessTokenAndIgnoresRefreshToken()
    {
        const string json = """
            {"claudeAiOauth":{"accessToken":"test-token","expiresAt":1800000000000,"refreshToken":"unused"}}
            """;
        var credentials = ClaudeCredentials.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1700000000), out var error);
        Assert.Null(error);
        Assert.Equal("test-token", credentials!.AccessToken);
        Assert.DoesNotContain("test-token", credentials.ToString());
    }

    [Theory]
    [InlineData(1700000000000)]
    [InlineData(1600000000000)]
    public void RejectsExpiredToken(long expiry)
    {
        var json = $"{{\"claudeAiOauth\":{{\"accessToken\":\"test-token\",\"expiresAt\":{expiry}}}}}";
        Assert.Null(ClaudeCredentials.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1700000000), out var error));
        Assert.Equal("Claude Code 權杖已過期，開啟 Claude Code 後會自動更新", error);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"claudeAiOauth\":null}")]
    [InlineData("{\"claudeAiOauth\":{\"accessToken\":\"test-token\",\"expiresAt\":\"bad\"}}")]
    public void InvalidCredentialsDoNotExposeInput(string json)
    {
        Assert.Null(ClaudeCredentials.Parse(json, DateTimeOffset.UtcNow, out var error));
        Assert.Equal("無法解析 Claude Code 登入資料", error);
    }
}
