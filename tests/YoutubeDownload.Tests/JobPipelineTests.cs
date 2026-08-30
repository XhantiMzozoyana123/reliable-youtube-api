using YoutubeDownload.Application.Common;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;
using Xunit;

namespace YoutubeDownload.Tests;

public class JobPipelineTests
{
    private static DownloadJob SeedJob(FakeJobStore store, string url = "https://youtube.com/watch?v=ok", string? quality = "720p")
    {
        var job = new DownloadJob("job_t1", url, "acct", "mp4", quality, DateTimeOffset.UtcNow);
        store.Jobs[job.JobId] = job;
        return job;
    }

    [Fact]
    public async Task HappyPath_Completes_AndStoresOutput()
    {
        var (processor, store, _, storage) = PipelineFactory.Create();
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var done = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Completed, done.Status);
        Assert.Equal(720, done.SelectedOption?.Height);
        Assert.True(done.FileBytes > 0);
        Assert.NotNull(done.DownloadUrl);
        Assert.NotNull(await storage.GetInfoAsync(job.JobId));
        Assert.Contains(done.Events, e => e.Message.Contains("Output validated"));
    }

    [Fact]
    public async Task UnavailableMedia_Fails_WithVideoUnavailable_NotRetryable()
    {
        var (processor, store, provider, _) = PipelineFactory.Create();
        provider.ResolveFailure = new MediaOperationException(
            JobErrorCode.VideoUnavailable, "The requested media is unavailable.", false);
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var failed = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.Equal(JobErrorCode.VideoUnavailable, failed.Error?.Code);
        Assert.False(failed.Error?.Retryable);
    }

    [Fact]
    public async Task TransientFailure_IsRetried_AndRecovers()
    {
        var (processor, store, provider, _) = PipelineFactory.Create();
        provider.TransientFailuresRemaining = 1; // fail first attempt only
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var done = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Completed, done.Status);
        Assert.Equal(2, done.Attempts); // failed once, recovered on retry
        Assert.Contains(done.Events, e => e.Message.Contains("retrying"));
    }

    [Fact]
    public async Task NonRetryableDownloadFailure_FailsImmediately()
    {
        var (processor, store, provider, _) = PipelineFactory.Create();
        provider.DownloadFailure = new MediaOperationException(
            JobErrorCode.FormatUnavailable, "no such format", false);
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var failed = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.Equal(JobErrorCode.FormatUnavailable, failed.Error?.Code);
        Assert.Equal(1, failed.Attempts);
    }

    [Fact]
    public async Task CancelledJob_StopsProcessing()
    {
        var (processor, store, provider, _) = PipelineFactory.Create();
        var job = SeedJob(store);
        job.Cancel("Cancelled by caller");

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        Assert.Equal(JobStatus.Cancelled, store.Jobs[job.JobId].Status);
        Assert.Equal(0, provider.DownloadCalls);
    }

    [Fact]
    public async Task TruncatedOutput_FailsValidation()
    {
        var (processor, store, provider, _) = PipelineFactory.Create();
        provider.OutputBytes = 10; // way below the 5MB estimate
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var failed = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.Equal(JobErrorCode.ValidationFailed, failed.Error?.Code);
        Assert.Contains("truncated", failed.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JobTimeout_IsEnforced_AndClassified()
    {
        var (processor, store, provider, _) = PipelineFactory.Create(o => o.JobTimeoutSeconds = 1);
        provider.HangOnDownload = true; // hangs until the job budget cancels it
        var job = SeedJob(store);

        await processor.ProcessJobAsync(job.JobId, CancellationToken.None);

        var timedOut = store.Jobs[job.JobId];
        Assert.Equal(JobStatus.Failed, timedOut.Status);
        Assert.Equal(JobErrorCode.TimedOut, timedOut.Error?.Code);
    }
}
