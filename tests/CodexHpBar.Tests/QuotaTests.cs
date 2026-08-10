using CodexHpBar.Core;

namespace CodexHpBar.Tests;

public sealed class QuotaTests
{
    [Theory]
    [InlineData(96, 150, 38)]
    [InlineData(120, 188, 48)]
    [InlineData(144, 225, 57)]
    public void TaskbarGeometryScalesForDpi(int dpi, int expectedWidth, int expectedHeight)
    {
        var bounds = TaskbarGeometry.Calculate(new TaskbarPlacement(0, 1700, 1040, 1920, 1080, dpi, true));
        Assert.Equal(expectedWidth, bounds.Width);
        Assert.Equal(expectedHeight, bounds.Height);
        Assert.Equal(1700 - expectedWidth - (int)Math.Round(6 * dpi / 96d), bounds.Left);
    }

    [Theory]
    [InlineData(96, 1644)]
    [InlineData(120, 1574)]
    [InlineData(144, 1506)]
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
    public void Settings_NormalizeMakesStartupDependOnBackground()
    {
        var normalized = new AppSettings(false, true).Normalize();
        Assert.True(normalized.BackgroundMode);
        Assert.True(normalized.StartWithWindows);
        Assert.Equal(MascotAssetMode.BuiltInMushroom, normalized.Mascot!.Mode);
    }

    [Fact]
    public void MascotSettings_NormalizeClearsPathForBuiltInAsset()
    {
        var normalized = new MascotSettings(MascotAssetMode.BuiltInMushroom, "C:\\temp\\ignored.png", 8).Normalize();
        Assert.Null(normalized.FilePath);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(8, 8)]
    [InlineData(60, 30)]
    public void MascotSettings_NormalizeClampsSpriteSheetFrameRate(int input, int expected)
    {
        var normalized = new MascotSettings(MascotAssetMode.SpriteSheet4x4, "C:\\temp\\frames.png", input).Normalize();
        Assert.Equal(expected, normalized.FramesPerSecond);
    }

    [Fact]
    public void MascotAssetStorage_CopiesSelectedFileWithoutRemovingOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CodexHpBarTests-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(root, "source");
        var executionDirectory = Path.Combine(root, "application");
        var sourcePath = Path.Combine(sourceDirectory, "frames.png");

        try
        {
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);

            var settings = new MascotSettings(MascotAssetMode.StaticImage, sourcePath, 8);
            var copied = MascotAssetStorage.EnsureLocalCopy(settings, executionDirectory);

            Assert.NotEqual(Path.GetFullPath(sourcePath), copied.FilePath);
            Assert.StartsWith(
                Path.Combine(Path.GetFullPath(executionDirectory), MascotAssetStorage.DirectoryName) + Path.DirectorySeparatorChar,
                copied.FilePath!,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(copied.FilePath!));
            Assert.True(File.Exists(sourcePath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
