namespace YoutubeDownload.Application.Dtos;

/// <summary>Structured error object included when a job has failed.</summary>
public sealed record JobErrorDto(string Code, string Message, bool Retryable);