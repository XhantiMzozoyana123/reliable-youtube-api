using YoutubeDownload.Domain.Entities;

namespace YoutubeDownload.Application.Ports;

/// <summary>
/// Persistence contract for download jobs. Implemented by an in-memory store in V1;
/// swappable for a durable store (e.g. Postgres/Redis) without touching the application layer.
/// </summary>
public interface IJobStore
{
    Task SaveAsync(DownloadJob job, CancellationToken ct = default);
    Task<DownloadJob?> GetAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadJob>> ListActiveAsync(CancellationToken ct = default);
    Task RemoveAsync(string jobId, CancellationToken ct = default);
}