namespace YoutubeDownload.Application.Ports;

/// <summary>Metadata for a stored output file.</summary>
public sealed record StoredFileInfo(string FileName, string ContentType, long Length);

/// <summary>
/// Temporary output storage. Files are retained only until <c>ExpiresAtUtc</c>, after which
/// they are evicted — this keeps the service as processing infrastructure, not permanent hosting.
/// Stored via streams so large outputs never have to fit in memory.
/// </summary>
public interface IFileStorage
{
    Task StoreAsync(string jobId, Stream content, string contentType, string fileName, DateTimeOffset expiresAtUtc, CancellationToken ct = default);
    Task<(Stream Content, StoredFileInfo Info)?> OpenReadAsync(string jobId, CancellationToken ct = default);
    Task<StoredFileInfo?> GetInfoAsync(string jobId, CancellationToken ct = default);
    Task DeleteAsync(string jobId, CancellationToken ct = default);
}