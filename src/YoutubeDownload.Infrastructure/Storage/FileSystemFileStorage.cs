using System.Text.Json;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Infrastructure.Storage;

/// <summary>
/// File-system temporary output storage (durable across restarts, memory-bounded):
/// one output file per job under OutputDirectory, plus a small metadata sidecar holding
/// content type / expiry. Expired files are removed by the periodic cleanup service.
/// </summary>
public sealed class FileSystemFileStorage : IFileStorage
{
    private sealed record Meta(string FileName, string ContentType, DateTimeOffset ExpiresAtUtc);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _directory;

    public FileSystemFileStorage(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    private string DataPath(string jobId) => Path.Combine(_directory, $"{jobId}.bin");
    private string MetaPath(string jobId) => Path.Combine(_directory, $"{jobId}.meta.json");

    public async Task StoreAsync(string jobId, Stream content, string contentType, string fileName, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        var dataPath = DataPath(jobId);
        var temp = dataPath + ".tmp";
        await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
        File.Move(temp, dataPath, overwrite: true);
        await File.WriteAllTextAsync(MetaPath(jobId),
            JsonSerializer.Serialize(new Meta(fileName, contentType, expiresAtUtc), Json), ct).ConfigureAwait(false);
    }

    public async Task<(Stream Content, StoredFileInfo Info)?> OpenReadAsync(string jobId, CancellationToken ct = default)
    {
        var info = await GetInfoAsync(jobId, ct).ConfigureAwait(false);
        if (info is null) return null;
        var stream = new FileStream(DataPath(jobId), FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, info);
    }

    public Task<StoredFileInfo?> GetInfoAsync(string jobId, CancellationToken ct = default)
    {
        var metaPath = MetaPath(jobId);
        if (!File.Exists(metaPath)) return Task.FromResult<StoredFileInfo?>(null);
        var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(metaPath), Json);
        if (meta is null || meta.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            Delete(jobId);
            return Task.FromResult<StoredFileInfo?>(null);
        }
        var length = File.Exists(DataPath(jobId)) ? new FileInfo(DataPath(jobId)).Length : 0;
        return Task.FromResult<StoredFileInfo?>(new StoredFileInfo(meta.FileName, meta.ContentType, length));
    }

    public Task DeleteAsync(string jobId, CancellationToken ct = default)
    {
        Delete(jobId);
        return Task.CompletedTask;
    }

    private void Delete(string jobId)
    {
        if (File.Exists(DataPath(jobId))) File.Delete(DataPath(jobId));
        if (File.Exists(MetaPath(jobId))) File.Delete(MetaPath(jobId));
    }

    /// <summary>Removes all expired output files. Called by the periodic cleanup service.</summary>
    public int RemoveExpired()
    {
        var removed = 0;
        foreach (var metaPath in Directory.EnumerateFiles(_directory, "*.meta.json"))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(metaPath), Json);
                if (meta is null || meta.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                {
                    var jobId = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(metaPath));
                    Delete(jobId);
                    removed++;
                }
            }
            catch (IOException) { /* in use; skip */ }
        }
        return removed;
    }
}