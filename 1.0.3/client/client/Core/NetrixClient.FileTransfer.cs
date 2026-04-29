namespace client.Core;

internal sealed partial class NetrixClient
{
    public Task SendFileOfferAsync(FileTransferOffer offer, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = offer.TransferId,
            file_name = offer.FileName,
            file_size = offer.FileSize,
            total_chunks = offer.TotalChunks,
            sender_name = offer.SenderName,
            sender_client_id = offer.SenderClientId,
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_offer", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_offer",
                    ["transfer_id"] = offer.TransferId,
                    ["file_name"] = offer.FileName,
                    ["file_size"] = offer.FileSize,
                    ["total_chunks"] = offer.TotalChunks,
                    ["sender_name"] = offer.SenderName,
                    ["sender_client_id"] = offer.SenderClientId,
                },
                cancellationToken);
    }

    public Task SendFileChunkAsync(FileTransferChunk chunk, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = chunk.TransferId,
            chunk_index = chunk.ChunkIndex,
            total_chunks = chunk.TotalChunks,
            chunk_base64 = Convert.ToBase64String(chunk.ChunkBytes),
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_chunk", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_chunk",
                    ["transfer_id"] = chunk.TransferId,
                    ["chunk_index"] = chunk.ChunkIndex,
                    ["total_chunks"] = chunk.TotalChunks,
                    ["chunk_base64"] = Convert.ToBase64String(chunk.ChunkBytes),
                },
                cancellationToken);
    }

    public Task SendFileCompleteAsync(FileTransferComplete complete, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = complete.TransferId,
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_complete", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_complete",
                    ["transfer_id"] = complete.TransferId,
                },
                cancellationToken);
    }
}
