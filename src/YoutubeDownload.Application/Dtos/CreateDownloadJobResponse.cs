namespace YoutubeDownload.Application.Dtos;

/// <summary>Response returned immediately by POST /v1/download (202 Accepted).</summary>
public sealed record CreateDownloadJobResponse(string JobId, string Status, string StatusUrl);