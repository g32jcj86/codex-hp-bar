using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexHpBar.Core;

namespace CodexHpBar;

public sealed class QuotaWidget : FrameworkElement
{
    private static readonly Brush Weekly = FrozenBrush(16, 163, 127);
    private static readonly Brush ShortTerm = FrozenBrush(217, 119, 87);
    private static readonly Brush Track = FrozenBrush(21, 22, 28);
    private static readonly Brush Cyan = FrozenBrush(73, 210, 204);
    private readonly DispatcherTimer _animationTimer;
    private readonly DateTimeOffset _animationStart = DateTimeOffset.UtcNow;
    private readonly MascotAnimation _mascotAnimation = new();
    private QuotaSnapshot _snapshot = QuotaSnapshot.Offline();

    public QuotaWidget()
    {
        // The character art is clean HD rather than pixel art. HighQuality
        // keeps the thick outline readable when the shared sprite canvas is
        // reduced to the compact taskbar slot.
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                _mascotAnimation.Advance(DateTimeOffset.UtcNow);
                InvalidateVisual();
            }

            UpdateAnimationTimer();
        }, Dispatcher);
        UpdateAnimationTimer();
        _animationTimer.Start();
    }

    public void SetMascot(MascotSettings settings)
    {
        _mascotAnimation.Apply(settings);
        UpdateAnimationTimer();
        InvalidateVisual();
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
        var windows = Snapshot.OrderedWindows;
        var center = new Point(19 * scale, height / 2);
        var thickness = (windows.Count == 2 ? 3 : 4) * scale;
        var outerRadius = 17 * scale - thickness / 2;
        if (windows.Count == 0)
        {
            dc.DrawEllipse(null, new Pen(Brushes.Gray, thickness), center, outerRadius, outerRadius);
        }
        else
        {
            DrawRing(dc, center, outerRadius, thickness, windows[^1].RemainingPercent, Weekly);
        }
        if (windows.Count == 2)
        {
            DrawRing(dc, center, outerRadius - 4 * scale, thickness,
                windows[0].RemainingPercent, ShortTerm);
        }

        var centerRadius = (windows.Count == 2 ? 9 : 12) * scale;
        dc.PushClip(new EllipseGeometry(center, centerRadius, centerRadius));
        var bob = _mascotAnimation.AllowsIdleBob && SystemParameters.ClientAreaAnimation
            ? Math.Sin((DateTimeOffset.UtcNow - _animationStart).TotalSeconds * Math.PI * 2 / 2.8) * scale * 0.5
            : 0;
        var mascotSize = centerRadius * 1.3;
        var frameOffset = _mascotAnimation.AllowsIdleBob
            ? _mascotAnimation.CurrentVerticalOffsetRatio * mascotSize : 0;
        var frameHorizontalOffset = _mascotAnimation.AllowsIdleBob
            ? _mascotAnimation.CurrentHorizontalOffsetRatio * mascotSize : 0;
        var slot = new Rect(center.X - mascotSize / 2 + frameHorizontalOffset,
            center.Y - mascotSize / 2 + bob + frameOffset, mascotSize, mascotSize);
        if (_mascotAnimation.CurrentImage is { } mascot)
        {
            DrawImageUniform(dc, mascot, slot);
        }
        else
        {
            dc.PushTransform(new TranslateTransform(slot.X, slot.Y));
            dc.PushTransform(new ScaleTransform(mascotSize / 33, mascotSize / 30));
            DrawFallbackMushroom(dc, 0, 0);
            dc.Pop();
            dc.Pop();
        }
        dc.Pop();

        var textRect = new Rect(40 * scale, (height - 32 * scale) / 2,
            Math.Max(0, width - 43 * scale), 32 * scale);
        dc.DrawRoundedRectangle(Track, null, textRect, 4 * scale, 4 * scale);
        if (windows.Count == 0)
        {
            DrawQuotaText(dc, "--%", Brushes.Gray, textRect, scale);
        }
        else if (windows.Count == 1)
        {
            DrawQuotaText(dc, $"週 {windows[0].RemainingPercent}%", Weekly, textRect, scale);
        }
        else
        {
            var row = new Rect(textRect.X, textRect.Y, textRect.Width, textRect.Height / 2);
            DrawQuotaText(dc, $"週 {windows[^1].RemainingPercent}%", Weekly, row, scale);
            row.Y += row.Height;
            DrawQuotaText(dc, $"5h {windows[0].RemainingPercent}%", ShortTerm, row, scale);
        }

        if (Snapshot.IsStale)
        {
            dc.DrawEllipse(Brushes.Gold, null, new Point(width - 5 * scale, 5 * scale), 2 * scale, 2 * scale);
        }
        if (windows.Count > 0 && windows.Min(window => window.RemainingPercent) <= 20)
        {
            dc.DrawEllipse(Cyan, null, new Point(33 * scale, center.Y - 13 * scale), 2 * scale, 3 * scale);
        }
        dc.Pop();
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

    private static void DrawQuotaText(DrawingContext dc, string text, Brush brush, Rect row, double scale)
    {
        // The inverse DPI transform makes one drawing unit one device pixel.
        // Scale the font size, with PixelsPerDip = 1 to avoid scaling it twice.
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                FontWeights.SemiBold, FontStretches.Normal), 11 * scale, brush, 1);
        dc.DrawText(formatted, new Point(row.X + 6 * scale, row.Y + (row.Height - formatted.Height) / 2));
    }

    private static void DrawImageUniform(DrawingContext dc, ImageSource image, Rect slot)
    {
        var sourceWidth = image is BitmapSource bitmap ? bitmap.PixelWidth : image.Width;
        var sourceHeight = image is BitmapSource bitmapSource ? bitmapSource.PixelHeight : image.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0) return;

        var scale = Math.Min(slot.Width / sourceWidth, slot.Height / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var destination = new Rect(
            slot.X + (slot.Width - width) / 2,
            slot.Y + (slot.Height - height) / 2,
            width,
            height);
        dc.DrawImage(image, destination);
    }

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static void DrawFallbackMushroom(DrawingContext dc, double x, double y)
    {
        var outline = new SolidColorBrush(Color.FromRgb(78, 52, 30));
        var orange = new SolidColorBrush(Color.FromRgb(244, 133, 24));
        var cream = new SolidColorBrush(Color.FromRgb(255, 246, 221));
        var brown = new SolidColorBrush(Color.FromRgb(151, 98, 55));
        const double p = 3;
        void Pixel(Brush brush, int px, int py, int w = 1, int h = 1) => dc.DrawRectangle(brush, null, new Rect(x + px * p, y + py * p, w * p, h * p));

        Pixel(outline, 1, 2, 9, 4); Pixel(orange, 2, 1, 7, 4);
        Pixel(orange, 3, 0, 5, 1); Pixel(cream, 2, 5, 7, 5);
        Pixel(outline, 2, 4, 7, 1); Pixel(outline, 3, 6); Pixel(outline, 7, 6);
        Pixel(cream, 3, 7, 5, 2); Pixel(outline, 4, 8); Pixel(outline, 6, 8);
        Pixel(brown, 1, 3); Pixel(brown, 8, 2); Pixel(brown, 5, 1);
    }

    private void UpdateAnimationTimer()
    {
        var minimum = TimeSpan.FromMilliseconds(20);
        _animationTimer.Interval = _mascotAnimation.NextFrameDelay < minimum
            ? minimum
            : _mascotAnimation.NextFrameDelay;
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
