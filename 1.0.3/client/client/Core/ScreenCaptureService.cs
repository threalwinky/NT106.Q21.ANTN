namespace client.Core;

internal sealed class ScreenCaptureService : IDisposable
{
    private readonly JpegFrameEncoder _encoder;
    private IScreenCaptureBackend? _backend;
    private CaptureBackendPreference _preference = CaptureBackendPreference.Auto;

    public ScreenCaptureService(long jpegQuality = 50, int maxWidth = 1440)
    {
        _encoder = new JpegFrameEncoder(jpegQuality, maxWidth);
        ActiveBackendName = DescribePreference(_preference);
    }

    public event Action<string>? BackendChanged;

    public string ActiveBackendName { get; private set; }

    public void SetPreference(CaptureBackendPreference preference)
    {
        if (_preference == preference)
        {
            return;
        }

        _preference = preference;
        ResetBackend();
        ActiveBackendName = DescribePreference(preference);
        BackendChanged?.Invoke(ActiveBackendName);
    }

    public RemoteFrame? CapturePrimaryScreen()
    {
        var backend = EnsureBackend();

        try
        {
            return backend.CaptureFrame();
        }
        catch (Exception) when (_preference == CaptureBackendPreference.Auto && backend is DesktopDuplicationScreenCaptureBackend)
        {
            SwitchBackend(new GdiScreenCaptureBackend(_encoder));
            return _backend!.CaptureFrame();
        }
    }

    public void Dispose()
    {
        ResetBackend();
    }

    private IScreenCaptureBackend EnsureBackend()
    {
        if (_backend is not null)
        {
            return _backend;
        }

        return _preference switch
        {
            CaptureBackendPreference.DesktopDuplication => SwitchBackend(new DesktopDuplicationScreenCaptureBackend(_encoder)),
            CaptureBackendPreference.GdiFallback => SwitchBackend(new GdiScreenCaptureBackend(_encoder)),
            _ => CreateAutomaticBackend(),
        };
    }

    private IScreenCaptureBackend CreateAutomaticBackend()
    {
        var dxgiBackend = new DesktopDuplicationScreenCaptureBackend(_encoder);
        if (dxgiBackend.IsSupported)
        {
            return SwitchBackend(dxgiBackend);
        }

        dxgiBackend.Dispose();
        return SwitchBackend(new GdiScreenCaptureBackend(_encoder));
    }

    private IScreenCaptureBackend SwitchBackend(IScreenCaptureBackend backend)
    {
        ResetBackend();
        _backend = backend;
        ActiveBackendName = backend.Name;
        BackendChanged?.Invoke(ActiveBackendName);
        return backend;
    }

    private void ResetBackend()
    {
        _backend?.Dispose();
        _backend = null;
    }

    private static string DescribePreference(CaptureBackendPreference preference)
    {
        return preference switch
        {
            CaptureBackendPreference.DesktopDuplication => "DXGI Desktop Duplication (forced)",
            CaptureBackendPreference.GdiFallback => "GDI CopyFromScreen (forced)",
            _ => "Auto (prefer DXGI Desktop Duplication)",
        };
    }
}
