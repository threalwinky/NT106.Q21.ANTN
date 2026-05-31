using System.Text.Json;

using client.Models;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    private static IReadOnlyList<ParticipantInfo> ParseParticipants(JsonElement root)
    {
        var participants = new List<ParticipantInfo>();
        foreach (var participant in root.GetProperty("participants").EnumerateArray())
        {
            participants.Add(
                new ParticipantInfo(
                    participant.GetProperty("client_id").GetString() ?? string.Empty,
                    participant.GetProperty("display_name").GetString() ?? string.Empty,
                    ParseRole(participant.GetProperty("role").GetString()),
                    ParseMode(participant.GetProperty("access_mode").GetString()),
                    participant.GetProperty("is_host").GetBoolean(),
                    participant.TryGetProperty("control_approved", out var controlApproved) ? controlApproved.GetBoolean() : true));
        }

        return participants;
    }

    private static ParticipantRole ParseRole(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "host" => ParticipantRole.Host,
            "controller" => ParticipantRole.Controller,
            _ => ParticipantRole.None,
        };
    }

    private static AppMode ParseMode(string? value)
    {
        return value?.ToLowerInvariant() == "internet" ? AppMode.Internet : AppMode.Lan;
    }

    private static RemoteFrameCodec ParseFrameCodec(JsonElement root)
    {
        if (!root.TryGetProperty("codec", out var codecElement) || codecElement.ValueKind == JsonValueKind.Null)
        {
            return RemoteFrameCodec.Jpeg;
        }

        if (codecElement.ValueKind == JsonValueKind.Number
            && TryParseFrameCodec(codecElement.GetInt32(), out var numericCodec))
        {
            return numericCodec;
        }

        var codecName = codecElement.GetString()?.Trim().ToLowerInvariant();
        return codecName switch
        {
            "h264" or "h.264" or "openh264" => RemoteFrameCodec.H264,
            _ => RemoteFrameCodec.Jpeg,
        };
    }

    private static bool TryParseFrameCodec(int value, out RemoteFrameCodec codec)
    {
        codec = value switch
        {
            (int)RemoteFrameCodec.Jpeg => RemoteFrameCodec.Jpeg,
            (int)RemoteFrameCodec.H264 => RemoteFrameCodec.H264,
            _ => default,
        };

        return codec != default;
    }

    private static FileTransferOffer ParseFileOffer(JsonElement root)
    {
        return new FileTransferOffer(
            TransferId: ReadString(root, "transfer_id"),
            FileName: ReadString(root, "file_name"),
            FileSize: ReadLong(root, "file_size"),
            TotalChunks: ReadInt(root, "total_chunks"),
            SenderName: ReadString(root, "sender_name"),
            SenderClientId: ReadString(root, "sender_client_id"));
    }

    private static FileTransferChunk ParseFileChunk(JsonElement root)
    {
        return new FileTransferChunk(
            TransferId: ReadString(root, "transfer_id"),
            ChunkIndex: ReadInt(root, "chunk_index"),
            TotalChunks: ReadInt(root, "total_chunks"),
            ChunkBytes: Convert.FromBase64String(ReadString(root, "chunk_base64")));
    }

    private static FileTransferComplete ParseFileComplete(JsonElement root)
    {
        return new FileTransferComplete(ReadString(root, "transfer_id"));
    }

    private static ProcessSnapshotInfo ParseProcessSnapshot(JsonElement root)
    {
        var processes = new List<RemoteProcessInfo>();
        if (root.TryGetProperty("processes", out var processesElement) && processesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var process in processesElement.EnumerateArray())
            {
                processes.Add(
                    new RemoteProcessInfo(
                        ProcessId: ReadInt(process, "process_id"),
                        Name: ReadString(process, "name"),
                        WindowTitle: ReadString(process, "window_title"),
                        CpuPercent: ReadDouble(process, "cpu_percent"),
                        WorkingSetBytes: ReadLong(process, "working_set_bytes"),
                        MemoryPercent: ReadDouble(process, "memory_percent"),
                        ThreadCount: ReadInt(process, "thread_count"),
                        HandleCount: ReadInt(process, "handle_count"),
                        StartTimeText: ReadString(process, "start_time")));
            }
        }

        return new ProcessSnapshotInfo(
            RequestId: ReadString(root, "request_id"),
            MachineName: ReadString(root, "machine_name"),
            CapturedAtUtc: ReadDateTime(root, "captured_at_utc"),
            ProcessorCount: ReadInt(root, "processor_count"),
            TotalMemoryBytes: ReadLong(root, "total_memory_bytes"),
            Processes: processes);
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? ReadNullableString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : 0;
    }

    private static long ReadLong(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt64()
            : 0L;
    }

    private static int? ReadNullableInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : null;
    }

    private static double? ReadNullableDouble(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : null;
    }

    private static double ReadDouble(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : 0d;
    }

    private static DateTime ReadDateTime(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return DateTime.UtcNow;
        }

        return value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;
    }
}
