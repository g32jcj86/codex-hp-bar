using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexHpBar.Core;

namespace CodexHpBar;

internal sealed record MascotFrame(ImageSource Image, TimeSpan Duration, double VerticalOffsetRatio = 0, double HorizontalOffsetRatio = 0);

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

    public double CurrentHorizontalOffsetRatio => _frames.Count == 0 ? 0 : _frames[_frameIndex].HorizontalOffsetRatio;

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

        var sourceFrames = decoder.Frames.ToArray();
        var frameRects = sourceFrames.Select(GetGifFrameRect).ToArray();
        var canvasSize = GetGifCanvasSize(sourceFrames, frameRects);
        var frames = new List<MascotFrame>(sourceFrames.Length);
        for (var index = 0; index < sourceFrames.Length; index++)
        {
            var frame = sourceFrames[index];
            var duration = ReadGifDuration(frame);
            // GifBitmapDecoder exposes optimized GIF frames as cropped images.
            // Later frames can therefore be 113x193, 196x180, etc. Drawing
            // those directly into a fixed 34x34 slot changes their aspect
            // ratio and makes the character look wider or thinner. Rebuild
            // every frame on the same square logical canvas using the GIF
            // frame offset before the runtime scales it.
            frames.Add(new MascotFrame(
                ComposeGifFrame(frame, frameRects[index], canvasSize),
                duration));
        }

        return frames;
    }

    private static (int Width, int Height) GetGifCanvasSize(IReadOnlyList<BitmapFrame> frames, IReadOnlyList<Int32Rect> frameRects)
    {
        var canvasWidth = frameRects.Max(rect => rect.X + rect.Width);
        var canvasHeight = frameRects.Max(rect => rect.Y + rect.Height);
        if (frames.FirstOrDefault()?.Metadata is BitmapMetadata metadata)
        {
            canvasWidth = Math.Max(canvasWidth, ReadGifMetadataInt(metadata, "/logscrdesc/Width", 0));
            canvasHeight = Math.Max(canvasHeight, ReadGifMetadataInt(metadata, "/logscrdesc/Height", 0));
        }

        canvasWidth = Math.Max(1, canvasWidth);
        canvasHeight = Math.Max(1, canvasHeight);
        if (canvasWidth > 4096 || canvasHeight > 4096)
        {
            throw new InvalidDataException("GIF 邏輯畫布不可超過 4096×4096 像素。");
        }

        return (canvasWidth, canvasHeight);
    }

    private static Int32Rect GetGifFrameRect(BitmapFrame frame)
    {
        var left = 0;
        var top = 0;
        if (frame.Metadata is BitmapMetadata metadata)
        {
            left = Math.Max(0, ReadGifMetadataInt(metadata, "/imgdesc/Left", 0));
            top = Math.Max(0, ReadGifMetadataInt(metadata, "/imgdesc/Top", 0));
        }

        return new Int32Rect(left, top, frame.PixelWidth, frame.PixelHeight);
    }

    private static BitmapSource ComposeGifFrame(BitmapFrame frame, Int32Rect frameRect, (int Width, int Height) canvasSize)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(frame, new Rect(frameRect.X, frameRect.Y, frame.PixelWidth, frame.PixelHeight));
        }

        var canvas = new RenderTargetBitmap(canvasSize.Width, canvasSize.Height, 96, 96, PixelFormats.Pbgra32);
        canvas.Render(visual);
        canvas.Freeze();
        return canvas;
    }

    private static int ReadGifMetadataInt(BitmapMetadata metadata, string query, int fallback)
    {
        if (!metadata.ContainsQuery(query)) return fallback;

        return metadata.GetQuery(query) switch
        {
            byte value => value,
            sbyte value => value,
            short value => value,
            ushort value => value,
            int value => value,
            uint value when value <= int.MaxValue => (int)value,
            long value when value is >= int.MinValue and <= int.MaxValue => (int)value,
            ulong value when value <= int.MaxValue => (int)value,
            _ => fallback
        };
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
        var sourceFrames = new List<BitmapSource>(16);
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
                sourceFrames.Add(frame);
            }
        }

        // Crop every frame with one shared rectangle. This removes common
        // transparent padding so a small toolbar can show more of the actor,
        // while preserving one identical transform for all 16 frames.
        var displayBox = FindCommonVisibleBounds(sourceFrames, padding: 4);
        var frames = new List<MascotFrame>(16);
        foreach (var sourceFrame in sourceFrames)
        {
            var displayFrame = new CroppedBitmap(sourceFrame, displayBox);
            displayFrame.Freeze();
            frames.Add(new MascotFrame(displayFrame, duration));
        }

        return frames;
    }

    private static Int32Rect FindCommonVisibleBounds(IReadOnlyList<BitmapSource> frames, int padding)
    {
        if (frames.Count == 0) return new Int32Rect(0, 0, 1, 1);

        var width = frames[0].PixelWidth;
        var height = frames[0].PixelHeight;
        var left = width;
        var top = height;
        var right = 0;
        var bottom = 0;
        foreach (var frame in frames)
        {
            var bounds = FindVisibleBounds(frame);
            if (bounds.Left < 0 || bounds.Right < 0 || bounds.Bottom < 0) continue;
            left = Math.Min(left, bounds.Left);
            top = Math.Min(top, bounds.Top);
            right = Math.Max(right, bounds.Right);
            bottom = Math.Max(bottom, bounds.Bottom + 1);
        }

        if (right <= left || bottom <= top) return new Int32Rect(0, 0, width, height);
        left = Math.Max(0, left - padding);
        top = Math.Max(0, top - padding);
        right = Math.Min(width, right + padding);
        bottom = Math.Min(height, bottom + padding);
        var cropWidth = Math.Max(1, right - left);
        var cropHeight = Math.Max(1, bottom - top);
        var cropSize = Math.Min(width, Math.Max(cropWidth, cropHeight));
        if (cropWidth < cropSize)
        {
            var extra = cropSize - cropWidth;
            left = Math.Max(0, Math.Min(width - cropSize, left - extra / 2));
        }
        if (cropHeight < cropSize)
        {
            var extra = cropSize - cropHeight;
            top = Math.Max(0, Math.Min(height - cropSize, top - extra / 2));
        }
        return new Int32Rect(left, top, cropSize, cropSize);
    }

    private static (int Left, int Top, int Right, int Bottom) FindVisibleBounds(BitmapSource frame)
    {
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var visibleLeft = converted.PixelWidth;
        var visibleTop = converted.PixelHeight;
        var visibleRight = -1;
        var visibleBottom = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                if (pixels[y * stride + x * 4 + 3] < 16) continue;
                visibleLeft = Math.Min(visibleLeft, x);
                visibleTop = Math.Min(visibleTop, y);
                visibleRight = Math.Max(visibleRight, x + 1);
                visibleBottom = Math.Max(visibleBottom, y);
            }
        }

        return (visibleLeft == converted.PixelWidth ? -1 : visibleLeft, visibleTop, visibleRight, visibleBottom);
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
