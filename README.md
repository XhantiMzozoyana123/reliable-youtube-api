# Reliable & Fast YouTube Video Download API

ASP.NET Core implementation of the V1 business specification, using **Clean Architecture**.

## Architecture

```
YoutubeDownload.Api              Presentation: controllers, middleware, OpenAPI, DI wiring
YoutubeDownload.Application      Use cases, DTOs, ports (interfaces) — no framework deps
YoutubeDownload.Domain           Entities, enums, state machine — zero dependencies
YoutubeDownload.Infrastructure   Ports implementations: job store, queue, workers,
                                 media providers, storage, metrics
```

Dependency rule: `Api -> Infrastructure -> Application -> Domain`.

## V1 API surface

| Method | Route | Purpose |
|---|---|---|
| POST | `/v1/download` | Create a download job (returns `202` + `jobId`) |
| GET | `/v1/download/{jobId}` | Job state: status, stage, progress, eta, error |
| GET | `/v1/download/{jobId}/formats` | Available formats/qualities discovered at resolve stage |
| DELETE | `/v1/download/{jobId}` | Cancel an active job |
| GET | `/v1/download/{jobId}/content` | Temporary download URL target (expires) |
| GET | `/v1/account/usage` | Reliability metrics (resolution/download success, retry-recovery rate, P95) |
| GET | `/health` | Liveness probe |

Example:

```bash
curl -X POST http://localhost:5000/v1/download \
  -H "Content-Type: application/json" \
  -d '{"url":"https://www.youtube.com/watch?v=dQw4w9WgXcQ","format":"mp4","quality":"720p"}'
# -> 202 {"jobId":"job_4f3a438375","status":"Queued","statusUrl":"/v1/download/job_4f3a438375"}

curl http://localhost:5000/v1/download/job_4f3a438375
# -> {"status":"Completed","progress":100,"downloadUrl":".../content","fileBytes":980000,...}
```

Error responses are always structured and machine-readable:

```json
{ "status": "Failed",
  "error": { "code": "VideoUnavailable", "message": "The requested media is unavailable...", "retryable": false } }
```

## Processing pipeline (job state machine)

```
Queued -> Processing(Resolving -> Downloading -> Validating -> Finalizing) -> Completed
                                                     |-> Failed / Cancelled
```

Recoverable failures are classified and retried with exponential backoff (`MaxAttempts`).
The retry-recovery rate is tracked in `/v1/account/usage`.

## Media providers

- **Simulated** (default): deterministic, dependency-free; exercises the full pipeline.
  URLs containing `unavailable` simulate `VIDEO_UNAVAILABLE`; URLs containing `flaky`
  fail the first download attempt to demonstrate automatic recovery.
- **YtDlp** (real retrieval): set `DownloadJobs:Provider=YtDlp` and install `yt-dlp`.
  Requires yt-dlp maintenance — which is exactly what the API abstracts from customers.

## Configuration (appsettings.json)

```json
"DownloadJobs": {
  "PublicBaseUrl": "http://localhost:5000",
  "OutputRetentionMinutes": 60,
  "OutputDirectory": "App_Data/jobs",
  "Persistence": "FileSystem",     // durable: job JSON + output files on disk; "Memory" = ephemeral
  "MaxAttempts": 3,
  "JobTimeoutSeconds": 300,        // enforced per-job processing budget
  "MaxConcurrency": 4,
  "Provider": "Simulated"
},
"Authentication": {
  "Enabled": false,
  "Mode": "Keys",                  // or "Gateway" to verify X-RapidAPI-Proxy-Secret behind RapidAPI
  "AllowedKeys": [],
  "RapidApiProxySecret": ""
}
```

## Production hardening included

- **Durable persistence** — `FileJobStore` (atomic JSON per job) and `FileSystemFileStorage`
  (streamed output + metadata sidecar); jobs and outputs survive restarts. Behind the same
  ports as the in-memory implementations.
- **Streaming delivery** — outputs are stored/read as streams; `/content` supports range requests.
- **Enforced job timeout** — hung providers fail with `TimedOut` instead of pinning workers.
- **Periodic cleanup** — expired outputs and temp artifacts are evicted every 2 minutes.
- **Cancellation propagation** — cancelling a job aborts an in-flight download, not just the next retry.
- **Customer identity** — `X-RapidAPI-User` (or `X-Api-Key`) is captured as `AccountId`.
- **Truncation guard** — output far below the estimated size fails `ValidationFailed`.
- **Request IDs + job timeline** — every job carries `requestId` and an event timeline
  (created → resolving → selected format → attempts → outcome) for §17-style support answers.
- **Gateway hardening** — `Authentication:Mode=Gateway` rejects requests that bypass the RapidAPI gateway.

## Tests

24 xUnit tests cover the quality/format planner, the job state machine, the processing pipeline
(happy path, non-retryable failure, transient recovery, timeout, cancellation, truncation) and
the durable store round-trip:

```bash
dotnet test YoutubeDownload.Api.slnx
```

## Run

```bash
dotnet run --project src/YoutubeDownload.Api --urls http://localhost:5000
```
