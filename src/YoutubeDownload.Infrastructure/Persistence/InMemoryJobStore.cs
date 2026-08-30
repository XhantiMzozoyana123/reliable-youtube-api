using System.Collections.Concurrent;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Infrastructure.Persistence;

/// <summary>
/// Thread-safe in-memory job store. Deliberately simple for V1/RapidAPI launch;
/// replace with a durable implementation without changing the application layer.
/// </summary>
public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new(StringComparer.Ordinal);

    public Task SaveAsync(DownloadJob job, CancellationToken ct = default)
    {
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }

    public Task<DownloadJob?> GetAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(_jobs.TryGetValue(jobId, out var job) ? job : null);

    public Task<IReadOnlyList<DownloadJob>> ListActiveAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DownloadJob>>(_jobs.Values.Where(j => j.IsActive).ToList());

    public Task RemoveAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }
}