using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;
using YoutubeDownload.Infrastructure.Processing;
using YoutubeDownload.Infrastructure.Storage;

namespace YoutubeDownload.Tests;

/// <summary>Shared fakes and a configured pipeline for exercising ProcessJobAsync directly.</summary>
internal sealed class FakeJobStore : IJobStore
{
    public Dictionary<string, DownloadJob> Jobs { get; } = [];
    public Task SaveAsync(DownloadJob job, CancellationToken ct = default) { Jobs[job.JobId] = job; return Task.CompletedTask; }
    public Task<DownloadJob?> GetAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(Jobs.GetValueOrDefault(jobId));
    public Task<IReadOnlyList<DownloadJob>> ListActiveAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DownloadJob>>(Jobs.Values.Where(j => j.IsActive).ToList());
    public Task RemoveAsync(string jobId, CancellationToken ct = default) { Jobs.Remove(jobId); return Task.CompletedTask; }
}

internal sealed class FakeMediaProvider : IMediaProvider
{
    public string Name => "fake";
    public IReadOnlyList<MediaFormatOption>? FormatsToReturn { get; set; }
    public MediaOperationException? ResolveFailure { get; set; }
    public MediaOperationException? DownloadFailure { get; set; }
    public int TransientFailuresRemaining { get; set; }
    public int DownloadCalls { get; private set; }
    public long OutputBytes { get; set; } = 512_000;
    public bool HangOnDownload { get; set; }
    public Func<CancellationToken, OperationCanceledException>? CancelOnDownload { get; set; }

    public Task<IReadOnlyList<MediaFormatOption>> ResolveFormatsAsync(string url, CancellationToken ct = default)
    {
        if (ResolveFailure is not null) throw ResolveFailure;
        return Task.FromResult(FormatsToReturn ?? (IReadOnlyList<MediaFormatOption>)
        [
            new("18", MediaFormat.Mp4, "360p", 360, "mp4", 1_000_000),
            new("22", MediaFormat.Mp4, "720p", 720, "mp4", 5_000_000),
            new("137", MediaFormat.Mp4, "1080p", 1080, "mp4", 10_000_000)
        ]);
    }

    public async Task<DownloadedMedia> DownloadAsync(string url, MediaFormatOption option, IProgress<int> progress, CancellationToken ct = default)
    {
        DownloadCalls++;
        if (CancelOnDownload is not null) throw CancelOnDownload(ct);
        if (HangOnDownload) await Task.Delay(Timeout.Infinite, ct);
        if (TransientFailuresRemaining > 0)
        {
            TransientFailuresRemaining--;
            throw new MediaOperationException(JobErrorCode.DownloadFailed, "connection reset", true);
        }
        if (DownloadFailure is not null) throw DownloadFailure;

        for (var i = 1; i <= 5; i++) { await Task.Delay(5, ct); progress?.Report(i * 20); }

        return new DownloadedMedia(new byte[OutputBytes], $"out_{option.Label}.{option.Extension}", "video/mp4");
    }
}

internal static class PipelineFactory
{
    public static (JobProcessingService Processor, FakeJobStore Store, FakeMediaProvider Provider, InMemoryFileStorage Storage) Create(
        Action<DownloadJobsOptions>? configure = null)
    {
        var options = new DownloadJobsOptions { MaxAttempts = 3, JobTimeoutSeconds = 30, PublicBaseUrl = "http://test" };
        configure?.Invoke(options);

        var store = new FakeJobStore();
        var provider = new FakeMediaProvider();
        var storage = new InMemoryFileStorage();

        var services = new ServiceCollection();
        services.AddSingleton<IMediaProvider>(provider);
        services.AddSingleton<IFileStorage>(storage);
        services.AddSingleton<IJobMetrics, InMemoryJobMetrics>();
        var serviceProvider = services.BuildServiceProvider();

        var processor = new JobProcessingService(
            new ChannelJobScheduler(), store, serviceProvider,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<JobProcessingService>.Instance);

        return (processor, store, provider, storage);
    }
}