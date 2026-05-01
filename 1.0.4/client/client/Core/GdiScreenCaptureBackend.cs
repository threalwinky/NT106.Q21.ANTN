using System.Drawing;

namespace client.Core;

internal sealed class GdiScreenCaptureBackend : IScreenCaptureBackend
{
    private readonly JpegFrameEncoder _encoder;

    public GdiScreenCaptureBackend(JpegFrameEncoder encoder)
    {
        _encoder = encoder;
    }

    public string Name => "GDI CopyFromScreen";

    public bool IsSupported => true;

    public RemoteFrame? CaptureFrame()
    {
        using var sourceBitmap = CaptureBitmap();
        return sourceBitmap is null ? null : _encoder.Encode(sourceBitmap);
    }

    public Bitmap? CaptureBitmap()
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("No primary screen is available.");
        var bounds = screen.Bounds;

        var sourceBitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(sourceBitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        return sourceBitmap;
    }

    public void Dispose()
    {
    }
}
