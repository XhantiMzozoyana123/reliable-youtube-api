using System.Text.Json;
using YoutubeDownload.Domain.Entities;
using YoutubeDownload.Domain.Enums;
using Xunit;

namespace YoutubeDownload.Tests;

public class FileJobStoreTests
{
    [Fact]
    public async Task Save_Get_RoundTripsFullState()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ytdl-tests", Guid.NewGuid().ToString("N"));
        var store = new YoutubeDownload.Infrastructure.Persistence.FileJobStore(dir);
        try
        {
            var job = new DownloadJob("job_rt1", "https://youtu.be/x", "acct", "mp4", "720p",
                DateTimeOffset.UtcNow, "req_rt1");
            await store.SaveAsync(job);

            job.MarkProcessing();
            job.SetFormats([new("22", Domain.Enums.MediaFormat.Mp4, "720p", 720, "mp4", 5_000_000)]);
            job.SelectOption(new("22", Domain.Enums.MediaFormat.Mp4, "720p", 720, "mp4", 5_000_000));
            job.SetProgress(50, 10);
            job.RecordEvent("checkpoint");
            job.Complete("http://t/c", "f.mp4", "video/mp4", 999, DateTimeOffset.UtcNow.AddMinutes(1));
            await store.SaveAsync(job);

            var restored = await store.GetAsync("job_rt1");

            Assert.NotNull(restored);
            Assert.Equal(JobStatus.Completed, restored!.Status);
            Assert.Equal("req_rt1", restored.RequestId);
            Assert.Equal("acct", restored.AccountId);
            Assert.Equal(100, restored.Progress); // Complete() clamps progress to 100
            Assert.Equal(999, restored.FileBytes);
            Assert.Equal("f.mp4", restored.FileName);
            Assert.Single(restored.UtilizedFormatOptions);
            Assert.Equal(4, restored.Events.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}