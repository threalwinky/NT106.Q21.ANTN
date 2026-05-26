using System.IO;
using client.Core;

namespace client;

partial class MainForm
{
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
