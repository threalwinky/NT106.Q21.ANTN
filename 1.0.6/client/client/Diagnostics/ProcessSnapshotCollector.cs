using System.Diagnostics;
using System.Runtime.InteropServices;

using client.Models;

namespace client.Diagnostics;

internal static class ProcessSnapshotCollector
{
    private const int CpuSampleMilliseconds = 700;

    public static async Task<ProcessSnapshotInfo> CaptureAsync(string requestId, CancellationToken cancellationToken)
    {
        var firstSample = CaptureSample();
        await Task.Delay(CpuSampleMilliseconds, cancellationToken);
        var secondSample = CaptureSample();
        var elapsedSeconds = CpuSampleMilliseconds / 1000d;
        var processorCount = Math.Max(1, Environment.ProcessorCount);
        var totalMemoryBytes = GetTotalPhysicalMemoryBytes();

        var processes = secondSample.Values
            .Select(sample =>
            {
                var cpuPercent = 0d;
                if (firstSample.TryGetValue(sample.ProcessId, out var previous)
                    && sample.TotalProcessorTime >= previous.TotalProcessorTime)
                {
                    cpuPercent = (sample.TotalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds
                        / (elapsedSeconds * 1000d * processorCount)
                        * 100d;
                }

                var memoryPercent = totalMemoryBytes > 0
                    ? sample.WorkingSetBytes / (double)totalMemoryBytes * 100d
                    : 0d;

                return new RemoteProcessInfo(
                    ProcessId: sample.ProcessId,
                    Name: sample.Name,
                    WindowTitle: sample.WindowTitle,
                    CpuPercent: Math.Round(Math.Max(0d, cpuPercent), 1),
                    WorkingSetBytes: sample.WorkingSetBytes,
                    MemoryPercent: Math.Round(Math.Max(0d, memoryPercent), 1),
                    ThreadCount: sample.ThreadCount,
                    HandleCount: sample.HandleCount,
                    StartTimeText: sample.StartTimeText);
            })
            .OrderByDescending(process => process.CpuPercent)
            .ThenByDescending(process => process.WorkingSetBytes)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProcessSnapshotInfo(
            RequestId: requestId,
            MachineName: Environment.MachineName,
            CapturedAtUtc: DateTime.UtcNow,
            ProcessorCount: processorCount,
            TotalMemoryBytes: totalMemoryBytes,
            Processes: processes);
    }

    private static Dictionary<int, ProcessSample> CaptureSample()
    {
        var samples = new Dictionary<int, ProcessSample>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processId = SafeRead(() => process.Id, 0);
                if (processId <= 0)
                {
                    continue;
                }

                samples[processId] = new ProcessSample(
                    ProcessId: processId,
                    Name: SafeRead(() => process.ProcessName, "Unknown"),
                    WindowTitle: SafeRead(() => process.MainWindowTitle, string.Empty),
                    TotalProcessorTime: SafeRead(() => process.TotalProcessorTime, TimeSpan.Zero),
                    WorkingSetBytes: Math.Max(0L, SafeRead(() => process.WorkingSet64, 0L)),
                    ThreadCount: Math.Max(0, SafeRead(() => process.Threads.Count, 0)),
                    HandleCount: Math.Max(0, SafeRead(() => process.HandleCount, 0)),
                    StartTimeText: FormatStartTime(SafeRead<DateTime?>(() => process.StartTime, null)));
            }
            finally
            {
                process.Dispose();
            }
        }

        return samples;
    }

    private static string FormatStartTime(DateTime? startTime)
    {
        return startTime.HasValue
            ? startTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : string.Empty;
    }

    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    private static long GetTotalPhysicalMemoryBytes()
    {
        var status = new MemoryStatusEx();
        status.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        return GlobalMemoryStatusEx(ref status) ? (long)status.TotalPhys : 0L;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private sealed record ProcessSample(
        int ProcessId,
        string Name,
        string WindowTitle,
        TimeSpan TotalProcessorTime,
        long WorkingSetBytes,
        int ThreadCount,
        int HandleCount,
        string StartTimeText);
}
