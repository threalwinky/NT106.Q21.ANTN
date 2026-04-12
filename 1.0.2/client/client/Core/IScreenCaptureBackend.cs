namespace client.Core;

internal interface IScreenCaptureBackend : IDisposable
{
    string Name { get; }

    bool IsSupported { get; }

    RemoteFrame? CaptureFrame();
}
