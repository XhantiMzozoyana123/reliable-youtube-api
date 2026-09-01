using YoutubeDownload.Api.Models;

namespace YoutubeDownload.Api.Controllers
{
    public sealed partial class DocsController
    {
        private static EndpointDoc BuildFormats() => new()
        {
            Title = "List Available Formats",
            HttpMethod = "GET",
            Route = "/v1/download/{jobId}/formats",
            AuthRequired = true,
            Overview = "Returns the format/quality variants discovered during the Resolving stage. Convenience accessor — formats are also included in the job status response once resolved.",
            RequestHeaders = { H("X-RapidAPI-Key"), H("X-RapidAPI-Host") },
            PathParameters = { new DocField("jobId", "string", true, "The job id returned from POST /v1/download.", "job_4f3a438375") },
            Responses = {
                new EndpointResponse("200 OK", "Array of MediaFormatOptionDto.",
                    "[ { \"id\": \"22\", \"container\": \"Mp4\", \"label\": \"720p\", \"height\": 720, \"extension\": \"mp4\", \"estimatedBytes\": 24500000 } ]"),
                new EndpointResponse("400 / 404", "Invalid jobId or job not found.",
                    "{ \"error\": { \"code\": \"NOT_FOUND\", \"message\": \"Job 'job_xxx' was not found.\" } }")
            },
            CurlExample = Curl("GET", "/v1/download/job_4f3a438375/formats"),
            Notes = "Selection never upscales or transcodes: given a requested quality, the highest available resolution at or below it is chosen."
        };

        private static EndpointDoc BuildCancel() => new()
        {
            Title = "Cancel a Job",
            HttpMethod = "DELETE",
            Route = "/v1/download/{jobId}",
            AuthRequired = true,
            Overview = "Cancels an active job. In-flight downloads are aborted immediately via cancellation propagation (not just the next retry).",
            RequestHeaders = { H("X-RapidAPI-Key"), H("X-RapidAPI-Host") },
            PathParameters = { new DocField("jobId", "string", true, "The job id to cancel.", "job_4f3a438375") },
            Responses = {
                new EndpointResponse("200 OK", "The full job status with status now Cancelled.",
                    "{ \"jobId\": \"job_4f3a438375\", \"status\": \"Cancelled\", \"stage\": \"Queued\", \"progress\": 0, \"attempts\": 0, \"message\": \"Cancelled by caller\" }"),
                new EndpointResponse("400 INVALID_REQUEST", "jobId missing/blank.",
                    "{ \"error\": { \"code\": \"INVALID_REQUEST\", \"message\": \"'jobId' is required.\" } }"),
                new EndpointResponse("404 NOT_FOUND", "Job not found.",
                    "{ \"error\": { \"code\": \"NOT_FOUND\", \"message\": \"Job 'job_xxx' was not found.\" } }"),
                new EndpointResponse("409 CONFLICT", "Job is already terminal and cannot be cancelled.",
                    "{ \"error\": { \"code\": \"CONFLICT\", \"message\": \"Job 'job_xxx' is already completed and cannot be cancelled.\" } }")
            },
            CurlExample = Curl("DELETE", "/v1/download/job_4f3a438375")
        };
    }
}
