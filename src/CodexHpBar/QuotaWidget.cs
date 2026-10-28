using System.Windows;
using System.Windows.Media;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class QuotaWidget : FrameworkElement
{
    private static readonly Brush Weekly = FrozenBrush(16, 163, 127);
    private static readonly Brush ShortTerm = FrozenBrush(125, 211, 184);
    private static readonly Brush ClaudeWeekly = FrozenBrush(217, 119, 87);
    private static readonly Brush ClaudeShortTerm = FrozenBrush(242, 184, 160);
    private static readonly Brush Track = FrozenBrush(21, 22, 28);
    private static readonly Brush Cyan = FrozenBrush(73, 210, 204);
    private QuotaSnapshot _snapshot = QuotaSnapshot.Offline();
    private QuotaSnapshot _claudeSnapshot = QuotaSnapshot.Offline();

    public QuotaSnapshot Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            InvalidateVisual();
        }
    }

    public QuotaSnapshot ClaudeSnapshot
    {
        get => _claudeSnapshot;
        set { _claudeSnapshot = value; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        dc.PushTransform(new ScaleTransform(1 / scale, 1 / scale));
        var height = ActualHeight * scale;
        DrawGauge(dc, new Point(19 * scale, height / 2), ClaudeSnapshot, ClaudeWeekly, ClaudeShortTerm, scale);
        DrawGauge(dc, new Point(59 * scale, height / 2), Snapshot, Weekly, ShortTerm, scale);
        dc.Pop();
    }

    private static void DrawGauge(DrawingContext dc, Point center, QuotaSnapshot snapshot, Brush weekly, Brush shortTerm, double scale)
    {
        var windows = snapshot.OrderedWindows;
        var radius = 16.5 * scale;
        var thickness = 3 * scale;
        if (windows.Count == 0)
            dc.DrawEllipse(null, new Pen(Brushes.Gray, thickness), center, radius, radius);
        else
            DrawRing(dc, center, radius, thickness, windows[^1].RemainingPercent, weekly);
        if (windows.Count == 2)
            DrawRing(dc, center, radius - 4 * scale, thickness, windows[0].RemainingPercent, shortTerm);

        var centerRadius = (windows.Count == 2 ? 10 : 14) * scale;
        dc.DrawEllipse(Track, null, center, centerRadius, centerRadius);
        var row = new Rect(center.X - centerRadius, center.Y - centerRadius, centerRadius * 2, centerRadius * 2);
        if (windows.Count < 2)
            DrawQuotaText(dc, windows.Count == 0 ? "--" : windows[0].RemainingPercent.ToString(),
                windows.Count == 0 ? Brushes.Gray : weekly, row, 11, scale);
        else
        {
            row.Height /= 2;
            DrawQuotaText(dc, windows[^1].RemainingPercent.ToString(), weekly, row, 8, scale);
            row.Y += row.Height;
            DrawQuotaText(dc, windows[0].RemainingPercent.ToString(), shortTerm, row, 8, scale);
        }
        if (snapshot.IsStale)
            dc.DrawEllipse(Brushes.Gold, null, new Point(center.X + 14 * scale, center.Y - 14 * scale), 2 * scale, 2 * scale);
        if (windows.Count > 0 && windows.Min(window => window.RemainingPercent) <= 20)
            dc.DrawEllipse(Cyan, null, new Point(center.X + 14 * scale, center.Y - 9 * scale), 2 * scale, 2 * scale);
    }
    private static void DrawRing(DrawingContext dc, Point center, double radius, double thickness, int percent, Brush fill)
    {
        dc.DrawEllipse(null, new Pen(Track, thickness), center, radius, radius);
        if (percent == 0) return;
        var pen = new Pen(fill, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (percent == 100)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }
        var angle = percent / 100d * Math.PI * 2;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            context.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius),
                new Size(radius, radius), 0, percent > 50, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static void DrawQuotaText(DrawingContext dc, string text, Brush brush, Rect row, double fontSize, double scale)
    {
        // The inverse DPI transform makes one drawing unit one device pixel.
        // Scale the font size, with PixelsPerDip = 1 to avoid scaling it twice.
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                FontWeights.Bold, FontStretches.Normal), fontSize * scale, brush, 1);
        dc.DrawText(formatted, new Point(row.X + (row.Width - formatted.Width) / 2, row.Y + (row.Height - formatted.Height) / 2));
    }

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

}

internal static class TooltipBuilder
{
    public static string Build(QuotaSnapshot snapshot)
    {
        if (snapshot.OrderedWindows.Count == 0)
        {
            return snapshot.Error ?? "目前無法取得額度";
        }

        return string.Join(Environment.NewLine, snapshot.OrderedWindows.Select(window =>
            $"{FormatWindow(window.WindowDurationMins)}：剩餘 {window.RemainingPercent}%（{window.ResetTime.ToLocalTime():MM/dd HH:mm} 重置）"))
            + $"{Environment.NewLine}更新：{snapshot.UpdatedAt.ToLocalTime():HH:mm:ss}";
    }

    private static string FormatWindow(int minutes) => minutes switch
    {
        <= 360 => "5 小時額度",
        >= 10000 => "每週額度",
        _ => $"{minutes} 分鐘額度"
    };
}
