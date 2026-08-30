using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;
using Xunit;

namespace YoutubeDownload.Tests;

public class DownloadJobTests
{
    private static DownloadJob NewJob() =>
        new("job_1", "https://youtube.com/watch?v=x", "acct", "mp4", "720p", DateTimeOffset.UtcNow, "req_1");

    [Fact]
    public void NewJob_StartsQueued_WithRequestIdAndEvent()
    {
        var job = NewJob();
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("req_1", job.RequestId);
        Assert.Single(job.Events);
    }

    [Fact]
    public void Complete_SetsTerminalState_Url_AndExpiry()
    {
        var job = NewJob();
        job.MarkProcessing();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);

        job.Complete("http://t/content", "file.mp4", "video/mp4", 123, expires);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(100, job.Progress);
        Assert.Equal("file.mp4", job.FileName);
        Assert.True(job.IsDownloadAvailable());
        Assert.Contains(job.Events, e => e.Message.StartsWith("Completed"));
    }

    [Fact]
    public void Complete_AfterFail_IsRejected()
    {
        var job = NewJob();
        job.MarkProcessing();
        job.Fail(new JobError(JobErrorCode.VideoUnavailable, "gone", false));

        Assert.Throws<InvalidOperationException>(() =>
            job.Complete("u", "f", "t", 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Fail_AfterComplete_IsRejected()
    {
        var job = NewJob();
        job.MarkProcessing();
        job.Complete("u", "f", "t", 1, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            job.Fail(new JobError(JobErrorCode.InternalError, "boom", true)));
    }

    [Fact]
    public void Cancel_TerminalJob_IsNoOp()
    {
        var job = NewJob();
        job.MarkProcessing();
        job.Cancel();
        job.Cancel(); // idempotent

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Single(job.Events, e => e.Message == "Job cancelled by caller");
    }

    [Fact]
    public void Fail_RecordsTimelineEvent()
    {
        var job = NewJob();
        job.MarkProcessing();
        job.Fail(new JobError(JobErrorCode.DownloadFailed, "reset", true));

        Assert.Contains(job.Events, e => e.Message.Contains("Failed (DownloadFailed)"));
    }

    [Fact]
    public void MarkProcessing_AfterTerminal_IsRejected()
    {
        var job = NewJob();
        job.Cancel();
        Assert.Throws<InvalidOperationException>(job.MarkProcessing);
    }
}
