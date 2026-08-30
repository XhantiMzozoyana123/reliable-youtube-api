using System.Text.RegularExpressions;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Dtos;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Application.Features.Jobs;

/// <summary>Implementation of the V1 job use cases.</summary>
public sealed partial class DownloadJobsService : IDownloadJobsService
{
    private static readonly Regex YouTubeUrl = BuildUrlRegex();

    private readonly IJobStore _store;
    private readonly IJobScheduler _scheduler;
    private readonly IJobIdGenerator _ids;
    private readonly IFileStorage _storage;
    private readonly IJobMetrics _metrics;
    private readonly DownloadJobsOptions _options;

    public DownloadJobsService(
        IJobStore store,
        IJobScheduler scheduler,
        IJobIdGenerator ids,
        IFileStorage storage,
        IJobMetrics metrics,
        DownloadJobsOptions options)
    {
        _store = store;
        _scheduler = scheduler;
        _ids = ids;
        _storage = storage;
        _metrics = metrics;
        _options = options;
    }

    public async Task<CreateDownloadJobResponse> CreateAsync(CreateDownloadJobRequest request, string? accountId, string? requestId, CancellationToken ct = default)
    {
        if (request is null) throw new ValidationException("A request body is required.");
        if (string.IsNullOrWhiteSpace(request.Url)) throw new ValidationException("'url' is required.");

        var url = request.Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ValidationException("'url' must be an absolute http(s) URL.");

        if (!YouTubeUrl.IsMatch(uri.Host))
            throw new ValidationException("'url' must be a YouTube video URL (youtube.com/watch, youtu.be, or youtube.com/shorts).");

        if (request.Format is not null && !MediaPlan.TryParseContainer(request.Format, out _))
            throw new ValidationException($"Unsupported 'format' '{request.Format}'. Supported: mp4, webm, mkv, mp3, m4a, wav.");

        if (request.Quality is not null && MediaPlan.ParseQualityHeight(request.Quality) is null)
            throw new ValidationException($"Unsupported 'quality' '{request.Quality}'. Use values like '1080p', '720p', '480p'.");

        var job = new DownloadJob(_ids.Generate(), url, accountId, request.Format, request.Quality, DateTimeOffset.UtcNow, requestId);
        await _store.SaveAsync(job, ct).ConfigureAwait(false);
        _metrics.RecordJobCreated();
        await _scheduler.EnqueueAsync(job.JobId, ct).ConfigureAwait(false);

        return new CreateDownloadJobResponse(job.JobId, job.Status.ToString(), $"/v1/download/{job.JobId}");
    }

    public async Task<JobStatusResponse> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        var job = await GetJobOrThrowAsync(jobId, ct).ConfigureAwait(false);
        return JobStatusResponse.From(job);
    }

    public async Task<IReadOnlyList<MediaFormatOptionDto>> GetFormatsAsync(string jobId, CancellationToken ct = default)
    {
        var job = await GetJobOrThrowAsync(jobId, ct).ConfigureAwait(false);
        return job.UtilizedFormatOptions.Select(MediaFormatOptionDto.From).ToList();
    }

    public async Task<JobStatusResponse> CancelAsync(string jobId, CancellationToken ct = default)
    {
        var job = await GetJobOrThrowAsync(jobId, ct).ConfigureAwait(false);
        if (job.IsTerminal)
            throw new ConflictException($"Job '{jobId}' is already {job.Status.ToString().ToLowerInvariant()} and cannot be cancelled.");

        job.Cancel("Cancelled by caller");
        await _store.SaveAsync(job, ct).ConfigureAwait(false);
        await _storage.DeleteAsync(jobId, ct).ConfigureAwait(false);
        _metrics.RecordJobCancelled();
        return JobStatusResponse.From(job);
    }

    public async Task<(Stream Content, StoredFileInfo Info)> GetContentAsync(string jobId, CancellationToken ct = default)
    {
        var job = await GetJobOrThrowAsync(jobId, ct).ConfigureAwait(false);
        if (job.Status != Domain.Enums.JobStatus.Completed)
            throw new ConflictException($"Job '{jobId}' has not completed yet (status: {job.Status}).");
        if (job.ExpiresAtUtc is { } exp && exp <= DateTimeOffset.UtcNow)
            throw new NotFoundException($"The output for job '{jobId}' has expired. Submit a new download request.");

        var stored = await _storage.OpenReadAsync(jobId, ct).ConfigureAwait(false)
                     ?? throw new NotFoundException($"No stored output found for job '{jobId}'.");

        return stored;
    }

    private async Task<DownloadJob> GetJobOrThrowAsync(string jobId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobId)) throw new ValidationException("'jobId' is required.");
        return await _store.GetAsync(jobId, ct).ConfigureAwait(false)
               ?? throw new NotFoundException($"Job '{jobId}' was not found.");
    }

    [GeneratedRegex(@"(^|\.)(youtube\.com|youtu\.be|youtube-nocookie\.com)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuildUrlRegex();
}