using System.Globalization;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Application.Common;

/// <summary>
/// Pure planning logic that selects which media variant to download given the caller's
/// requested format and quality, following deterministic rules that avoid transcription.
/// </summary>
public static class MediaPlan
{
    /// <summary>
    /// Picks the best matching option. Format match is required; quality is a best-effort
    /// "highest resolution at or below the request" rule (never upscale, never transcode).
    /// </summary>
    public static MediaFormatOption? Select(
        IReadOnlyList<MediaFormatOption> options,
        string? requestedFormat,
        string? requestedQuality,
        MediaFormat defaultContainer = MediaFormat.Mp4)
    {
        if (options is null || options.Count == 0) return null;

        if (!TryParseContainer(requestedFormat, out var container)) container = defaultContainer;

        var inContainer = options
            .Where(o => o.Container == container)
            .ToList();
        if (inContainer.Count == 0)
        {
            // No audio for requested container; fall back to any container but signal gap.
            return options.OrderByDescending(o => o.Height).FirstOrDefault();
        }

        var targetHeight = ParseQualityHeight(requestedQuality);

        // Highest resolution that does NOT exceed the requested height.
        var best = inContainer
            .Where(o => targetHeight is null || o.Height == 0 || o.Height <= targetHeight.Value)
            .OrderByDescending(o => o.Height)
            .FirstOrDefault();

        // No option low enough -> pick the smallest available rather than transcoding.
        best ??= inContainer.OrderBy(o => o.Height).FirstOrDefault();

        return best;
    }

    /// <summary>Parses a requested container string (e.g. "mp4", "webm"). Defaults to null when unrecognized.</summary>
    public static bool TryParseContainer(string? value, out MediaFormat container)
    {
        container = MediaFormat.Mp4;
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Enum.TryParse(value.Trim(), ignoreCase: true, out container)
               && Enum.IsDefined(container);
    }

    /// <summary>Parses a requested quality string (e.g. "720p" or "p720" -> 720) into a target height.</summary>
    public static int? ParseQualityHeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim().ToLowerInvariant();
        if (s.StartsWith('p') && s.Length > 1) s = s[1..];
        if (s.EndsWith('p') && s.Length > 1) s = s[..^1];
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h > 0 ? h : null;
    }
}