using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class ClaudeUsageSource
{
    private static readonly HttpClient SharedClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private readonly HttpClient _client;
    private readonly string _credentialsPath;
    private DateTimeOffset _nextRead;
    private DateTimeOffset _nextRefresh;
    private QuotaSnapshot _snapshot = QuotaSnapshot.Offline("尚未取得 Claude Code 額度");

    public ClaudeUsageSource(HttpClient? client = null, string? credentialsPath = null)
    {
        _client = client ?? SharedClient;
        var configDirectory = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        _credentialsPath = credentialsPath ?? Path.Combine(
            string.IsNullOrWhiteSpace(configDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
                : configDirectory, ".credentials.json");
    }

    public async Task<QuotaSnapshot> ReadAsync(DateTimeOffset now)
    {
        if (now < _nextRead) return _snapshot;
        _nextRead = now.AddSeconds(60);
        try
        {
            if (!File.Exists(_credentialsPath))
                return _snapshot = QuotaSnapshot.Offline("找不到 Claude Code 登入資料");

            var credentials = ClaudeCredentials.Parse(await File.ReadAllTextAsync(_credentialsPath), now, out var error);
            if (credentials is null && error == ClaudeCredentials.ExpiredError && now >= _nextRefresh)
            {
                _nextRefresh = now.AddMinutes(10);
                await RefreshWithClaudeCliAsync();
                credentials = ClaudeCredentials.Parse(await File.ReadAllTextAsync(_credentialsPath), now, out error);
            }
            if (credentials is null) return _snapshot = QuotaSnapshot.Offline(error);

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            using var response = await _client.SendAsync(request);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return _snapshot = QuotaSnapshot.Offline("Claude Code 登入已失效");

            if (response.IsSuccessStatusCode)
            {
                var parsed = ClaudeUsageParser.Parse(await response.Content.ReadAsStringAsync(), now);
                if (parsed.Error is null) return _snapshot = parsed;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or HttpRequestException or OperationCanceledException or FormatException or Win32Exception)
        {
            // Keep all request/credential details out of user-visible errors.
        }

        return _snapshot = _snapshot.OrderedWindows.Count > 0
            ? _snapshot with { IsStale = true, Error = "無法更新 Claude Code 額度" }
            : QuotaSnapshot.Offline("無法更新 Claude Code 額度");
    }

    // Let Claude Code refresh its own login: using the refresh token here would rotate it away from Claude Code.
    private static async Task RefreshWithClaudeCliAsync()
    {
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        using var process = Process.Start(new ProcessStartInfo(File.Exists(installed) ? installed : "claude", "auth status")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (process is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
        }
    }
}
