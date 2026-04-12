using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace client.Core;

internal sealed class DesktopDuplicationScreenCaptureBackend : IScreenCaptureBackend
{
    private const int DxgiErrorAccessLost = unchecked((int)0x887A0026);
    private const int DxgiErrorWaitTimeout = unchecked((int)0x887A0027);

    private readonly JpegFrameEncoder _encoder;

    private IDXGIAdapter1? _adapter;
    private IDXGIOutput? _output;
    private IDXGIOutput1? _output1;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _deviceContext;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _stagingTexture;
    private uint _stagingWidth;
    private uint _stagingHeight;
    private Format _stagingFormat = Format.Unknown;

    public DesktopDuplicationScreenCaptureBackend(JpegFrameEncoder encoder)
    {
        _encoder = encoder;
    }

    public string Name => "DXGI Desktop Duplication";

    public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(6, 2);

    public RemoteFrame? CaptureFrame()
    {
        EnsureInitialized();

        var acquireResult = _duplication!.AcquireNextFrame(100, out var _, out var desktopResource);
        if (acquireResult.Code == DxgiErrorWaitTimeout)
        {
            return null;
        }

        if (acquireResult.Code == DxgiErrorAccessLost)
        {
            ResetResources();
            return null;
        }

        acquireResult.CheckError();

        try
        {
            using var desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
            var sourceDescription = desktopTexture.Description;
            EnsureStagingTexture(sourceDescription);

            _deviceContext!.CopyResource(_stagingTexture!, desktopTexture);

            var mapped = _deviceContext.Map(_stagingTexture!, 0, MapMode.Read);
            try
            {
                using var bitmap = CreateBitmap(mapped, sourceDescription.Width, sourceDescription.Height);
                return _encoder.Encode(bitmap);
            }
            finally
            {
                _deviceContext.Unmap(_stagingTexture!, 0);
            }
        }
        finally
        {
            desktopResource.Dispose();
            _duplication.ReleaseFrame();
        }
    }

    public void Dispose()
    {
        ResetResources();
    }

    private void EnsureInitialized()
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Desktop Duplication requires Windows 8 or newer.");
        }

        if (_duplication is not null)
        {
            return;
        }

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        factory.EnumAdapters1(0, out _adapter).CheckError();
        _adapter!.EnumOutputs(0, out _output).CheckError();
        _output1 = _output.QueryInterface<IDXGIOutput1>();

        var creationResult = Vortice.Direct3D11.D3D11.D3D11CreateDevice(
            _adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
            },
            out ID3D11Device? device,
            out _,
            out ID3D11DeviceContext? deviceContext);
        creationResult.CheckError();

        _device = device ?? throw new InvalidOperationException("Unable to create a D3D11 device.");
        _deviceContext = deviceContext ?? throw new InvalidOperationException("Unable to create a D3D11 device context.");
        _duplication = _output1.DuplicateOutput(_device);
    }

    private void EnsureStagingTexture(Texture2DDescription sourceDescription)
    {
        if (_stagingTexture is not null
            && _stagingWidth == sourceDescription.Width
            && _stagingHeight == sourceDescription.Height
            && _stagingFormat == sourceDescription.Format)
        {
            return;
        }

        _stagingTexture?.Dispose();
        _stagingTexture = _device!.CreateTexture2D(
            new Texture2DDescription(
                sourceDescription.Format,
                sourceDescription.Width,
                sourceDescription.Height,
                sourceDescription.ArraySize,
                sourceDescription.MipLevels,
                BindFlags.None,
                ResourceUsage.Staging,
                CpuAccessFlags.Read,
                sourceDescription.SampleDescription.Count,
                sourceDescription.SampleDescription.Quality,
                ResourceOptionFlags.None));

        _stagingWidth = sourceDescription.Width;
        _stagingHeight = sourceDescription.Height;
        _stagingFormat = sourceDescription.Format;
    }

    private void ResetResources()
    {
        _stagingTexture?.Dispose();
        _duplication?.Dispose();
        _deviceContext?.Dispose();
        _device?.Dispose();
        _output1?.Dispose();
        _output?.Dispose();
        _adapter?.Dispose();

        _stagingTexture = null;
        _duplication = null;
        _deviceContext = null;
        _device = null;
        _output1 = null;
        _output = null;
        _adapter = null;
        _stagingWidth = 0;
        _stagingHeight = 0;
        _stagingFormat = Format.Unknown;
    }

    private static Bitmap CreateBitmap(MappedSubresource mappedSubresource, uint width, uint height)
    {
        var bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            var rowBytes = bitmap.Width * 4;
            var rowBuffer = new byte[rowBytes];

            for (var rowIndex = 0; rowIndex < bitmap.Height; rowIndex++)
            {
                var sourceRow = IntPtr.Add(mappedSubresource.DataPointer, rowIndex * (int)mappedSubresource.RowPitch);
                var targetRow = IntPtr.Add(bitmapData.Scan0, rowIndex * bitmapData.Stride);
                Marshal.Copy(sourceRow, rowBuffer, 0, rowBytes);
                Marshal.Copy(rowBuffer, 0, targetRow, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }
}
