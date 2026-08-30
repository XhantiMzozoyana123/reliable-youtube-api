namespace YoutubeDownload.Application.Dtos;

/// <summary>
/// Request body for POST /v1/download.
/// Only <c>url</c> is required; format/quality are optional preferences.
/// </summary>
public sealed record CreateDownloadJobRequest(string Url, string? Format, string? Quality);