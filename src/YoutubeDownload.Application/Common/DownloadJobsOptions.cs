namespace YoutubeDownload.Application.Common;

/// <summary>Runtime options for job processing. Bound from configuration in the API layer.</summary>
public sealed class DownloadJobsOptions
{
    /// <summary>Base public URL used to build temporary download URLs.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>How long a completed file remains downloadable.</summary>
    public int OutputRetentionMinutes { get; set; } = 60;

    /// <summary>Directory used when Persistence/FileStorage mode is "FileSystem".</summary>
    public string OutputDirectory { get; set; } = "App_Data/jobs";

    /// <summary>Maximum automatic retry attempts for recoverable failures.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Per-job processing time budget in seconds.</summary>
    public int JobTimeoutSeconds { get; set; } = 300;

    /// <summary>Maximum concurrent downloads per processor instance.</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Which media provider to use: "Simulated" or "YtDlp".</summary>
    public string Provider { get; set; } = "Simulated";

    /// <summary>Path to the yt-dlp executable (used when Provider = "YtDlp").</summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>Storage mode: "Memory" (default, ephemeral) or "FileSystem" (survives restarts).</summary>
    public string Persistence { get; set; } = "FileSystem";
}