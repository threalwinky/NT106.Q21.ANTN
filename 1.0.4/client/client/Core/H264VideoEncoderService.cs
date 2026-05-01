using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using H264Sharp;
using H264ImageFormat = H264Sharp.ImageFormat;

namespace client.Core;

internal sealed class H264VideoEncoderService : IDisposable
{
    private readonly int _targetFps;
    private readonly int _targetBitrate;
    private readonly int _maxWidth;
    private H264Encoder? _encoder;
    private int _width;
    private int _height;
    private int _framesSinceKeyFrame;
    private bool _disabled;
    private string? _disabledReason;

    public H264VideoEncoderService(int targetFps, int targetBitrate, int maxWidth)
    {
        _targetFps = targetFps;
        _targetBitrate = targetBitrate;
        _maxWidth = maxWidth;
    }

    public bool IsAvailable => !_disabled;

    public string Status => _disabled
        ? $"H.264 unavailable: {_disabledReason}"
        : "H.264/OpenH264";

    public RemoteFrame? Encode(Bitmap sourceBitmap)
    {
        if (_disabled)
        {
            return null;
        }

        try
        {
            using var preparedBitmap = PrepareBitmap(sourceBitmap);
            var rgbBytes = CopyBitmapBytesAsRgb(preparedBitmap);
            EnsureEncoder(preparedBitmap.Width, preparedBitmap.Height);

            using var rgbImage = new RgbImage(
                H264ImageFormat.Rgb,
                preparedBitmap.Width,
                preparedBitmap.Height,
                preparedBitmap.Width * 3,
                rgbBytes);

            if (_framesSinceKeyFrame >= _targetFps * 2)
            {
                _encoder!.ForceIntraFrame();
                _framesSinceKeyFrame = 0;
            }

            if (!_encoder!.Encode(rgbImage, out var encodedLayers) || encodedLayers is null || encodedLayers.Length == 0)
            {
                return null;
            }

            var encodedBytes = EncodedDataExtentions.GetAllBytes(encodedLayers);
            if (encodedBytes.Length == 0)
            {
                return null;
            }

            _framesSinceKeyFrame++;
            return new RemoteFrame(encodedBytes, preparedBitmap.Width, preparedBitmap.Height, RemoteFrameCodec.H264);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Disable(ex.Message);
            return null;
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("Dll", StringComparison.OrdinalIgnoreCase))
        {
            Disable(ex.Message);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            Disable(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Disable(ex.Message);
            return null;
        }
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _encoder = null;
    }

    private void EnsureEncoder(int width, int height)
    {
        if (_encoder is not null && _width == width && _height == height)
        {
            return;
        }

        _encoder?.Dispose();
        H264Encoder.EnableDebugPrints = false;
        _encoder = new H264Encoder();
        var result = _encoder.Initialize(width, height, _targetBitrate, _targetFps, ConfigType.ScreenCaptureBasic);
        if (result != 0)
        {
            _encoder.Dispose();
            _encoder = null;
            throw new InvalidOperationException($"OpenH264 encoder initialization failed with code {result}.");
        }

        _encoder.ForceIntraFrame();
        _width = width;
        _height = height;
        _framesSinceKeyFrame = 0;
    }

    private Bitmap PrepareBitmap(Bitmap sourceBitmap)
    {
        var scale = sourceBitmap.Width > _maxWidth
            ? _maxWidth / (double)sourceBitmap.Width
            : 1.0;

        var width = MakeEven(Math.Max(2, (int)Math.Round(sourceBitmap.Width * scale)));
        var height = MakeEven(Math.Max(2, (int)Math.Round(sourceBitmap.Height * scale)));
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.Low;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(sourceBitmap, 0, 0, width, height);

        return bitmap;
    }

    private static byte[] CopyBitmapBytesAsRgb(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var sourceRowBytes = bitmap.Width * 4;
            var targetRowBytes = bitmap.Width * 3;
            var bytes = new byte[targetRowBytes * bitmap.Height];
            var sourceRowBuffer = new byte[sourceRowBytes];
            for (var row = 0; row < bitmap.Height; row++)
            {
                var sourceRow = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                Marshal.Copy(sourceRow, sourceRowBuffer, 0, sourceRowBytes);

                var targetOffset = row * targetRowBytes;
                var targetColumn = 0;
                for (var sourceColumn = 0; sourceColumn < sourceRowBytes; sourceColumn += 4)
                {
                    bytes[targetOffset + targetColumn] = sourceRowBuffer[sourceColumn + 2];
                    bytes[targetOffset + targetColumn + 1] = sourceRowBuffer[sourceColumn + 1];
                    bytes[targetOffset + targetColumn + 2] = sourceRowBuffer[sourceColumn];
                    targetColumn += 3;
                }
            }

            return bytes;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    private void Disable(string reason)
    {
        _disabled = true;
        _disabledReason = reason;
        Dispose();
    }

    private static int MakeEven(int value)
    {
        return value % 2 == 0 ? value : value - 1;
    }
}
