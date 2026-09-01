namespace YoutubeDownload.Api.Models;

/// <summary>One row in the documentation index listing.</summary>
public sealed record EndpointSummary(
    string Title,
    string HttpMethod,
    string Route,
    string Description,
    string ActionName);
