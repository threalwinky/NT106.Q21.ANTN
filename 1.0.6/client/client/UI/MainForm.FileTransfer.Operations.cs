using System.IO;

using client.Models;

namespace client.UI;

partial class MainForm
{
    private async Task SendFileAsync()
    {
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

        await SendFilePathAsync(dialog.FileName);
    }

    private async Task SendFilePathAsync(string filePath)
    {
        var session = _netrixClient.CurrentSession;
        if (session is null || !_netrixClient.IsConnected)
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Selected file does not exist.", filePath);
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
            ShowErrorDialog(ex.Message);
            AppendTransferStatus($"Send failed: {ex.Message}");
        }
    }

    private void HandleRemoteDragEnter(DragEventArgs e)
    {
        e.Effect = _netrixClient.IsConnected
            && _netrixClient.CurrentSession is not null
            && e.Data is not null
            && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
    }

    private async Task HandleRemoteDragDropAsync(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        foreach (var filePath in paths.Where(File.Exists))
        {
            await SendFilePathAsync(filePath);
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
            ShowErrorDialog(ex.Message);
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
            ShowErrorDialog(ex.Message);
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
}
