using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace client.Core;

internal sealed class JpegFrameEncoder
{
    private static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

    private readonly long _jpegQuality;
    private readonly int _maxWidth;

    public JpegFrameEncoder(long jpegQuality = 50, int maxWidth = 1440)
    {
        _jpegQuality = jpegQuality;
        _maxWidth = maxWidth;
    }

    public RemoteFrame Encode(Bitmap sourceBitmap)
    {
        using var resizedBitmap = ResizeIfNeeded(sourceBitmap);
        using var stream = new MemoryStream();
        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, _jpegQuality);
        resizedBitmap.Save(stream, JpegCodec, encoderParameters);
        return new RemoteFrame(stream.ToArray(), resizedBitmap.Width, resizedBitmap.Height);
    }

    private Bitmap ResizeIfNeeded(Bitmap sourceBitmap)
    {
        if (sourceBitmap.Width <= _maxWidth)
        {
            return new Bitmap(sourceBitmap);
        }

        var scale = _maxWidth / (double)sourceBitmap.Width;
        var resizedWidth = _maxWidth;
        var resizedHeight = Math.Max(1, (int)(sourceBitmap.Height * scale));
        var resizedBitmap = new Bitmap(resizedWidth, resizedHeight);

        using var graphics = Graphics.FromImage(resizedBitmap);
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.Low;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(sourceBitmap, 0, 0, resizedWidth, resizedHeight);

        return resizedBitmap;
    }
}
