using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Infrastructure.Processing;

/// <summary>
/// The V1 processing pipeline:
///   URL -> validate -> resolve -> select -> download -> failure detection -> recovery ->
///   file validation -> storage -> temporary delivery URL.
/// Recoverable failures are classified and retried with backoff rather than abandoned —
/// this is the "Automatic Recovery" pillar and the source of the retry-recovery metric.
/// </summary>
public sealed class JobProcessingService : BackgroundService
{
    private readonly ChannelJobScheduler _scheduler;
    private readonly IJobStore _store;
    private readonly IServiceProvider _services;
    private readonly DownloadJobsOptions _options;
    private readonly ILogger<JobProcessingService> _logger;

    public JobProcessingService(
        ChannelJobScheduler scheduler,
        IJobStore store,
        IServiceProvider services,
        Microsoft.Extensions.Options.IOptions<DownloadJobsOptions> options,
        ILogger<JobProcessingService> logger)
    {
        _scheduler = scheduler;
        _store = store;
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, Math.Max(1, _options.MaxConcurrency))
            .Select(_ => Task.Run(() => WorkerLoopAsync(stoppingToken), stoppingToken))
            .ToArray();

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var jobId = await _scheduler.Reader.ReadAsync(ct).ConfigureAwait(false);
                await ProcessJobAsync(jobId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker loop");
            }
        }
    }
    public async Task ProcessJobAsync(string jobId, CancellationToken ct)
    {
        var job = await _store.GetAsync(jobId, ct).ConfigureAwait(false);
        if (job is null || job.IsTerminal) return;

        using var scope = _services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IMediaProvider>();
        var metrics = scope.ServiceProvider.GetRequiredService<IJobMetrics>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var startedAt = DateTimeOffset.UtcNow;

        // Per-job time budget (enforced) — a hung provider call can no longer pin a worker.
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        jobCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.JobTimeoutSeconds)));

        try
        {
            job.MarkProcessing();
            job.SetStage(JobStage.Resolving);
            job.RecordEvent($"Resolution started (provider: {provider.Name})");
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var formats = await provider.ResolveFormatsAsync(job.Url, jobCts.Token).ConfigureAwait(false);
            metrics.RecordResolution(formats.Count > 0);
            if (formats.Count == 0)
            {
                Fail(job, metrics, JobErrorCode.VideoUnavailable,
                    "No downloadable media found for this URL.", retryable: false);
                return;
            }

            job.SetFormats(formats);
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var selected = MediaPlan.Select(formats, job.RequestedFormat, job.RequestedQuality);
            if (selected is null)
            {
                Fail(job, metrics, JobErrorCode.QualityUnavailable,
                    "No format matched the requested quality.", retryable: false);
                return;
            }

            job.SelectOption(selected);
            job.RecordEvent($"Selected {selected.Label} {selected.Container} (format id {selected.Id})");
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var media = await DownloadWithRecoveryAsync(job, provider, metrics, selected, jobCts).ConfigureAwait(false);
            if (media is null) return;

            job.SetStage(JobStage.Validating);
            job.SetMessage("Validating output");
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var validationError = Validate(media, selected.EstimatedBytes);
            if (validationError is not null)
            {
                Fail(job, metrics, validationError.Value.Code, validationError.Value.Message, retryable: true);
                return;
            }
            job.RecordEvent($"Output validated ({media.Bytes.LongLength} bytes, {media.ContentType})");

            job.SetStage(JobStage.Finalizing);
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var expires = DateTimeOffset.UtcNow.AddMinutes(_options.OutputRetentionMinutes);
            await using (var output = new MemoryStream(media.Bytes, writable: false))
            {
                await storage.StoreAsync(job.JobId, output, media.ContentType, media.FileName, expires, jobCts.Token)
                    .ConfigureAwait(false);
            }

            var downloadUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/v1/download/{job.JobId}/content";
            job.Complete(downloadUrl, media.FileName, media.ContentType, media.Bytes.LongLength, expires);

            metrics.RecordJobCompleted((DateTimeOffset.UtcNow - (job.StartedAtUtc ?? startedAt)).TotalSeconds);
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            _logger.LogInformation("Job {JobId} completed in {Seconds:F1}s via {Provider}",
                job.JobId, (DateTimeOffset.UtcNow - startedAt).TotalSeconds, provider.Name);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            Fail(job, metrics, JobErrorCode.TimedOut,
                $"The job exceeded its processing time budget ({_options.JobTimeoutSeconds}s).", retryable: true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Host shutting down during job {JobId}; job left active for restart", jobId);
        }
        catch (MediaOperationException ex)
        {
            metrics.RecordResolution(false);
            Fail(job, metrics, ex.Code, ex.Message, ex.Retryable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            Fail(job, metrics, JobErrorCode.InternalError,
                "An unexpected error occurred while processing this job.", retryable: true);
        }
    }

    private async Task<DownloadedMedia?> DownloadWithRecoveryAsync(
        DownloadJob job, IMediaProvider provider, IJobMetrics metrics, MediaFormatOption selected,
        CancellationTokenSource jobCts)
    {
        var ct = jobCts.Token;
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var delayMs = 250;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // A cancelled job must stop work immediately.
            var fresh = await _store.GetAsync(job.JobId, ct).ConfigureAwait(false);
            if (fresh is null || fresh.IsTerminal) return null;

            job.RegisterAttempt();
            job.SetProgress(0);
            job.SetMessage($"Downloading (attempt {attempt}/{maxAttempts})");
            job.RecordEvent($"Download attempt {attempt}/{maxAttempts} started");
            await _store.SaveAsync(job, ct).ConfigureAwait(false);

            var progress = new Progress<int>(p =>
            {
                job.SetProgress(p, EtaRemaining(p));
                _ = _store.SaveAsync(job, CancellationToken.None);

                // Cancelled while downloading? Stop the provider mid-flight.
                _ = WatchForExternalCancelAsync(job.JobId, jobCts);
            });

            try
            {
                var media = await provider.DownloadAsync(job.Url, selected, progress, ct).ConfigureAwait(false);
                metrics.RecordDownload(true);
                return media;
            }
            catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
            {
                throw; // timeout or external cancel — handled by caller
            }
            catch (MediaOperationException ex)
            {
                metrics.RecordDownload(false);

                var canRetry = ex.Retryable && attempt < maxAttempts;
                metrics.RecordRetryRecovered(canRetry);
                job.RecordEvent($"Attempt {attempt} failed ({ex.Code}): {ex.Message}" +
                                (canRetry ? " — retrying" : " — no further retries"));

                if (!canRetry)
                {
                    Fail(job, metrics, ex.Code, ex.Message, ex.Retryable);
                    return null;
                }

                _logger.LogWarning("Job {JobId} attempt {Attempt} failed ({Code}); retrying",
                    job.JobId, attempt, ex.Code);

                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
                delayMs *= 2;
            }
        }

        return null;
    }

    private async Task WatchForExternalCancelAsync(string jobId, CancellationTokenSource jobCts)
    {
        var fresh = await _store.GetAsync(jobId, CancellationToken.None).ConfigureAwait(false);
        if (fresh is { IsTerminal: true }) jobCts.Cancel();
    }

    private static int? EtaRemaining(int progress) =>
        progress <= 0 ? null : Math.Max(1, (int)(100.0 / progress * 0.6));

    private static (JobErrorCode Code, string Message)? Validate(DownloadedMedia media, long? estimatedBytes)
    {
        if (media.Bytes.Length == 0)
            return (JobErrorCode.ValidationFailed, "The downloaded output was empty.");
        if (media.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return (JobErrorCode.ValidationFailed, "The provider returned a non-media response.");
        if (estimatedBytes is { } expected && expected > 0)
        {
            // Truncation guard: accept within an order of magnitude of the estimated size.
            var ratio = media.Bytes.LongLength / (double)expected;
            if (ratio < 0.05)
                return (JobErrorCode.ValidationFailed,
                    $"Output appears truncated ({media.Bytes.LongLength} of ~{expected} bytes).");
        }
        return null;
    }

    private void Fail(DownloadJob job, IJobMetrics metrics, JobErrorCode code, string message, bool retryable)
    {
        job.Fail(new JobError(code, message, retryable));
        metrics.RecordJobFailed();
        _ = _store.SaveAsync(job, CancellationToken.None);
        _logger.LogWarning("Job {JobId} failed with {Code}: {Message}", job.JobId, code, message);
    }
}