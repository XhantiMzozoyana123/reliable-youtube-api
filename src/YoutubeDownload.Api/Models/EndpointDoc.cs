namespace YoutubeDownload.Api.Models;

/// <summary>
/// Documentation model for a single endpoint, rendered by the shared <c>Docs/Endpoint</c> view.
/// </summary>
public sealed class EndpointDoc
{
    public string Title { get; init; } = default!;
    public string HttpMethod { get; init; } = default!;
    public string Route { get; init; } = default!;
    public bool AuthRequired { get; init; } = true;
    public string Overview { get; init; } = default!;

    public List<DocField> PathParameters { get; init; } = [];
    public List<DocField> RequestHeaders { get; init; } = [];
    public List<DocField> RequestBody { get; init; } = [];
    public List<EndpointResponse> Responses { get; init; } = [];

    public string CurlExample { get; init; } = default!;
    public string? Notes { get; init; }
}
