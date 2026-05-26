using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using H264Sharp;
using H264ImageFormat = H264Sharp.ImageFormat;

namespace client.Core;

internal sealed class H264VideoDecoderService : IDisposable
{
    private H264Decoder? _decoder;
    private RgbImage? _rgbImage;
    private int _width;
    private int _height;

    public Bitmap DecodeToBitmap(RemoteFrame frame)
    {
        EnsureDecoder();
        EnsureOutputImage(frame.Width, frame.Height);

        var state = DecodingState.dsErrorFree;
        var outputImage = _rgbImage ?? throw new InvalidOperationException("OpenH264 output image was not initialized.");
        var decoded = _decoder!.Decode(
            frame.PayloadBytes,
            0,
            frame.PayloadBytes.Length,
            noDelay: true,
            out state,
            ref outputImage);
        _rgbImage = outputImage;

        if (!decoded)
        {
            throw new InvalidOperationException($"OpenH264 decoder did not output a frame. State: {state}");
        }

        var bytes = outputImage.GetBytes();
        return CreateBitmapFromBgr(bytes, frame.Width, frame.Height);
    }

    public void Dispose()
    {
        _rgbImage?.Dispose();
        _decoder?.Dispose();
        _rgbImage = null;
        _decoder = null;
    }

    private void EnsureDecoder()
    {
        if (_decoder is not null)
        {
            return;
        }

        H264Decoder.EnableDebugPrints = false;
        _decoder = new H264Decoder();
        var result = _decoder.Initialize();
        if (result != 0)
        {
            _decoder.Dispose();
            _decoder = null;
            throw new InvalidOperationException($"OpenH264 decoder initialization failed with code {result}.");
        }
    }

    private void EnsureOutputImage(int width, int height)
    {
        if (_rgbImage is not null && _width == width && _height == height)
        {
            return;
        }

        _rgbImage?.Dispose();
        _rgbImage = new RgbImage(H264ImageFormat.Rgb, width, height);
        _width = width;
        _height = height;
    }

    private static Bitmap CreateBitmapFromBgr(byte[] bytes, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rectangle = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var sourceRowBytes = width * 3;
            var targetRowBytes = width * 4;
            var targetRowBuffer = new byte[targetRowBytes];
            for (var row = 0; row < height; row++)
            {
                var sourceOffset = row * sourceRowBytes;
                var targetRow = IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride);
                var sourceColumn = 0;
                for (var targetColumn = 0; targetColumn < targetRowBytes; targetColumn += 4)
                {
                    targetRowBuffer[targetColumn] = bytes[sourceOffset + sourceColumn];
                    targetRowBuffer[targetColumn + 1] = bytes[sourceOffset + sourceColumn + 1];
                    targetRowBuffer[targetColumn + 2] = bytes[sourceOffset + sourceColumn + 2];
                    targetRowBuffer[targetColumn + 3] = 255;
                    sourceColumn += 3;
                }

                Marshal.Copy(targetRowBuffer, 0, targetRow, targetRowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }
}
