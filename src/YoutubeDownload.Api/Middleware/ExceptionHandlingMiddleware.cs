using System.Text.Json;
using YoutubeDownload.Application.Common;

namespace YoutubeDownload.Api.Middleware;

/// <summary>
/// Maps expected application exceptions onto structured JSON error bodies so customers
/// always receive predictable, machine-readable failures instead of stack traces.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var (status, code, message) = ex switch
            {
                ValidationException v => (400, "INVALID_REQUEST", v.Message),
                NotFoundException nf => (404, "NOT_FOUND", nf.Message),
                ConflictException c => (409, "CONFLICT", c.Message),
                _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.")
            };

            if (status >= 500)
                _logger.LogError(ex, "Unhandled exception while handling {Path}", context.Request.Path);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code, message }
            }), context.RequestAborted).ConfigureAwait(false);
        }
    }
}