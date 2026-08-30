namespace YoutubeDownload.Domain.Enums;

/// <summary>
/// Stable, machine-readable error codes returned to callers.
/// These are part of the public API contract and should never change once released.
/// </summary>
public enum JobErrorCode
{
    /// <summary>The supplied URL is not valid HTTP(S).</summary>
    InvalidUrl = 1000,

    /// <summary>The URL is not recognized as a supported YouTube link.</summary>
    UnsupportedUrl = 1001,

    /// <summary>The requested media is unavailable (removed, private or geo-restricted). Not retryable.</summary>
    VideoUnavailable = 1002,

    /// <summary>The requested container format is not available for this media.</summary>
    FormatUnavailable = 1003,

    /// <summary>The requested quality/height is not available for this media.</summary>
    QualityUnavailable = 1004,

    /// <summary>The media download failed after exhausting retries.</summary>
    DownloadFailed = 1005,

    /// <summary>The downloaded output failed validation before it could be marked complete.</summary>
    ValidationFailed = 1006,

    /// <summary>The job exceeded its allowed processing time budget.</summary>
    TimedOut = 1007,

    /// <summary>The account exceeded a rate/concurrency limit.</summary>
    RateLimited = 1008,

    /// <summary>An unexpected internal error occurred. Retryable.</summary>
    InternalError = 1009
}