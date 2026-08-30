namespace YoutubeDownload.Application.Ports;

/// <summary>
/// Contract for scheduling a job for asynchronous processing.
/// Implemented in Infrastructure with an in-process channel + background worker.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Enqueues a job id so the processing pipeline picks it up.</summary>
    Task EnqueueAsync(string jobId, CancellationToken ct = default);
}