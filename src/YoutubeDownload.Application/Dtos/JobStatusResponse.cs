using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Application.Dtos;

/// <summary>
/// Full job state returned by GET /v1/download/{jobId}.
/// This is the transparency contract: what stage, what progress, what failed and why,
/// and whether retrying makes sense.
/// </summary>
public sealed record JobStatusResponse(
    string JobId,
    string RequestId,
    string Status,
    string Stage,
    int Progress,
    int? EtaSeconds,
    int Attempts,
    string? Message,
    JobErrorDto? Error,
    IReadOnlyList<MediaFormatOptionDto>? Formats,
    string? RequestedFormat,
    string? RequestedQuality,
    string? DownloadUrl,
    string? FileName,
    string? ContentType,
    long? FileBytes,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<JobEventDto>? Timeline)
{
    public static JobStatusResponse From(DownloadJob job) => new(
        job.JobId,
        job.RequestId,
        job.Status.ToString(),
        job.Stage.ToString(),
        job.Progress,
        job.EtaSeconds,
        job.Attempts,
        job.Message,
        job.Error is null ? null : new JobErrorDto(job.Error.Code.ToString(), job.Error.Message, job.Error.Retryable),
        job.UtilizedFormatOptions.Count == 0 ? null : job.UtilizedFormatOptions.Select(MediaFormatOptionDto.From).ToList(),
        job.RequestedFormat,
        job.RequestedQuality,
        job.DownloadUrl,
        job.FileName,
        job.ContentType,
        job.FileBytes,
        job.ExpiresAtUtc,
        job.CreatedAtUtc,
        job.StartedAtUtc,
        job.CompletedAtUtc,
        job.UpdatedAtUtc,
        job.Events.Count == 0 ? null : job.Events.Select(e => new JobEventDto(e.AtUtc, e.Message)).ToList());
}

/// <summary>One timeline entry in the job's failure/transition history.</summary>
public sealed record JobEventDto(DateTimeOffset AtUtc, string Message);