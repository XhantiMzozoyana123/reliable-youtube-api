using System.Collections.Concurrent;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Infrastructure.Storage;

/// <summary>
/// In-memory temporary output storage with expiry (for tests/demo only — outputs are
/// buffered in memory). Prefer FileSystemFileStorage in production.
/// </summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private sealed record Entry(MemoryStream Content, StoredFileInfo Info, DateTimeOffset ExpiresAtUtc);
    private readonly ConcurrentDictionary<string, Entry> _files = new(StringComparer.Ordinal);

    public async Task StoreAsync(string jobId, Stream content, string contentType, string fileName, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;
        _files[jobId] = new Entry(ms, new StoredFileInfo(fileName, contentType, ms.Length), expiresAtUtc);
    }

    public Task<(Stream Content, StoredFileInfo Info)?> OpenReadAsync(string jobId, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(jobId, out var entry) || entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _files.TryRemove(jobId, out _);
            return Task.FromResult<(Stream, StoredFileInfo)?> (null);
        }
        var copy = new MemoryStream(entry.Content.ToArray(), writable: false);
        return Task.FromResult<(Stream, StoredFileInfo)?>((copy, entry.Info));
    }

    public Task<StoredFileInfo?> GetInfoAsync(string jobId, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(jobId, out var entry) || entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Task.FromResult<StoredFileInfo?>(null);
        return Task.FromResult<StoredFileInfo?>(entry.Info);
    }

    public Task DeleteAsync(string jobId, CancellationToken ct = default)
    {
        if (_files.TryRemove(jobId, out var entry)) entry.Content.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Removes all expired entries. Called by the periodic cleanup service.</summary>
    public int RemoveExpired()
    {
        var removed = 0;
        foreach (var (jobId, entry) in _files)
        {
            if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow) continue;
            if (_files.TryRemove(jobId, out var removedEntry))
            {
                removedEntry.Content.Dispose();
                removed++;
            }
        }
        return removed;
    }
}