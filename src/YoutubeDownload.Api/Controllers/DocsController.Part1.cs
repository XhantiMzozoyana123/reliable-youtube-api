using YoutubeDownload.Api.Models;

namespace YoutubeDownload.Api.Controllers
{
    public sealed partial class DocsController
    {
        private static EndpointDoc BuildCreateJob() => new()
        {
            Title = "Create a Download Job",
            HttpMethod = "POST",
            Route = "/v1/download",
            AuthRequired = true,
            Overview = "Submits a YouTube URL for asynchronous download. The job is queued and processed in the background. Poll GET /v1/download/{jobId} for status, then fetch the output via GET /content once status is Completed.",
            RequestHeaders = {
                new DocField("Content-Type",    "string", true,  "Must be application/json.", null),
                new DocField("X-RapidAPI-Key",  "string", true,  "Your RapidAPI subscription key.", null),
                new DocField("X-RapidAPI-Host", "string", true,  "Your RapidAPI endpoint host.", null),
                new DocField("X-RapidAPI-User", "string", false, "Subscriber identity, captured as the job's accountId.", null),
                new DocField("X-Request-Id",    "string", false, "Correlation id echoed back on the job.", null)
            },
            RequestBody = {
                new DocField("url",     "string", true,  "A YouTube URL (youtube.com/watch, youtu.be, shorts/embed/live). Absolute http(s).", "https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
                new DocField("format",  "string", false, "Container: mp4 (default), webm, mkv, mp3, m4a, wav.", "mp4"),
                new DocField("quality", "string", false, "Max quality: 1080p, 720p, 480p, 360p. Never upscales.", "720p")
            },
            Responses = {
                new EndpointResponse("202 Accepted", "Job accepted and queued. A Location header points to the status URL.",
                    "{ \"jobId\": \"job_4f3a438375\", \"status\": \"Queued\", \"statusUrl\": \"/v1/download/job_4f3a438375\" }"),
                new EndpointResponse("400 INVALID_REQUEST", "Invalid body, URL, format or quality.",
                    "{ \"error\": { \"code\": \"INVALID_REQUEST\", \"message\": \"'url' must be a YouTube video URL ...\" } }")
            },
            CurlExample = Curl("POST", "/v1/download",
                "'{\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\",\"format\":\"mp4\",\"quality\":\"720p\"}'"),
            Notes = "Validation runs before the job is created, so malformed input never queues a job."
        };

        private static EndpointDoc BuildJobStatus() => new()
        {
            Title = "Get Job Status",
            HttpMethod = "GET",
            Route = "/v1/download/{jobId}",
            AuthRequired = true,
            Overview = "Returns the full job state: lifecycle stage, download progress (0-100), current attempt, structured error detail and a timeline of events. Poll until status is Completed, Failed or Cancelled.",
            RequestHeaders = { H("X-RapidAPI-Key"), H("X-RapidAPI-Host") },
            PathParameters = { new DocField("jobId", "string", true, "The job id returned from POST /v1/download.", "job_4f3a438375") },
            Responses = {
                new EndpointResponse("200 OK", "Full job status (JobStatusResponse). Null fields are omitted from the JSON.",
                    "{ \"jobId\": \"job_4f3a438375\", \"status\": \"Completed\", \"stage\": \"Finalizing\", \"progress\": 100, \"attempts\": 2, \"downloadUrl\": \"https://.../content\", \"fileName\": \"dQw4w9WgXcQ_720p.mp4\", \"contentType\": \"video/mp4\", \"fileBytes\": 3062500, \"expiresAtUtc\": \"2025-09-01T12:04:32Z\", \"timeline\": [ { \"atUtc\": \"2025-09-01T11:04:32Z\", \"message\": \"Job created and queued\" } ] }"),
                new EndpointResponse("400 INVALID_REQUEST", "jobId missing/blank.",
                    "{ \"error\": { \"code\": \"INVALID_REQUEST\", \"message\": \"'jobId' is required.\" } }"),
                new EndpointResponse("404 NOT_FOUND", "No job with the given id.",
                    "{ \"error\": { \"code\": \"NOT_FOUND\", \"message\": \"Job 'job_xxx' was not found.\" } }")
            },
            CurlExample = Curl("GET", "/v1/download/job_4f3a438375")
        };
    }
}
