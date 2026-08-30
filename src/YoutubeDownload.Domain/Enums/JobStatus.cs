namespace YoutubeDownload.Domain.Enums;

/// <summary>
/// The top-level lifecycle state of a download job.
/// </summary>
public enum JobStatus
{
    /// <summary>Created and waiting to be picked up by a processor.</summary>
    Queued = 0,

    /// <summary>Actively being processed (resolving, downloading, validating).</summary>
    Processing = 1,

    /// <summary>Finished successfully; output passed validation and is downloadable.</summary>
    Completed = 2,

    /// <summary>Terminal failure after retries were exhausted or the failure was non-recoverable.</summary>
    Failed = 3,

    /// <summary>The caller cancelled an active job.</summary>
    Cancelled = 4
}