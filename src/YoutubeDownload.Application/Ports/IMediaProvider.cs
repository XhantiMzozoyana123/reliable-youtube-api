using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Application.Ports;

/// <summary>The produced media asset returned by a provider after a successful download.</summary>
public sealed record DownloadedMedia(byte[] Bytes, string FileName, string ContentType);

/// <summary>
/// Abstraction over the underlying media resolution/download engine.
/// V1 ships a simulated provider (for a working demo without external binaries) and a
/// yt-dlp-based provider that can be enabled via configuration.
/// </summary>
public interface IMediaProvider
{
    /// <summary>Provider name used in diagnostics (e.g. "simulated", "yt-dlp").</summary>
    string Name { get; }

    /// <summary>Discovers available formats/qualities for the given URL.</summary>
    Task<IReadOnlyList<MediaFormatOption>> ResolveFormatsAsync(string url, CancellationToken ct = default);

    /// <summary>Downloads the selected option, reporting progress as 0..100.</summary>
    Task<DownloadedMedia> DownloadAsync(string url, MediaFormatOption option, IProgress<int> progress, CancellationToken ct = default);
}