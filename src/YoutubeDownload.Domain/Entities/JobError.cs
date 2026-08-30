using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Domain.Entities;

/// <summary>
/// A structured failure associated with a job's terminal state.
/// Mirrors the "error" object returned in API responses:
/// <code>{ code, message, retryable }</code>
/// </summary>
public sealed record JobError(JobErrorCode Code, string Message, bool Retryable)
{
    public static JobError Internal(string message) => new(JobErrorCode.InternalError, message, true);
}