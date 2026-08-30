using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Infrastructure.Media;

/// <summary>
/// Real media provider that shells out to the `yt-dlp` binary.
/// Enable with  Media:Provider = "YtDlp"  and set  Media:YtDlpPath  if the binary is not on PATH.
/// The provider requires yt-dlp to be installed and kept up to date — that maintenance burden is
/// exactly what the service abstracts away from customers.
/// </summary>
public sealed class YtDlpMediaProvider : IMediaProvider
{
    private readonly DownloadJobsOptions _options;
    private readonly ILogger<YtDlpMediaProvider> _logger;

    public YtDlpMediaProvider(DownloadJobsOptions options, ILogger<YtDlpMediaProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string Name => "yt-dlp";

    public async Task<IReadOnlyList<MediaFormatOption>> ResolveFormatsAsync(string url, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await RunAsync(
            $"--dump-single-json --no-warnings --no-playlist \"{url}\"", ct).ConfigureAwait(false);

        if (code != 0)
            throw MapFailure(code, stderr);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        if (root.TryGetProperty("formats", out var formatsEl) && formatsEl.ValueKind == JsonValueKind.Array)
        {
            var list = new List<MediaFormatOption>();
            foreach (var f in formatsEl.EnumerateArray())
            {
                var id = f.TryGetProperty("format_id", out var idEl) ? idEl.GetString() ?? "" : "";
                var ext = f.TryGetProperty("ext", out var extEl) ? extEl.GetString() ?? "bin" : "bin";
                var height = f.TryGetProperty("height", out var hEl) && hEl.TryGetInt32(out var h) ? h : 0;
                var size = f.TryGetProperty("filesize", out var sEl) && sEl.TryGetInt64(out var s) ? (long?)s : null;

                if (string.IsNullOrEmpty(id)) continue;

                var isAudioOnly = ext is "m4a" or "mp3" or "wav";
                var container = ext switch
                {
                    "mp4" => MediaFormat.Mp4,
                    "webm" => MediaFormat.WebM,
                    "mkv" => MediaFormat.Mkv,
                    "m4a" => MediaFormat.M4a,
                    "mp3" => MediaFormat.Mp3,
                    "wav" => MediaFormat.Wav,
                    _ => MediaFormat.Mp4
                };

                list.Add(new MediaFormatOption(
                    id, container, isAudioOnly ? "audio" : $"{height}p", height, ext, size));
            }
            return list;
        }

        return [];
    }

    public async Task<DownloadedMedia> DownloadAsync(string url, MediaFormatOption option, IProgress<int> progress, CancellationToken ct = default)
    {
        var ext = option.Container is MediaFormat.M4a or MediaFormat.Mp3 ? option.Container.ToString().ToLowerInvariant() : option.Extension;
        var formatSelector = option.Height > 0
            ? $"bestvideo[height<={option.Height}][ext={ext}]+bestaudio/best[height<={option.Height}]/best"
            : "bestaudio/best";

        var tempDir = Path.Combine(Path.GetTempPath(), "ytdownload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputTemplate = Path.Combine(tempDir, "output.%(ext)s");

        try
        {
            var (code, _, stderr) = await RunAsync(
                $"-f \"{formatSelector}\" --no-playlist --no-warnings -o \"{outputTemplate}\" \"{url}\"", ct)
                .ConfigureAwait(false);

            if (code != 0)
                throw MapFailure(code, stderr);

            var produced = Directory.EnumerateFiles(tempDir).FirstOrDefault()
                ?? throw new MediaOperationException(JobErrorCode.ValidationFailed,
                    "The provider produced no output file.", retryable: true);

            var bytes = await File.ReadAllBytesAsync(produced, ct).ConfigureAwait(false);
            progress?.Report(100);

            return new DownloadedMedia(bytes, SimulatedMediaProvider.ContentTypeFor(option.Container),
                $"media_{option.Id}.{ext}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private MediaOperationException MapFailure(int exitCode, string stderr)
    {
        var s = stderr ?? "";
        _logger.LogWarning("yt-dlp exited with {Code}: {Stderr}", exitCode, s);

        if (s.Contains("Private video", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("members-only", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("removed", StringComparison.OrdinalIgnoreCase))
            return new MediaOperationException(JobErrorCode.VideoUnavailable,
                "The requested media is unavailable.", retryable: false);

        if (s.Contains("requested format", StringComparison.OrdinalIgnoreCase))
            return new MediaOperationException(JobErrorCode.FormatUnavailable,
                "The requested format is not available for this media.", retryable: false);

        return new MediaOperationException(JobErrorCode.DownloadFailed,
            "The media operation failed. This is usually transient.", retryable: true);
    }

    private async Task<(int Code, string StdOut, string StdErr)> RunAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.YtDlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }
}