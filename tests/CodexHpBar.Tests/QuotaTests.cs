using CodexHpBar.Core;

namespace CodexHpBar.Tests;

public sealed class QuotaTests
{
    [Theory]
    [InlineData(true, "2027-01-15T08:00:00+00:00", 2)]
    [InlineData(false, "2027-01-15T08:00:00+00:00", 1)]
    [InlineData(true, "2020-09-13T12:00:00+00:00", 1)]
    public void ClaudeParser_ReadsUnexpiredWindows(bool includeShort, string reset, int count)
    {
        var shortWindow = includeShort ? $"\"five_hour\":{{\"utilization\":23.5,\"resets_at\":\"{reset}\"}}," : "";
        var json = "{" + shortWindow + "\"seven_day\":{\"utilization\":41.2,\"resets_at\":\"2027-01-15T08:00:01+00:00\"}}";
        var snapshot = ClaudeUsageParser.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1700000000));
        Assert.Equal(count, snapshot.OrderedWindows.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), snapshot.UpdatedAt);
        Assert.Equal(10080, snapshot.OrderedWindows[^1].WindowDurationMins);
        Assert.Equal(59, snapshot.OrderedWindows[^1].RemainingPercent);
        if (count == 2)
        {
            Assert.Equal(300, snapshot.Primary!.WindowDurationMins);
            Assert.Equal(77, snapshot.Primary.RemainingPercent);
        }
    }

    [Theory]
    [InlineData("{\"five_hour\":null,\"seven_day\":{\"utilization\":6,\"resets_at\":\"2027-01-15T08:00:00Z\"}}", 1)]
    [InlineData("{\"five_hour\":null,\"seven_day\":null}", 0)]
    public void ClaudeParser_AllowsNullWindows(string json, int count)
    {
        Assert.Equal(count, ClaudeUsageParser.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1700000000)).OrderedWindows.Count);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void ClaudeParser_InvalidJsonIsOffline(string json)
    {
        var snapshot = ClaudeUsageParser.Parse(json, DateTimeOffset.UtcNow);
        Assert.Empty(snapshot.OrderedWindows);
        Assert.True(snapshot.IsStale);
        Assert.NotNull(snapshot.Error);
    }

    [Theory]
    [InlineData(96, 80, 38)]
    [InlineData(120, 100, 48)]
    [InlineData(144, 120, 57)]
    public void TaskbarGeometryScalesForDpi(int dpi, int expectedWidth, int expectedHeight)
    {
        var bounds = TaskbarGeometry.Calculate(new TaskbarPlacement(0, 1700, 1040, 1920, 1080, dpi, true));
        Assert.Equal(expectedWidth, bounds.Width);
        Assert.Equal(expectedHeight, bounds.Height);
        Assert.Equal(1700 - expectedWidth - (int)Math.Round(6 * dpi / 96d), bounds.Left);
    }

    [Theory]
    [InlineData(96, 1714)]
    [InlineData(120, 1662)]
    [InlineData(144, 1611)]
    public void SecondaryTaskbarReservesClockAndStatusArea(int dpi, int expectedLeft)
    {
        var bounds = TaskbarGeometry.Calculate(new TaskbarPlacement(0, 0, 1040, 1920, 1080, dpi, false));
        Assert.Equal(expectedLeft, bounds.Left);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(17, 83)]
    [InlineData(99.6, 0)]
    [InlineData(-4, 100)]
    [InlineData(120, 0)]
    public void RemainingPercent_IsRoundedAndClamped(double used, int expected)
    {
        var window = new RateLimitWindow(used, 300, 1_800_000_000);
        Assert.Equal(expected, window.RemainingPercent);
    }

    [Fact]
    public void Parser_ReadsResponseAndOrdersShortWindowFirst()
    {
        const string json = """
            {"id":7,"result":{"rateLimits":{
              "primary":{"usedPercent":20,"windowDurationMins":10080,"resetsAt":1800000000},
              "secondary":{"usedPercent":5,"windowDurationMins":300,"resetsAt":1700000000},
              "unknownField":"ignored"
            }}}
            """;

        Assert.True(QuotaJsonParser.TryParseResponse(json, out var snapshot));
        Assert.Equal(300, snapshot.OrderedWindows[0].WindowDurationMins);
        Assert.Equal(95, snapshot.OrderedWindows[0].RemainingPercent);
        Assert.Equal(80, snapshot.OrderedWindows[1].RemainingPercent);
    }

    [Fact]
    public void Parser_AcceptsSingleWindowNotification()
    {
        const string json = """
            {"method":"account/rateLimits/updated","params":{"rateLimits":{
              "primary":{"usedPercent":33,"windowDurationMins":10080,"resetsAt":1800000000}
            }}}
            """;

        Assert.True(QuotaJsonParser.TryParseResponse(json, out var snapshot));
        Assert.NotNull(snapshot.Primary);
        Assert.Null(snapshot.Secondary);
        Assert.Equal(67, snapshot.Primary!.RemainingPercent);
    }

    [Fact]
    public void Merge_PreservesMissingSparseFields()
    {
        var current = new QuotaSnapshot(
            new RateLimitWindow(10, 300, 1_800_000_000),
            new RateLimitWindow(20, 10080, 1_800_000_001),
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        var update = new QuotaSnapshot(
            new RateLimitWindow(30, 300, 1_800_000_002),
            null,
            null,
            DateTimeOffset.UtcNow);

        var merged = QuotaJsonParser.Merge(current, update);
        Assert.Equal(70, merged.Primary!.RemainingPercent);
        Assert.Equal(80, merged.Secondary!.RemainingPercent);
    }

    [Fact]
    public void Settings_DefaultEnablesBackgroundAndStartup()
    {
        var settings = AppSettings.Default;
        Assert.True(settings.BackgroundMode);
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void Settings_IgnoresLegacyMascotField()
    {
        const string json = """
            {"BackgroundMode":true,"StartWithWindows":true,"Mascot":{"Mode":"AnimatedGif","FilePath":"missing.gif","FramesPerSecond":8}}
            """;
        var options = new System.Text.Json.JsonSerializerOptions();
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, options);
        Assert.Equal(AppSettings.Default, settings);
        Assert.DoesNotContain("Mascot", System.Text.Json.JsonSerializer.Serialize(settings, options));
    }

    [Fact]
    public void Settings_NormalizeMakesStartupDependOnBackground()
    {
        var normalized = new AppSettings(false, true).Normalize();
        Assert.True(normalized.BackgroundMode);
        Assert.True(normalized.StartWithWindows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"result\":{}}")]
    public void Parser_RejectsInvalidPayload(string json)
    {
        Assert.False(QuotaJsonParser.TryParseResponse(json, out _));
    }
}
