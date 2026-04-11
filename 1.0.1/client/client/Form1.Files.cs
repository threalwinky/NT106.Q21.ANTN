using System.IO;
using client.Core;

namespace client;

partial class Form1
{
    private void InitializeTransferSettings()
    {
        _downloadsFolderPath = ResolveDefaultDownloadsFolder();
        EnsureDownloadsFolderExists();
    }

    private void SyncRoomSecurityState()
    {
        var session = _netrixClient.CurrentSession;
        if (session is null)
        {
            _netrixClient.ClearRoomSecurity();
            return;
        }

        var roomPassword = _roomPasswordTextBox.Text;
        if (UseRoomEncryption && !string.IsNullOrWhiteSpace(roomPassword))
        {
            _netrixClient.ConfigureRoomSecurity(session.RoomId, roomPassword, enabled: true);
            return;
        }

        _netrixClient.ClearRoomSecurity();
    }

    private async Task SendFileAsync()
    {
        var session = _netrixClient.CurrentSession;
        if (session is null || !_netrixClient.IsConnected)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Title = "Send a file to this room",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(dialog.FileName);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Selected file does not exist.", dialog.FileName);
            }

            var totalChunks = (int)Math.Ceiling(fileInfo.Length / (double)FileTransferChunkSize);
            var transferId = Guid.NewGuid().ToString("N");
            var offer = new FileTransferOffer(
                TransferId: transferId,
                FileName: fileInfo.Name,
                FileSize: fileInfo.Length,
                TotalChunks: totalChunks,
                SenderName: ResolveDisplayName(),
                SenderClientId: session.ClientId);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            AppendTransferStatus($"Sending {fileInfo.Name} ({fileInfo.Length / 1024d:0.0} KB)");
            await _netrixClient.SendFileOfferAsync(offer, cts.Token);

            await using var stream = fileInfo.OpenRead();
            var buffer = new byte[FileTransferChunkSize];
            var chunkIndex = 0;

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                {
                    break;
                }

                var chunkBytes = read == buffer.Length ? buffer.ToArray() : buffer[..read];
                await _netrixClient.SendFileChunkAsync(
                    new FileTransferChunk(
                        TransferId: transferId,
                        ChunkIndex: chunkIndex,
                        TotalChunks: totalChunks,
                        ChunkBytes: chunkBytes),
                    cts.Token);
                chunkIndex += 1;
            }

            await _netrixClient.SendFileCompleteAsync(new FileTransferComplete(transferId), cts.Token);
            AppendTransferStatus($"Finished sending {fileInfo.Name}");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
            AppendTransferStatus($"Send failed: {ex.Message}");
        }
    }

    private void HandleIncomingFileOffer(FileTransferOffer offer)
    {
        if (_netrixClient.CurrentSession?.ClientId == offer.SenderClientId)
        {
            return;
        }

        try
        {
            EnsureDownloadsFolderExists();
            var outputPath = CreateUniqueDownloadPath(offer.FileName);
            var transferState = new IncomingFileTransferState(offer.TransferId, outputPath, offer.TotalChunks);

            lock (_incomingTransfersLock)
            {
                if (_incomingTransfers.Remove(offer.TransferId, out var existing))
                {
                    existing.Dispose();
                }

                _incomingTransfers[offer.TransferId] = transferState;
            }

            AppendTransferStatus($"Receiving {offer.FileName} from {offer.SenderName}");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
            AppendTransferStatus($"Receive setup failed: {ex.Message}");
        }
    }

    private void HandleIncomingFileChunk(FileTransferChunk chunk)
    {
        IncomingFileTransferState? transferState;
        lock (_incomingTransfersLock)
        {
            _incomingTransfers.TryGetValue(chunk.TransferId, out transferState);
        }

        if (transferState is null)
        {
            return;
        }

        try
        {
            transferState.AppendChunk(chunk);
        }
        catch (Exception ex)
        {
            lock (_incomingTransfersLock)
            {
                _incomingTransfers.Remove(chunk.TransferId);
            }

            transferState.Dispose();
            UpdateStatus(ex.Message);
            AppendTransferStatus($"Receive failed: {ex.Message}");
        }
    }

    private void HandleIncomingFileComplete(FileTransferComplete complete)
    {
        IncomingFileTransferState? transferState;
        lock (_incomingTransfersLock)
        {
            if (!_incomingTransfers.Remove(complete.TransferId, out transferState))
            {
                return;
            }
        }

        try
        {
            transferState.Complete();
            AppendTransferStatus($"Saved file to {transferState.OutputPath}");
        }
        finally
        {
            transferState.Dispose();
        }
    }

    private void ClearIncomingTransfers()
    {
        lock (_incomingTransfersLock)
        {
            foreach (var transferState in _incomingTransfers.Values)
            {
                transferState.Dispose();
            }

            _incomingTransfers.Clear();
        }
    }

    private void AppendTransferStatus(string message)
    {
        OnUiThread(() =>
        {
            _transferListBox.Items.Add($"{DateTime.Now:HH:mm:ss} | {message}");
            _transferListBox.TopIndex = Math.Max(0, _transferListBox.Items.Count - 1);
            RefreshSessionChrome();
        });
    }

    private void EnsureDownloadsFolderExists()
    {
        Directory.CreateDirectory(ResolveDownloadsFolder());
    }

    private string ResolveDownloadsFolder()
    {
        var folder = _downloadsFolderPath.Trim();
        return string.IsNullOrWhiteSpace(folder) ? ResolveDefaultDownloadsFolder() : folder;
    }

    private static string ResolveDefaultDownloadsFolder()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, "Downloads", "Netrix");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Netrix");
    }

    private string CreateUniqueDownloadPath(string originalFileName)
    {
        var safeFileName = SanitizeFileName(originalFileName);
        var directory = ResolveDownloadsFolder();
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var candidate = Path.Combine(directory, safeFileName);
        var suffix = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({suffix}){extension}");
            suffix += 1;
        }

        return candidate;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "netrix-file.bin" : sanitized;
    }

    private sealed class IncomingFileTransferState : IDisposable
    {
        private readonly FileStream _stream;
        private readonly int _totalChunks;
        private int _nextChunkIndex;
        private bool _isCompleted;

        public IncomingFileTransferState(string transferId, string outputPath, int totalChunks)
        {
            TransferId = transferId;
            OutputPath = outputPath;
            _totalChunks = Math.Max(0, totalChunks);
            _stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public string TransferId { get; }

        public string OutputPath { get; }

        public void AppendChunk(FileTransferChunk chunk)
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("Transfer already completed.");
            }

            if (chunk.ChunkIndex != _nextChunkIndex)
            {
                throw new InvalidOperationException(
                    $"Unexpected chunk order for transfer {TransferId}. Expected {_nextChunkIndex}, got {chunk.ChunkIndex}.");
            }

            _stream.Write(chunk.ChunkBytes, 0, chunk.ChunkBytes.Length);
            _nextChunkIndex += 1;
        }

        public void Complete()
        {
            if (_isCompleted)
            {
                return;
            }

            if (_totalChunks > 0 && _nextChunkIndex < _totalChunks)
            {
                throw new InvalidOperationException(
                    $"Transfer {TransferId} ended early. Expected {_totalChunks} chunks, received {_nextChunkIndex}.");
            }

            _stream.Flush();
            _isCompleted = true;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
