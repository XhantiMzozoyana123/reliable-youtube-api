using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Application.Dtos;

/// <summary>An available format/quality variant, as returned by GET /v1/download/{jobId}/formats.</summary>
public sealed record MediaFormatOptionDto(string Id, string Container, string Label, int Height, string Extension, long? EstimatedBytes)
{
    public static MediaFormatOptionDto From(MediaFormatOption o) =>
        new(o.Id, o.Container.ToString(), o.Label, o.Height, o.Extension, o.EstimatedBytes);
}