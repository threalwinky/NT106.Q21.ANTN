using client.Core;

namespace client;

partial class MainForm
{
    private void StartHostStreaming()
    {
        ApplyCaptureBackendPreference();
        StopHostStreaming();
        _hostCaptureCts = new CancellationTokenSource();
        _ = Task.Run(() => HostStreamingLoopAsync(_hostCaptureCts.Token));
    }

    private async Task HostStreamingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var session = _netrixClient.CurrentSession;
                if (!_netrixClient.IsConnected || session?.Role != ParticipantRole.Host)
                {
                    return;
                }

                var frame = await Task.Run(_screenCaptureService.CapturePrimaryScreen, cancellationToken);
                if (frame is null)
                {
                    await Task.Delay(20, cancellationToken);
                    continue;
                }

                var frameSignature = ComputeFrameSignature(frame);
                if (string.Equals(frameSignature, _lastFrameSignature, StringComparison.Ordinal))
                {
                    await Task.Delay(150, cancellationToken);
                    continue;
                }

                _lastFrameSignature = frameSignature;
                await _netrixClient.SendFrameAsync(frame, cancellationToken);
                await Task.Delay(80, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Streaming stopped: {ex.Message}");
                return;
            }
        }
    }

    private void StopHostStreaming()
    {
        if (_hostCaptureCts is null)
        {
            return;
        }

        _hostCaptureCts.Cancel();
        _hostCaptureCts.Dispose();
        _hostCaptureCts = null;
        _lastFrameSignature = null;
    }

    private void StartPingLoop()
    {
        StopPingLoop();
        _pingCts = new CancellationTokenSource();
        _ = Task.Run(() => PingLoopAsync(_pingCts.Token));
    }

    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                if (_netrixClient.IsConnected)
                {
                    await _netrixClient.PingAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }
        }
    }

    private void StopPingLoop()
    {
        if (_pingCts is null)
        {
            return;
        }

        _pingCts.Cancel();
        _pingCts.Dispose();
        _pingCts = null;
    }

    private static string ComputeFrameSignature(RemoteFrame frame)
    {
        var bytes = frame.JpegBytes;
        var hash = new HashCode();
        hash.Add(frame.Width);
        hash.Add(frame.Height);
        hash.Add(bytes.Length);

        if (bytes.Length > 0)
        {
            var step = Math.Max(1, bytes.Length / 16);
            for (var index = 0; index < bytes.Length; index += step)
            {
                hash.Add(bytes[index]);
            }

            hash.Add(bytes[^1]);
        }

        return hash.ToHashCode().ToString("X8");
    }
}
