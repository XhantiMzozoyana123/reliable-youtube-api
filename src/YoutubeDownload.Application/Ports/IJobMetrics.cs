namespace YoutubeDownload.Application.Ports;

/// <summary>A point-in-time view of the reliability metrics tracked from launch (business spec §15).</summary>
public sealed record ReliabilitySnapshot(
    long JobsCreated,
    long JobsCompleted,
    long JobsFailed,
    long JobsCancelled,
    long ResolutionAttempts,
    long ResolutionSuccesses,
    long DownloadAttempts,
    long DownloadSuccesses,
    long RetryRecoveries,
    long RecoveryAttempts,
    double AverageProcessingSeconds,
    double P95ProcessingSeconds,
    double RetryRecoveryRatePercent);

/// <summary>
/// In-memory reliability telemetry. These are the numbers that let us make claims like
/// "X% of recoverable failures were automatically recovered" instead of just "reliable".
/// </summary>
public interface IJobMetrics
{
    void RecordJobCreated();
    void RecordResolution(bool success);
    void RecordDownload(bool success);
    void RecordRetryRecovered(bool recovered);
    void RecordJobCompleted(double processingSeconds);
    void RecordJobFailed();
    void RecordJobCancelled();
    ReliabilitySnapshot Snapshot();
}