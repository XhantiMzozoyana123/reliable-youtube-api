using System.Collections.Concurrent;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Infrastructure.Processing;

/// <summary>
/// Lock-free in-memory metrics used to power the reliability claims:
/// resolution/download success rates, retry recovery rate and processing-time percentiles.
/// </summary>
public sealed class InMemoryJobMetrics : IJobMetrics
{
    private long _created, _completed, _failed, _cancelled;
    private long _resolveAttempts, _resolveSuccess, _downloadAttempts, _downloadSuccess;
    private long _recoveryAttempts, _recoverySuccess;
    private readonly ConcurrentQueue<double> _processingSeconds = new();

    public void RecordJobCreated() => Interlocked.Increment(ref _created);
    public void RecordResolution(bool success)
    {
        Interlocked.Increment(ref _resolveAttempts);
        if (success) Interlocked.Increment(ref _resolveSuccess);
    }
    public void RecordDownload(bool success)
    {
        Interlocked.Increment(ref _downloadAttempts);
        if (success) Interlocked.Increment(ref _downloadSuccess);
    }
    public void RecordRetryRecovered(bool recovered)
    {
        Interlocked.Increment(ref _recoveryAttempts);
        if (recovered) Interlocked.Increment(ref _recoverySuccess);
    }
    public void RecordJobCompleted(double processingSeconds)
    {
        Interlocked.Increment(ref _completed);
        _processingSeconds.Enqueue(processingSeconds);
    }
    public void RecordJobFailed() => Interlocked.Increment(ref _failed);
    public void RecordJobCancelled() => Interlocked.Increment(ref _cancelled);

    public ReliabilitySnapshot Snapshot()
    {
        var times = _processingSeconds.ToArray();
        Array.Sort(times);
        double P(double pct) => times.Length == 0 ? 0 : times[Math.Min(times.Length - 1, (int)Math.Ceiling(pct / 100.0 * times.Length) - 1)];
        double Rate(long num, long den) => den == 0 ? 0 : Math.Round(num * 100.0 / den, 2);

        return new ReliabilitySnapshot(
            JobsCreated: Interlocked.Read(ref _created),
            JobsCompleted: Interlocked.Read(ref _completed),
            JobsFailed: Interlocked.Read(ref _failed),
            JobsCancelled: Interlocked.Read(ref _cancelled),
            ResolutionAttempts: Interlocked.Read(ref _resolveAttempts),
            ResolutionSuccesses: Interlocked.Read(ref _resolveSuccess),
            DownloadAttempts: Interlocked.Read(ref _downloadAttempts),
            DownloadSuccesses: Interlocked.Read(ref _downloadSuccess),
            RetryRecoveries: Interlocked.Read(ref _recoverySuccess),
            RecoveryAttempts: Interlocked.Read(ref _recoveryAttempts),
            AverageProcessingSeconds: times.Length == 0 ? 0 : Math.Round(times.Average(), 3),
            P95ProcessingSeconds: Math.Round(P(95), 3),
            RetryRecoveryRatePercent: Rate(Interlocked.Read(ref _recoverySuccess), Interlocked.Read(ref _recoveryAttempts)));
    }
}