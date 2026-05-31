using System.Diagnostics;

using client.Models;

namespace client.UI;

partial class MainForm
{
    private void StartHostStreaming()
    {
        ApplyCaptureBackendPreference();
        StopHostStreaming();
        var captureCts = new CancellationTokenSource();
        _hostCaptureCts = captureCts;
        _hostStreamingThread = new Thread(() => HostStreamingLoop(captureCts.Token))
        {
            IsBackground = true,
            Name = "Netrix Host Streaming",
            Priority = ThreadPriority.AboveNormal,
        };
        _hostStreamingThread.Start();
    }

    private void HostStreamingLoop(CancellationToken cancellationToken)
    {
        var frameClock = Stopwatch.StartNew();
        var nextFrameAt = TimeSpan.Zero;
        var statsStartedAt = DateTime.UtcNow;
        var sentFramesInWindow = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var waitForNextFrame = nextFrameAt - frameClock.Elapsed;
                if (waitForNextFrame > TimeSpan.Zero)
                {
                    if (cancellationToken.WaitHandle.WaitOne(waitForNextFrame))
                    {
                        return;
                    }
                }

                var frameStartedAt = frameClock.Elapsed;
                nextFrameAt = frameStartedAt + TargetFrameInterval;

                var session = _netrixClient.CurrentSession;
                if (!_netrixClient.IsConnected || session?.Role != ParticipantRole.Host)
                {
                    return;
                }

                var frame = CaptureStreamingFrame();
                if (frame is null)
                {
                    PublishStreamingStatsIfDue(ref sentFramesInWindow, ref statsStartedAt);
                    continue;
                }

                if (frame.Codec == RemoteFrameCodec.Jpeg)
                {
                    var frameSignature = ComputeFrameSignature(frame);
                    if (string.Equals(frameSignature, _lastFrameSignature, StringComparison.Ordinal))
                    {
                        PublishStreamingStatsIfDue(ref sentFramesInWindow, ref statsStartedAt);
                        continue;
                    }

                    _lastFrameSignature = frameSignature;
                }
                else
                {
                    _lastFrameSignature = null;
                }

                _netrixClient.SendFrameAsync(frame, cancellationToken).GetAwaiter().GetResult();
                sentFramesInWindow++;
                PublishStreamingStatsIfDue(ref sentFramesInWindow, ref statsStartedAt);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                OnUiThread(() => ShowErrorDialog($"Streaming stopped: {ex.Message}"));
                return;
            }
        }
    }

    private RemoteFrame? CaptureStreamingFrame()
    {
        using var bitmap = _screenCaptureService.CapturePrimaryBitmap();
        if (bitmap is null)
        {
            return null;
        }

        var h264Frame = _h264VideoEncoderService.Encode(bitmap);
        return h264Frame ?? _screenCaptureService.EncodeBitmap(bitmap);
    }

    private void StopHostStreaming()
    {
        var captureCts = _hostCaptureCts;
        var streamingThread = _hostStreamingThread;
        if (captureCts is null)
        {
            return;
        }

        captureCts.Cancel();
        if (streamingThread is not null && streamingThread.IsAlive && !ReferenceEquals(streamingThread, Thread.CurrentThread))
        {
            streamingThread.Join(TimeSpan.FromSeconds(1));
        }

        captureCts.Dispose();
        _hostCaptureCts = null;
        _hostStreamingThread = null;
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
        var bytes = frame.PayloadBytes;
        var hash = new HashCode();
        hash.Add(frame.Codec);
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

    private void PublishStreamingStatsIfDue(ref int sentFramesInWindow, ref DateTime statsStartedAt)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - statsStartedAt;
        if (elapsed < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var fps = sentFramesInWindow / Math.Max(0.001, elapsed.TotalSeconds);
        sentFramesInWindow = 0;
        statsStartedAt = now;

        OnUiThread(() =>
        {
            var session = _netrixClient.CurrentSession;
            if (_netrixClient.IsConnected && session?.Role == ParticipantRole.Host)
            {
                UpdateStatus(
                    $"Hosting room {session.RoomId}. Stream {fps:0.0} FPS sent, {TargetStreamingFps} FPS target, {_h264VideoEncoderService.Status}.");
            }
        });
    }
}
