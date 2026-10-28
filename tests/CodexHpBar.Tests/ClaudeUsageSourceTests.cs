using System.Net;
using CodexHpBar;

namespace CodexHpBar.Tests;

public sealed class ClaudeUsageSourceTests
{
    [Theory]
    [InlineData(false, "找不到 Claude Code 登入資料")]
    [InlineData(true, "Claude Code 權杖已過期，開啟 Claude Code 後會自動更新")]
    public async Task MissingOrExpiredCredentialsDoNotSendRequests(bool expired, string expectedError)
    {
        var path = Path.Combine(Path.GetTempPath(), $"claude-test-{Guid.NewGuid():N}.json");
        try
        {
            if (expired) await File.WriteAllTextAsync(path, """
                {"claudeAiOauth":{"accessToken":"test-token","expiresAt":1600000000000}}
                """);
            using var handler = new UsageHandler(HttpStatusCode.OK);
            using var client = new HttpClient(handler);
            var snapshot = await new ClaudeUsageSource(client, path).ReadAsync(DateTimeOffset.FromUnixTimeSeconds(1700000000));
            Assert.Equal(expectedError, snapshot.Error);
            Assert.Empty(snapshot.OrderedWindows);
            Assert.Equal(0, handler.Calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task ThrottlesRequestsAndHandlesFailure(HttpStatusCode failure, bool stale)
    {
        var path = Path.Combine(Path.GetTempPath(), $"claude-test-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                {"claudeAiOauth":{"accessToken":"test-token","expiresAt":1800000000000}}
                """);
            using var handler = new UsageHandler(failure);
            using var client = new HttpClient(handler);
            var source = new ClaudeUsageSource(client, path);
            var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
            var first = await source.ReadAsync(now);
            Assert.Equal(94, first.Primary!.RemainingPercent);
            Assert.Same(first, await source.ReadAsync(now.AddSeconds(59)));
            Assert.Equal(1, handler.Calls);
            var failed = await source.ReadAsync(now.AddSeconds(60));
            Assert.Equal(2, handler.Calls);
            Assert.True(failed.IsStale);
            Assert.Equal(stale ? 1 : 0, failed.OrderedWindows.Count);
            Assert.DoesNotContain("test-token", failed.Error!);
            if (!stale) Assert.Equal("Claude Code 登入已失效", failed.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class UsageHandler(HttpStatusCode failure) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.anthropic.com/api/oauth/usage", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            Assert.Equal("oauth-2025-04-20", Assert.Single(request.Headers.GetValues("anthropic-beta")));
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Calls == 1 ? HttpStatusCode.OK : failure)
            {
                Content = new StringContent(Calls == 1
                    ? "{\"five_hour\":{\"utilization\":6,\"resets_at\":\"2027-01-15T08:00:00Z\"}}"
                    : "test-token must never appear in errors")
            });
        }
    }
}
