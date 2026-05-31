using client.Models;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    public Task SendProcessRequestAsync(string requestId, CancellationToken cancellationToken)
    {
        return SendSecurePayloadAsync(
            "process_request",
            new
            {
                request_id = requestId,
            },
            cancellationToken,
            new Dictionary<string, object?>
            {
                ["request_id"] = requestId,
            });
    }

    public Task SendProcessSnapshotAsync(
        string targetClientId,
        ProcessSnapshotInfo snapshot,
        CancellationToken cancellationToken)
    {
        return SendSecurePayloadAsync(
            "process_snapshot",
            new
            {
                request_id = snapshot.RequestId,
                machine_name = snapshot.MachineName,
                captured_at_utc = snapshot.CapturedAtUtc,
                processor_count = snapshot.ProcessorCount,
                total_memory_bytes = snapshot.TotalMemoryBytes,
                processes = snapshot.Processes.Select(process => new
                {
                    process_id = process.ProcessId,
                    name = process.Name,
                    window_title = process.WindowTitle,
                    cpu_percent = process.CpuPercent,
                    working_set_bytes = process.WorkingSetBytes,
                    memory_percent = process.MemoryPercent,
                    thread_count = process.ThreadCount,
                    handle_count = process.HandleCount,
                    start_time = process.StartTimeText,
                }),
            },
            cancellationToken,
            new Dictionary<string, object?>
            {
                ["request_id"] = snapshot.RequestId,
                ["target_client_id"] = targetClientId,
            });
    }
}
