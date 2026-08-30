using YoutubeDownload.Application.Dtos;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Application.Features.Jobs;

/// <summary>
/// The V1 application surface: create, inspect, list formats, cancel and retrieve output.
/// </summary>
public interface IDownloadJobsService
{
    Task<CreateDownloadJobResponse> CreateAsync(CreateDownloadJobRequest request, string? accountId, string? requestId, CancellationToken ct = default);
    Task<JobStatusResponse> GetStatusAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<MediaFormatOptionDto>> GetFormatsAsync(string jobId, CancellationToken ct = default);
    Task<JobStatusResponse> CancelAsync(string jobId, CancellationToken ct = default);
    Task<(Stream Content, StoredFileInfo Info)> GetContentAsync(string jobId, CancellationToken ct = default);
}