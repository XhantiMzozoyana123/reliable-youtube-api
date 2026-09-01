using YoutubeDownload.Api.Models;

namespace YoutubeDownload.Api.Controllers
{
    public sealed partial class DocsController
    {
        private static EndpointDoc BuildContent() => new()
        {
            Title = "Download Media Content",
            HttpMethod = "GET",
            Route = "/v1/download/{jobId}/content",
            AuthRequired = true,
            Overview = "Streams the completed output file as raw bytes. Range requests are supported for resumable downloads. The URL is temporary — see expiresAtUtc in the job status.",
            RequestHeaders = { H("X-RapidAPI-Key"), H("X-RapidAPI-Host") },
            PathParameters = { new DocField("jobId", "string", true, "A Completed job whose output is still within retention.", "job_4f3a438375") },
            Responses = {
                new EndpointResponse("200 OK", "Binary file stream. Headers: Content-Type, Content-Disposition (filename), Content-Length, Accept-Ranges=bytes, Last-Modified.", null, "binary"),
                new EndpointResponse("404 NOT_FOUND", "Job not completed, output expired, or no stored file.",
                    "{ \"error\": { \"code\": \"NOT_FOUND\", \"message\": \"The output for job 'job_xxx' has expired.\" } }"),
                new EndpointResponse("409 CONFLICT", "Job exists but has not completed yet.",
                    "{ \"error\": { \"code\": \"CONFLICT\", \"message\": \"Job 'job_xxx' has not completed yet (status: Processing).\" } }")
            },
            CurlExample = Curl("GET", "/v1/download/job_4f3a438375/content"),
            Notes = "Recommended: append -OJ (follow redirects, use Content-Disposition filename) to save directly to disk."
        };

        private static EndpointDoc BuildAccountUsage() => new()
        {
            Title = "Get Reliability Metrics",
            HttpMethod = "GET",
            Route = "/v1/account/usage",
            AuthRequired = true,
            Overview = "A point-in-time snapshot of reliability telemetry — the numbers behind claims like 'X% of transient failures are automatically recovered' rather than a generic 'reliable'.",
            RequestHeaders = { H("X-RapidAPI-Key"), H("X-RapidAPI-Host") },
            Responses = {
                new EndpointResponse("200 OK", "ReliabilitySnapshot object.",
                    "{ \"jobsCreated\": 1423, \"jobsCompleted\": 1310, \"jobsFailed\": 95, \"retryRecoveries\": 78, \"recoveryAttempts\": 113, \"averageProcessingSeconds\": 12.405, \"p95ProcessingSeconds\": 28.703, \"retryRecoveryRatePercent\": 69.02 }")
            },
            CurlExample = Curl("GET", "/v1/account/usage")
        };

        private static EndpointDoc BuildHealth() => new()
        {
            Title = "Health Check",
            HttpMethod = "GET",
            Route = "/health",
            AuthRequired = false,
            Overview = "Liveness probe. Does not require an API key. Suitable for load balancers and uptime monitoring.",
            RequestHeaders = { },
            Responses = {
                new EndpointResponse("200 OK", "Service is healthy.", "{ \"status\": \"healthy\", \"utc\": \"2025-09-01T11:04:32Z\" }")
            },
            CurlExample = Curl("GET", "/health")
        };
    }
}
