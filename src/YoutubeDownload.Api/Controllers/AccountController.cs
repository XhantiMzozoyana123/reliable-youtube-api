using Microsoft.AspNetCore.Mvc;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Api.Controllers;

/// <summary>
/// Account & reliability surfaces. V1 exposes the reliability snapshot internally;
/// these map to the planned  GET /v1/account/usage  and  GET /v1/account/limits.
/// </summary>
[ApiController]
[Route("v1")]
[Produces("application/json")]
public sealed class AccountController : ControllerBase
{
    private readonly IJobMetrics _metrics;

    public AccountController(IJobMetrics metrics) => _metrics = metrics;

    /// <summary>Reliability telemetry: success rates, retry recovery rate and processing time percentiles.</summary>
    [HttpGet("account/usage")]
    [ProducesResponseType(typeof(ReliabilitySnapshot), StatusCodes.Status200OK)]
    public IActionResult GetUsage() => Ok(_metrics.Snapshot());

    /// <summary>Liveness probe.</summary>
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy", utc = DateTimeOffset.UtcNow });
}