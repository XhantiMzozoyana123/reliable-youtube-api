using Microsoft.Extensions.Options;

namespace YoutubeDownload.Api.Middleware;

/// <summary>
/// Request authentication for self-hosted/backed-by-RapidAPI deployments.
/// Mode "Gateway": verify the RapidAPI proxy secret — the gateway is the only legitimate
/// caller, and this prevents bypassing it (and billing) by hitting the backend directly.
/// Mode "Keys": validate a caller-supplied key (X-RapidAPI-Key / X-Api-Key) locally.
/// Disabled entirely is fine only when the backend is otherwise unreachable.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || IsExempt(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var authorized = _options.Mode?.Equals("Gateway", StringComparison.OrdinalIgnoreCase) == true
            ? CheckProxySecret(context)
            : CheckKey(context);

        if (!authorized)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":{"code":"UNAUTHORIZED","message":"A valid API key is required."}}""")
                .ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool CheckProxySecret(HttpContext context) =>
        !string.IsNullOrEmpty(_options.RapidApiProxySecret) &&
        context.Request.Headers["X-RapidAPI-Proxy-Secret"].FirstOrDefault() == _options.RapidApiProxySecret;

    private bool CheckKey(HttpContext context)
    {
        var key = context.Request.Headers["X-RapidAPI-Key"].FirstOrDefault()
                  ?? context.Request.Headers["X-Api-Key"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(key)
               && _options.AllowedKeys.Length > 0
               && _options.AllowedKeys.Contains(key);
    }

    private static readonly PathString[] ExemptPrefixes =
        { "/health", "/openapi", "/css", "/js", "/lib", "/images", "/favicon.ico", "/home", "/docs" };

    /// <summary>
    /// Routes visible from the documentation website or serving static assets are exempt
    /// from API-key checks, mirroring /health and /openapi. API consumers still authenticate
    /// on every /v1/* and /account/* route.
    /// </summary>
    private static bool IsExempt(PathString path) =>
        path == "/" || ExemptPrefixes.Any(p => path.StartsWithSegments(p));
}

public sealed class ApiKeyOptions
{
    /// <summary>"Keys" (validate allowed keys locally) or "Gateway" (validate RapidAPI proxy secret).</summary>
    public string Mode { get; set; } = "Keys";

    public bool Enabled { get; set; }

    /// <summary>Allowed keys for Mode = "Keys".</summary>
    public string[] AllowedKeys { get; set; } = [];

    /// <summary>Shared secret for Mode = "Gateway" (copy from the RapidAPI dashboard).</summary>
    public string RapidApiProxySecret { get; set; } = "";
}
