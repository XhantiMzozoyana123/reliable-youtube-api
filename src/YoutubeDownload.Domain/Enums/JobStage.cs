namespace YoutubeDownload.Domain.Enums;

/// <summary>
/// A finer-grained position within <see cref="JobStatus.Processing"/>.
/// This directly maps to the "stage" field surfaced in job status responses.
/// </summary>
public enum JobStage
{
    Queued = 0,
    Resolving = 1,
    Downloading = 2,
    Validating = 3,
    Finalizing = 4
}