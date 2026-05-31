using System.Drawing;

using client.Models;

namespace client.Streaming.Capture;

internal interface IScreenCaptureBackend : IDisposable
{
    string Name { get; }

    bool IsSupported { get; }

    RemoteFrame? CaptureFrame();

    Bitmap? CaptureBitmap();
}
