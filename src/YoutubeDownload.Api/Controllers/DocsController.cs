using Microsoft.AspNetCore.Mvc;
using YoutubeDownload.Api.Models;

namespace YoutubeDownload.Api.Controllers
{
    /// <summary>Landing page (MVC). Renders the introduction, use cases and problems solved.</summary>
    public sealed class HomeController : Controller
    {
        public IActionResult Index() => View();
    }

    /// <summary>
    /// Documentation website (MVC). Renders Razor views only — it never touches the /v1/* Web API
    /// routes, whose behaviour is unchanged. Split into partials for maintainability.
    /// </summary>
    public sealed partial class DocsController : Controller
    {
        public IActionResult Index()
        {
            var items = new[]
            {
                new EndpointSummary("Create a Download Job", "POST",   "/v1/download",            "Submit a YouTube URL and start an async download.", nameof(CreateJob)),
                new EndpointSummary("Get Job Status",        "GET",    "/v1/download/{jobId}",    "Poll progress, stage and final result.",            nameof(JobStatus)),
                new EndpointSummary("List Formats",          "GET",    "/v1/download/{jobId}/formats", "Discover formats/qualities for the media.",     nameof(Formats)),
                new EndpointSummary("Cancel a Job",          "DELETE", "/v1/download/{jobId}",    "Cancel an active download job.",                 nameof(Cancel)),
                new EndpointSummary("Download Output",       "GET",    "/v1/download/{jobId}/content", "Stream the completed file (binary).",           nameof(Content)),
                new EndpointSummary("Reliability Metrics",   "GET",    "/v1/account/usage",       "Success/retry/percentile telemetry snapshot.",   nameof(AccountUsage)),
                new EndpointSummary("Health",                "GET",    "/health",                 "Liveness probe (no auth).",                      nameof(Health))
            };
            return View(items);
        }

        public IActionResult CreateJob()    => View("Endpoint", BuildCreateJob());
        public IActionResult JobStatus()    => View("Endpoint", BuildJobStatus());
        public IActionResult Formats()      => View("Endpoint", BuildFormats());
        public IActionResult Cancel()       => View("Endpoint", BuildCancel());
        public IActionResult Content()      => View("Endpoint", BuildContent());
        public IActionResult AccountUsage() => View("Endpoint", BuildAccountUsage());
        public IActionResult Health()       => View("Endpoint", BuildHealth());

        private static string Curl(string method, string path, string? body = null)
        {
            var s = $"curl -X {method} \"https://reliable-youtube-download.p.rapidapi.com{path}\" \\\n" +
                    "  -H \"X-RapidAPI-Key: YOUR_RAPIDAPI_KEY\" \\\n" +
                    "  -H \"X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com\"";
            if (body != null) s += "\n  -H \"Content-Type: application/json\" \\\n  -d " + body;
            return s;
        }

        private static DocField H(string name) => new(name, "string", false, "Standard RapidAPI gateway header.", null);
    }
}
