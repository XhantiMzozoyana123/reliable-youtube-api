namespace YoutubeDownload.Api.Models;

/// <summary>A documented HTTP response entry: status, description and optional example body.</summary>
public sealed record EndpointResponse(
    string Status,
    string Description,
    string? ExampleJson = null,
    string? BodyType = "json");
