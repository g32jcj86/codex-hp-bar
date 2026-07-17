using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class QuotaWidget : FrameworkElement
{
    private static readonly Brush Coral = FrozenBrush(255, 77, 103);
    private static readonly Brush Berry = FrozenBrush(217, 70, 239);
    private static readonly Brush Track = FrozenBrush(21, 22, 28);
    private static readonly Brush FrameBrush = FrozenBrush(218, 222, 232);
    private static readonly Brush Cyan = FrozenBrush(73, 210, 204);
    private static readonly Pen Frame = FrozenPen(FrameBrush, 1);
    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['0'] = ["111", "101", "101", "101", "111"],
        ['1'] = ["010", "110", "010", "010", "111"],
        ['2'] = ["111", "001", "111", "100", "111"],
        ['3'] = ["111", "001", "111", "001", "111"],
        ['4'] = ["101", "101", "111", "001", "001"],
        ['5'] = ["111", "100", "111", "001", "111"],
        ['6'] = ["111", "100", "111", "101", "111"],
        ['7'] = ["111", "001", "010", "010", "010"],
        ['8'] = ["111", "101", "111", "101", "111"],
        ['9'] = ["111", "101", "111", "001", "111"],
        ['%'] = ["101", "001", "010", "100", "101"],
        ['-'] = ["000", "000", "111", "000", "000"]
    };
    private readonly DispatcherTimer _animationTimer;
    private readonly DateTimeOffset _animationStart = DateTimeOffset.UtcNow;
    private static readonly ImageSource? Mascot = LoadMascot();
    private QuotaSnapshot _snapshot = QuotaSnapshot.Offline();

    public QuotaWidget()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            if (SystemParameters.ClientAreaAnimation) InvalidateVisual();
        }, Dispatcher);
        _animationTimer.Start();
    }

    public QuotaSnapshot Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        dc.PushTransform(new ScaleTransform(1 / scale, 1 / scale));
        var width = ActualWidth * scale;
        var height = ActualHeight * scale;
        var bob = SystemParameters.ClientAreaAnimation
            ? Math.Round(Math.Sin((DateTimeOffset.UtcNow - _animationStart).TotalSeconds * Math.PI * 2 / 2.8))
            : 0;

        if (Mascot is not null)
        {
            dc.DrawImage(Mascot, new Rect(2, 3 + bob, 30, 30));
        }
        else
        {
            DrawMascot(dc, 2, 4 + bob);
        }
        var windows = Snapshot.OrderedWindows;
        if (windows.Count == 0)
        {
            DrawBar(dc, new Rect(35, 9, Math.Max(20, width - 38), 20), null, Brushes.Gray, "--%");
        }
        else if (windows.Count == 1)
        {
            DrawBar(dc, new Rect(35, 9, Math.Max(20, width - 38), 20), windows[0], Berry, null);
        }
        else
        {
            DrawBar(dc, new Rect(35, 5, Math.Max(20, width - 38), 12), windows[0], Coral, null);
            DrawBar(dc, new Rect(35, 21, Math.Max(20, width - 38), 12), windows[1], Berry, null);
        }

        if (Snapshot.IsStale)
        {
            dc.DrawEllipse(Brushes.Gold, null, new Point(width - 5, 5), 2, 2);
        }

        if (windows.Count > 0 && windows.Min(window => window.RemainingPercent) <= 20)
        {
            dc.DrawEllipse(Cyan, null, new Point(31, 7), 2, 3);
        }

        dc.Pop();
    }

    private static void DrawBar(DrawingContext dc, Rect rect, RateLimitWindow? window, Brush fill, string? fallback)
    {
        dc.DrawRoundedRectangle(Track, Frame, rect, rect.Height / 2, rect.Height / 2);

        var percent = window?.RemainingPercent ?? 0;
        var inner = new Rect(rect.X + 2, rect.Y + 2, Math.Max(0, (rect.Width - 4) * percent / 100d), rect.Height - 4);
        if (inner.Width > 0)
        {
            dc.DrawRoundedRectangle(percent <= 20 ? Brushes.OrangeRed : fill, null, inner, inner.Height / 2, inner.Height / 2);
            if (SystemParameters.ClientAreaAnimation && inner.Width > 8)
            {
                var progress = ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 3500) / 3500d);
                var glowX = inner.X + 2 + progress * Math.Max(1, inner.Width - 4);
                dc.DrawRectangle(Brushes.White, null, new Rect(glowX, inner.Y + 1, 1, Math.Max(1, inner.Height - 2)));
            }
        }

        var text = fallback ?? $"{percent}%";
        DrawPixelText(dc, text, rect);
    }

    private static void DrawPixelText(DrawingContext dc, string text, Rect rect)
    {
        var pixel = rect.Height >= 18 ? 2d : 1d;
        var glyphWidth = 3 * pixel;
        var spacing = pixel;
        var totalWidth = text.Length * glyphWidth + Math.Max(0, text.Length - 1) * spacing;
        var x = Math.Round(rect.X + (rect.Width - totalWidth) / 2);
        var y = Math.Round(rect.Y + (rect.Height - 5 * pixel) / 2);
        foreach (var character in text)
        {
            if (Glyphs.TryGetValue(character, out var glyph))
            {
                for (var row = 0; row < 5; row++)
                    for (var column = 0; column < 3; column++)
                    {
                        if (glyph[row][column] == '1') dc.DrawRectangle(Brushes.White, null, new Rect(x + column * pixel, y + row * pixel, pixel, pixel));
                    }
            }
            x += glyphWidth + spacing;
        }
    }

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static void DrawMascot(DrawingContext dc, double x, double y)
    {
        var outline = new SolidColorBrush(Color.FromRgb(45, 35, 55));
        var pink = new SolidColorBrush(Color.FromRgb(255, 158, 181));
        var cream = new SolidColorBrush(Color.FromRgb(255, 230, 205));
        var cyan = new SolidColorBrush(Color.FromRgb(73, 210, 204));
        const double p = 3;
        void Pixel(Brush brush, int px, int py, int w = 1, int h = 1) => dc.DrawRectangle(brush, null, new Rect(x + px * p, y + py * p, w * p, h * p));

        Pixel(outline, 2, 0, 2, 2); Pixel(outline, 7, 0, 2, 2);
        Pixel(pink, 3, 1, 5, 1); Pixel(outline, 1, 2, 9, 6);
        Pixel(pink, 2, 2, 7, 5); Pixel(cream, 3, 5, 5, 2);
        Pixel(outline, 3, 3); Pixel(outline, 7, 3);
        Pixel(pink, 4, 5, 3, 1); Pixel(outline, 4, 5); Pixel(outline, 6, 5);
        Pixel(cyan, 2, 7, 7, 1); Pixel(outline, 9, 6, 2, 1); Pixel(pink, 10, 5);
    }

    private static ImageSource? LoadMascot()
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/Assets/cat-pig.png", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}

internal static class TooltipBuilder
{
    public static string Build(QuotaSnapshot snapshot)
    {
        if (snapshot.OrderedWindows.Count == 0)
        {
            return snapshot.Error ?? "目前無法取得 Codex 額度";
        }

        return string.Join(Environment.NewLine, snapshot.OrderedWindows.Select(window =>
            $"{FormatWindow(window.WindowDurationMins)}：剩餘 {window.RemainingPercent}%（{window.ResetTime.ToLocalTime():MM/dd HH:mm} 重置）"))
            + $"{Environment.NewLine}更新：{snapshot.UpdatedAt.ToLocalTime():HH:mm:ss}";
    }

    private static string FormatWindow(int minutes) => minutes switch
    {
        <= 360 => "短期額度",
        >= 10000 => "每週額度",
        _ => $"{minutes} 分鐘額度"
    };
}
