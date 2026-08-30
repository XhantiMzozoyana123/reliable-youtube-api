using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Infrastructure.Media;

/// <summary>
/// Deterministic, dependency-free provider used for the default/demo configuration and tests.
/// It exercises the full pipeline (resolve -> download -> progress -> validate) without any
/// external binary, and simulates two important behaviours:
///   * "unavailable" in the URL -> VIDEO_UNAVAILABLE (non-retryable)
///   * first download attempt on a URL containing "flaky" -> transient failure that recovers on retry
/// Swap in YtDlpMediaProvider for real media retrieval.
/// </summary>
public sealed class SimulatedMediaProvider : IMediaProvider
{
    public string Name => "simulated";

    public Task<IReadOnlyList<MediaFormatOption>> ResolveFormatsAsync(string url, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (url.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
            throw new MediaOperationException(JobErrorCode.VideoUnavailable,
                "The requested media is unavailable (removed, private or access-restricted).", retryable: false);

        var videoId = ExtractVideoId(url) ?? "dQw4w9WgXcQ";
        var seed = (uint)HashCode.Combine(videoId);

        // 360p/720p/1080p mp4 + webm, plus audio-only tracks.
        List<MediaFormatOption> formats =
        [
            new("18", MediaFormat.Mp4,  "360p",  360,  "mp4", 8_400_000),
            new("22", MediaFormat.Mp4,  "720p",  720,  "mp4", 24_500_000),
            new("137", MediaFormat.Mp4, "1080p", 1080, "mp4", 51_200_000),
            new("244", MediaFormat.WebM, "480p", 480,  "webm", 9_900_000),
            new("248", MediaFormat.WebM, "1080p", 1080, "webm", 48_100_000),
            new("140", MediaFormat.M4a, "audio", 0,    "m4a", 3_800_000)
        ];

        // Occasionally a video simply has no 1080p variant.
        if (seed % 5 == 0) formats.RemoveAll(f => f.Height == 1080);

        IReadOnlyList<MediaFormatOption> result = formats;
        return Task.FromResult(result);
    }

    public async Task<DownloadedMedia> DownloadAsync(string url, MediaFormatOption option, IProgress<int> progress, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var attempts = _transientFailures.GetOrAdd(url, _ => 0);
        if (url.Contains("flaky", StringComparison.OrdinalIgnoreCase) && attempts < 1)
        {
            _transientFailures[url] = attempts + 1;
            throw new MediaOperationException(JobErrorCode.DownloadFailed,
                "The media connection was reset during download.", retryable: true);
        }

        var videoId = ExtractVideoId(url) ?? "video";
        var size = option.EstimatedBytes ?? 5_000_000;
        // Keep the demo output small but proportionally representative (the truncation
        // guard rejects outputs far below the estimated size).
        var demoBytes = (int)Math.Clamp(size / 8, 256 * 1024, 4 * 1024 * 1024);

        var buffer = new byte[demoBytes];
        RandomNumberGenerator.Fill(buffer);

        // Report progress in ~10 increments to exercise the progress-tracking contract.
        for (var i = 1; i <= 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(120, ct).ConfigureAwait(false);
            progress?.Report(i * 10);
        }

        var fileName = $"{Sanitize(videoId)}_{option.Label}.{option.Extension}";
        return new DownloadedMedia(buffer, fileName, ContentTypeFor(option.Container));
    }

    private readonly ConcurrentDictionary<string, int> _transientFailures = new(StringComparer.Ordinal);

    internal static string? ExtractVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Trim('/');

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var v = query["v"];
        if (!string.IsNullOrWhiteSpace(v)) return v;

        var seg = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return seg.Length >= 2 && seg[0] is "shorts" or "embed" or "live" ? seg[1] : null;
    }

    internal static string ContentTypeFor(MediaFormat container) => container switch
    {
        MediaFormat.Mp4 => "video/mp4",
        MediaFormat.WebM => "video/webm",
        MediaFormat.Mkv => "video/x-matroska",
        MediaFormat.Mp3 => "audio/mpeg",
        MediaFormat.M4a => "audio/mp4",
        MediaFormat.Wav => "audio/wav",
        _ => "application/octet-stream"
    };

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        return sb.ToString().Length == 0 ? "media" : sb.ToString();
    }
}