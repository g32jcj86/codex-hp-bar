using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexHpBar.Core;

namespace CodexHpBar;

internal sealed record MascotFrame(ImageSource Image, TimeSpan Duration, double VerticalOffsetRatio = 0);

internal sealed class MascotAnimation
{
    private readonly List<MascotFrame> _frames = [];
    private int _frameIndex;
    private DateTimeOffset _nextFrameAt;
    private MascotAssetMode _mode = MascotAssetMode.BuiltInMushroom;

    public MascotAnimation()
    {
        Apply(new MascotSettings(MascotAssetMode.BuiltInMushroom, null, 8));
    }

    public ImageSource? CurrentImage => _frames.Count == 0 ? null : _frames[_frameIndex].Image;

    public double CurrentVerticalOffsetRatio => _frames.Count == 0 ? 0 : _frames[_frameIndex].VerticalOffsetRatio;

    public bool IsAnimated => _frames.Count > 1;

    public bool AllowsIdleBob => _mode != MascotAssetMode.SpriteSheet4x4;

    public TimeSpan NextFrameDelay => IsAnimated
        ? _frames[_frameIndex].Duration
        : TimeSpan.FromMilliseconds(250);

    public string? Error { get; private set; }

    public void Apply(MascotSettings settings)
    {
        var normalized = settings.Normalize();
        _mode = normalized.Mode;
        try
        {
            ReplaceFrames(LoadFrames(normalized));
            Error = null;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            try
            {
                var fallback = new MascotSettings(MascotAssetMode.BuiltInMushroom, null, 8);
                _mode = fallback.Mode;
                ReplaceFrames(LoadFrames(fallback));
            }
            catch
            {
                _frames.Clear();
                _frameIndex = 0;
            }
        }
    }

    public bool TryValidate(MascotSettings settings, out string error)
    {
        try
        {
            _ = LoadFrames(settings.Normalize());
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool Advance(DateTimeOffset now)
    {
        if (!IsAnimated || now < _nextFrameAt) return false;

        var changed = false;
        var catchUpFrames = 64;
        while (now >= _nextFrameAt && catchUpFrames-- > 0)
        {
            _frameIndex = (_frameIndex + 1) % _frames.Count;
            _nextFrameAt += _frames[_frameIndex].Duration;
            changed = true;
        }

        if (now >= _nextFrameAt) _nextFrameAt = now + _frames[_frameIndex].Duration;

        return changed;
    }

    private IReadOnlyList<MascotFrame> LoadFrames(MascotSettings settings)
    {
        return settings.Mode switch
        {
            MascotAssetMode.BuiltInMushroom => LoadStatic(new Uri("pack://application:,,,/Assets/mushroom.png", UriKind.Absolute)),
            MascotAssetMode.StaticImage => LoadStatic(CreateExternalUri(settings, [".png", ".jpg", ".jpeg", ".bmp", ".ico"])),
            MascotAssetMode.AnimatedGif => LoadGif(CreateExternalUri(settings, [".gif"])),
            MascotAssetMode.SpriteSheet4x4 => LoadSpriteSheet(CreateExternalUri(settings, [".png", ".jpg", ".jpeg", ".bmp"]), settings.FramesPerSecond),
            _ => throw new InvalidDataException("不支援的圖片模式。")
        };
    }

    private BitmapSource LoadBitmap(Uri uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.EndInit();
        image.Freeze();
        if (image.PixelWidth > 4096 || image.PixelHeight > 4096)
        {
            throw new InvalidDataException("圖片尺寸不可超過 4096×4096 像素。");
        }

        return image;
    }

    private IReadOnlyList<MascotFrame> LoadStatic(Uri uri)
    {
        var image = LoadBitmap(uri);
        return [new MascotFrame(image, TimeSpan.FromMilliseconds(250))];
    }

    private IReadOnlyList<MascotFrame> LoadGif(Uri uri)
    {
        var decoder = new GifBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidDataException("GIF 沒有可播放的影格。");
        if (decoder.Frames.Any(frame => frame.PixelWidth > 4096 || frame.PixelHeight > 4096))
        {
            throw new InvalidDataException("圖片尺寸不可超過 4096×4096 像素。");
        }

        var frames = new List<MascotFrame>(decoder.Frames.Count);
        foreach (var frame in decoder.Frames)
        {
            var duration = ReadGifDuration(frame);
            if (frame.CanFreeze) frame.Freeze();
            frames.Add(new MascotFrame(frame, duration));
        }

        return frames;
    }

    private IReadOnlyList<MascotFrame> LoadSpriteSheet(Uri uri, int framesPerSecond)
    {
        var sheet = LoadBitmap(uri);
        if (sheet.PixelWidth < 4 || sheet.PixelHeight < 4 || sheet.PixelWidth % 4 != 0 || sheet.PixelHeight % 4 != 0)
        {
            throw new InvalidDataException("4×4 連續動畫圖片必須能平均切成 4 欄與 4 列，共 16 個影格。");
        }

        var frameWidth = sheet.PixelWidth / 4;
        var frameHeight = sheet.PixelHeight / 4;
        var duration = TimeSpan.FromSeconds(1d / Math.Clamp(framesPerSecond, 1, 30));
        var sourceFrames = new List<(BitmapSource Frame, int VisibleBottom)>(16);
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var frame = new CroppedBitmap(sheet, new Int32Rect(
                    column * frameWidth,
                    row * frameHeight,
                    frameWidth,
                    frameHeight));
                frame.Freeze();
                sourceFrames.Add((frame, FindVisibleBottom(frame)));
            }
        }

        var baseline = sourceFrames.Max(item => item.VisibleBottom);
        var frames = new List<MascotFrame>(16);
        foreach (var item in sourceFrames)
        {
            var verticalOffsetRatio = baseline >= 0 && item.VisibleBottom >= 0
                ? (baseline - item.VisibleBottom) / (double)frameHeight
                : 0;
            frames.Add(new MascotFrame(item.Frame, duration, verticalOffsetRatio));
        }

        return frames;
    }

    private static int FindVisibleBottom(BitmapSource frame)
    {
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var visibleBottom = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                if (pixels[y * stride + x * 4 + 3] >= 16) visibleBottom = y;
            }
        }

        return visibleBottom;
    }

    private TimeSpan ReadGifDuration(BitmapFrame frame)
    {
        var duration = 100d;
        if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery("/grctlext/Delay"))
        {
            var rawDelay = metadata.GetQuery("/grctlext/Delay");
            var hundredths = rawDelay switch
            {
                byte value => (ulong)value,
                ushort value => (ulong)value,
                uint value => (ulong)value,
                ulong value => Math.Min(value, uint.MaxValue),
                _ => 0UL
            };
            if (hundredths > 0) duration = Math.Clamp(hundredths * 10d, 20d, 1000d);
        }

        return TimeSpan.FromMilliseconds(duration);
    }

    private Uri CreateExternalUri(MascotSettings settings, IReadOnlyList<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(settings.FilePath))
        {
            throw new InvalidDataException("請先選擇圖片檔案。");
        }

        var path = Path.GetFullPath(settings.FilePath);
        if (!File.Exists(path)) throw new FileNotFoundException("找不到指定的圖片檔案。", path);
        if (new FileInfo(path).Length > 20 * 1024 * 1024)
        {
            throw new InvalidDataException("圖片檔案不可超過 20 MB。");
        }

        var extension = Path.GetExtension(path);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{settings.Mode} 只接受 {string.Join("、", extensions)} 檔案。");
        }

        return new Uri(path, UriKind.Absolute);
    }

    private void ReplaceFrames(IReadOnlyList<MascotFrame> frames)
    {
        if (frames.Count == 0) throw new InvalidDataException("圖片沒有可顯示的內容。");
        _frames.Clear();
        _frames.AddRange(frames);
        _frameIndex = 0;
        _nextFrameAt = DateTimeOffset.UtcNow + _frames[0].Duration;
    }
}
