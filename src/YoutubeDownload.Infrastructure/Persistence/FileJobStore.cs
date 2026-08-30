using System.Text.Json;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Infrastructure.Persistence;

/// <summary>
/// File-backed job store: one JSON document per job, written atomically (temp file + rename).
/// Jobs survive restarts. JSON round-trips the private setters via constructor + mutators.
/// Select with  DownloadJobs:Persistence = "FileSystem"; InMemoryJobStore remains available.
/// </summary>
public sealed class FileJobStore : IJobStore
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public FileJobStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    private string PathFor(string jobId) => Path.Combine(_directory, $"{jobId}.json");

    public async Task SaveAsync(DownloadJob job, CancellationToken ct = default)
    {
        var path = PathFor(job.JobId);
        // Unique temp name per write: concurrent saves (worker + progress callbacks) must not collide.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, job, Json, ct).ConfigureAwait(false);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            // A concurrent save won the race; retry once with a fresh temp file.
            await Task.Delay(25, ct).ConfigureAwait(false);
            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, job, Json, ct).ConfigureAwait(false);
            }
            File.Move(temp, path, overwrite: true);
        }
    }

    public Task<DownloadJob?> GetAsync(string jobId, CancellationToken ct = default)
    {
        var path = PathFor(jobId);
        if (!File.Exists(path)) return Task.FromResult<DownloadJob?>(null);
        try
        {
            using var fs = File.OpenRead(path);
            return Task.FromResult<DownloadJob?>(JsonSerializer.Deserialize<DownloadJob>(fs, Json));
        }
        catch (IOException)
        {
            return Task.FromResult<DownloadJob?>(null); // transient concurrent swap
        }
    }

    public Task<IReadOnlyList<DownloadJob>> ListActiveAsync(CancellationToken ct = default)
    {
        var jobs = new List<DownloadJob>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                using var fs = File.OpenRead(path);
                var job = JsonSerializer.Deserialize<DownloadJob>(fs, Json);
                if (job is not null && job.IsActive) jobs.Add(job);
            }
            catch (IOException) { /* transient */ }
        }
        return Task.FromResult<IReadOnlyList<DownloadJob>>(jobs);
    }

    public Task RemoveAsync(string jobId, CancellationToken ct = default)
    {
        var path = PathFor(jobId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}