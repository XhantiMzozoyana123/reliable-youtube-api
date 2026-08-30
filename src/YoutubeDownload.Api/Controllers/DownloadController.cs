using Microsoft.AspNetCore.Mvc;
using YoutubeDownload.Application.Dtos;
using YoutubeDownload.Application.Features.Jobs;

namespace YoutubeDownload.Api.Controllers;

/// <summary>
/// V1 public API surface (business spec §8).
/// Every mutating operation is asynchronous: create a job, poll it, fetch the result.
/// </summary>
[ApiController]
[Route("v1/download")]
[Produces("application/json")]
public sealed class DownloadController : ControllerBase
{
    private readonly IDownloadJobsService _jobs;

    public DownloadController(IDownloadJobsService jobs) => _jobs = jobs;

    /// <summary>Creates an asynchronous download job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateDownloadJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDownloadJobRequest request, CancellationToken ct)
    {
        // RapidAPI forwards the subscriber identity; self-hosted callers can use X-Api-Key.
        var accountId = Request.Headers["X-RapidAPI-User"].FirstOrDefault()
                        ?? Request.Headers["X-Api-Key"].FirstOrDefault();
        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault();

        var result = await _jobs.CreateAsync(request, accountId, requestId, ct).ConfigureAwait(false);
        return Accepted(result.StatusUrl, result);
    }

    /// <summary>Returns the current state of a job, including stage, progress and any error detail.</summary>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string jobId, CancellationToken ct) =>
        Ok(await _jobs.GetStatusAsync(jobId, ct).ConfigureAwait(false));

    /// <summary>Returns the formats/qualities discovered for this media during resolution.</summary>
    [HttpGet("{jobId}/formats")]
    [ProducesResponseType(typeof(IReadOnlyList<MediaFormatOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFormats(string jobId, CancellationToken ct) =>
        Ok(await _jobs.GetFormatsAsync(jobId, ct).ConfigureAwait(false));

    /// <summary>Cancels an active job where possible.</summary>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(string jobId, CancellationToken ct) =>
        Ok(await _jobs.CancelAsync(jobId, ct).ConfigureAwait(false));

    /// <summary>Streams the completed output while its temporary URL is still valid.</summary>
    [HttpGet("{jobId}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetContent(string jobId, CancellationToken ct)
    {
        var (stream, info) = await _jobs.GetContentAsync(jobId, ct).ConfigureAwait(false);
        Response.Headers.ContentDisposition = $"attachment; filename=\"{info.FileName}\"";
        return File(stream, info.ContentType, enableRangeProcessing: true);
    }
}