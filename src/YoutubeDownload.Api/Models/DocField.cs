namespace YoutubeDownload.Api.Models;

/// <summary>A single field in a request-body or response schema table.</summary>
public sealed record DocField(
    string Name,
    string Type,
    bool Required,
    string Description,
    string? Example = null);
