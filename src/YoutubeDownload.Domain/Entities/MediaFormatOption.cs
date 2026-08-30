using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Domain.Entities;

/// <summary>
/// A single selectable media variant discovered during the resolve stage.
/// </summary>
/// <param name="Id">Provider-specific format identifier.</param>
/// <param name="Container">The container format.</param>
/// <param name="Label">Human-readable quality label (e.g. "1080p").</param>
/// <param name="Height">Vertical resolution in pixels (0 for audio-only).</param>
/// <param name="Extension">File extension (e.g. "mp4").</param>
/// <param name="EstimatedBytes">Optional estimated size for pre-flight validation.</param>
public sealed record MediaFormatOption(
    string Id,
    MediaFormat Container,
    string Label,
    int Height,
    string Extension,
    long? EstimatedBytes);