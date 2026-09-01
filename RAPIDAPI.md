# Reliable & Fast YouTube Video Download API — RapidAPI Documentation

> **Base URL (via RapidAPI gateway)**
> `https://reliable-youtube-download.p.rapidapi.com`
>
> ⚠️ Replace the host above with the endpoint hostname assigned to your RapidAPI
> application. All paths below are relative to that base URL.

A stateless-but-reliable YouTube media download service exposed behind the RapidAPI
gateway. Every mutating operation is **asynchronous**: you submit a download job, poll its
status until it reaches a terminal state, then fetch the output. The pipeline performs
automatic retries with exponential backoff, output validation, and per-job time budgets — so
transient failures are recovered without the caller retrying.

| Property | Value |
|---|---|
| **Protocol** | HTTPS |
| **Base path** | `/` (endpoints live under `/v1/`) |
| **Authentication** | `X-RapidAPI-Key` header (RapidAPI-managed). See [Authentication](#authentication). |
| **Response format** | `application/json` (UTF-8) for all JSON bodies |
| **Date format** | ISO 8601 with timezone, e.g. `2025-09-01T11:04:32Z` |
| **Default media provider** | `Simulated` (works out of the box). Set `DownloadJobs:Provider=YtDlp` to download real media. |

## Table of Contents

1. [Quick Start](#quick-start)
2. [Authentication](#authentication)
3. [Headers Forwarded by RapidAPI](#headers-forwarded-by-rapidapi)
4. [Endpoint Summary](#endpoint-summary)
5. [Endpoints](#endpoints)
   - [1. Create a Download Job — `POST /v1/download`](#1-create-a-download-job--post-v1download)
   - [2. Get Job Status — `GET /v1/download/{jobId}`](#2-get-job-status--get-v1downloadjobid)
   - [3. List Available Formats — `GET /v1/download/{jobId}/formats`](#3-list-available-formats--get-v1downloadjobidformats)
   - [4. Cancel a Job — `DELETE /v1/download/{jobId}`](#4-cancel-a-job--delete-v1downloadjobid)
   - [5. Download Output — `GET /v1/download/{jobId}/content`](#5-download-output--get-v1downloadjobidcontent)
   - [6. Reliability Metrics — `GET /v1/account/usage`](#6-reliability-metrics--get-v1accountusage)
   - [7. Health — `GET /health`](#7-health--get-health)
6. [Job Lifecycle (State Machine)](#job-lifecycle-state-machine)
7. [Async Polling Pattern](#async-polling-pattern)
8. [Error Handling](#error-handling)
9. [Job Error Codes Reference](#job-error-codes-reference)
10. [Data Types](#data-types)
11. [Configuration & Deployment Notes](#configuration--deployment-notes)

## Quick Start

> The examples below use `curl` with the RapidAPI gateway. Substitute
> `YOUR_RAPIDAPI_KEY` with your actual key.

**1. Create a download job.**

```bash
curl -X POST "https://reliable-youtube-download.p.rapidapi.com/v1/download" \
  -H "Content-Type: application/json" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com" \
  -d '{
        "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        "format": "mp4",
        "quality": "720p"
      }'
```

**Response (`202 Accepted`):** the job is queued for asynchronous processing.

```json
{
  "jobId": "job_4f3a438375",
  "status": "Queued",
  "statusUrl": "/v1/download/job_4f3a438375"
}
```
A `Location` header is also set to `/v1/download/job_4f3a438375`.

**2. Poll the job status** until `status` is `Completed` (or a terminal failure).

```bash
curl -X GET "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

**3. Once `Completed`, download the file** from the `downloadUrl`.

```bash
curl -L -OJ "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375/content" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

## Authentication

When you access the API **through RapidAPI**, authentication is managed by the RapidAPI
gateway:

| Header | Required | Description |
|---|---|---|
| `X-RapidAPI-Key` | Yes | Your RapidAPI subscription key. Validates that the caller has an active subscription to this listing. |
| `X-RapidAPI-Host` | Yes | Your RapidAPI endpoint host (e.g. `reliable-youtube-download.p.rapidapi.com`). Required by the RapidAPI gateway. |

### Backend gateway protection

To prevent subscribers from bypassing the RapidAPI gateway (and its billing/rate-limiting)
by calling the backend directly, the service can run in **Gateway mode**
(`Authentication:Mode = "Gateway"`): it verifies the `X-RapidAPI-Proxy-Secret` that RapidAPI
injects on behalf of every proxied request. Requests reaching the backend without the correct
shared secret are rejected with `401 UNAUTHORIZED`.

- **Behind RapidAPI (recommended):** use `Mode = "Gateway"` and set `RapidApiProxySecret` from
  the RapidAPI dashboard. `X-RapidAPI-Key` is handled entirely by RapidAPI; callers never see
  the backend directly.
- **Self-hosted (direct):** use `Mode = "Keys"` and populate `AllowedKeys` with your API keys.

> `/health` and `/openapi` are exempt from authentication.

**Invalid or missing credentials** produce:

```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "A valid API key is required."
  }
}
```

## Headers Forwarded by RapidAPI

RapidAPI enriches each proxied request with identity headers. The API captures these:

| Header | Used as |
|---|---|
| `X-RapidAPI-User` | Captured as `accountId` (the subscriber identity). Used for attribution and future per-account limits. |
| `X-Request-Id` | Optional caller-supplied correlation id, echoed back in job status for traceability. |

> If `X-RapidAPI-User` is not present (e.g. self-hosted calls), `X-Api-Key` may be supplied
> instead as an identity hint.

## Endpoint Summary

| Method | Path | Purpose | Success Code |
|---|---|---|---|
| `POST` | `/v1/download` | Create a download job (async) | `202 Accepted` |
| `GET` | `/v1/download/{jobId}` | Poll job status / progress | `200 OK` |
| `GET` | `/v1/download/{jobId}/formats` | List resolved formats/qualities | `200 OK` |
| `DELETE` | `/v1/download/{jobId}` | Cancel an active job | `200 OK` |
| `GET` | `/v1/download/{jobId}/content` | Stream the completed file | `200 OK` (binary) |
| `GET` | `/v1/account/usage` | Reliability/success metrics snapshot | `200 OK` |
| `GET` | `/health` | Liveness probe | `200 OK` |

**Note on JSON shape:** All responses use `camelCase` property names. Fields whose values are
`null` are omitted from the response body (the API serializes with "ignore null values").

### 1. Create a Download Job — `POST /v1/download`

Creates an asynchronous media download job and returns immediately. The actual download runs
in the background; poll the returned `statusUrl` until the job reaches a terminal state.

#### Request

**Headers**

| Header | Required | Value |
|---|---|---|
| `Content-Type` | Yes | `application/json` |
| `X-RapidAPI-Key` | Yes | Your RapidAPI key |
| `X-RapidAPI-Host` | Yes | Your RapidAPI host |
| `X-RapidAPI-User` | optional | Subscriber identity (forwarded by RapidAPI) |
| `X-Request-Id` | optional | Correlation id returned on the job |

**Body** — `CreateDownloadJobRequest`

| Field | Type | Required | Description |
|---|---|---|---|
| `url` | string | Yes | A YouTube video URL. Supported shapes: `youtube.com/watch?v=...`, `youtu.be/...`, `youtube.com/shorts/...`, `youtube.com/embed/...`, `youtube.com/live/...`. Must be absolute `http(s)`. |
| `format` | string | No | Desired output container. One of: **`mp4`** (default), `webm`, `mkv`, `mp3`, `m4a`, `wav`. Best-effort; if the requested container has no matching track, the highest available resolution in any container is selected. |
| `quality` | string | No | Maximum quality. Accepts `1080p`, `720p`, `480p`, `360p`, or `p1080`/`p720` style. The selector never upscales — it picks the highest resolution **at or below** the request. If the exact quality is unavailable, the nearest lower option is used. |

**Example**

```bash
curl -X POST "https://reliable-youtube-download.p.rapidapi.com/v1/download" \
  -H "Content-Type: application/json" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com" \
  -d '{
        "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        "format": "mp4",
        "quality": "720p"
      }'
```

#### Response — `202 Accepted`

A `Location` header is set to the status URL.

**Headers**

| Header | Value |
|---|---|
| `Location` | `/v1/download/{jobId}` |

**Body** — `CreateDownloadJobResponse`

| Field | Type | Description |
|---|---|---|
| `jobId` | string | The job identifier, e.g. `job_4f3a438375`. |
| `status` | string | Always `Queued` (the initial state). |
| `statusUrl` | string | Relative URL to poll, e.g. `/v1/download/job_4f3a438375`. |

**Example**

```json
{
  "jobId": "job_4f3a438375",
  "status": "Queued",
  "statusUrl": "/v1/download/job_4f3a438375"
}
```

#### Errors — `400 Bad Request`

The request body is required and the URL/format/quality are validated before a job is created.

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "'url' must be a YouTube video URL (youtube.com/watch, youtu.be, or youtube.com/shorts)."
  }
}
```

| Condition | `message` |
|---|---|
| Body missing | `A request body is required.` |
| `url` empty | `'url' is required.` |
| URL not http(s) | `'url' must be an absolute http(s) URL.` |
| Not a YouTube link | `'url' must be a YouTube video URL ...` |
| Unsupported format | `Unsupported 'format' 'xxx'. Supported: mp4, webm, mkv, mp3, m4a, wav.` |
| Unsupported quality | `Unsupported 'quality' 'xxx'. Use values like '1080p', '720p', '480p'.` |

### 2. Get Job Status — `GET /v1/download/{jobId}`

Returns the current state of a job: its lifecycle stage, progress, and any error detail.
Poll this endpoint (see [Async Polling Pattern](#async-polling-pattern)) after creating a job.

#### Request

**Path parameter**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `jobId` | string | Yes | The job id returned from `POST /v1/download`. |

**Headers**

| Header | Required |
|---|---|
| `X-RapidAPI-Key` | Yes |
| `X-RapidAPI-Host` | Yes |

```bash
curl -X GET "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

#### Response — `200 OK`

**Body** — `JobStatusResponse`. Null fields are omitted.

| Field | Type | Present when | Description |
|---|---|---|---|
| `jobId` | string | always | The job id. |
| `requestId` | string | always | Correlation id (`X-Request-Id` if supplied, else generated). |
| `status` | string | always | Top-level state. See [Job Lifecycle](#job-lifecycle-state-machine). |
| `stage` | string | always | Fine-grained processing stage (see below). |
| `progress` | integer | always | 0–100 download progress. |
| `etaSeconds` | integer\|null | when progress > 0 | Estimated seconds remaining. |
| `attempts` | integer | always | Number of download attempts so far. |
| `message` | string\|null | sometimes | Human-readable status detail. |
| `error` | object\|null | on `Failed` | Structured error. See [`error` object](#error-object). |
| `formats` | array\|null | after resolving | Discovered format options. See [List Formats](#3-list-available-formats). |
| `requestedFormat` | string\|null | if a format was requested | e.g. `mp4`. |
| `requestedQuality` | string\|null | if a quality was requested | e.g. `720p`. |
| `downloadUrl` | string\|null | on `Completed` | Temporary download URL (expires per `expiresAtUtc`). |
| `fileName` | string\|null | on `Completed` | Suggested filename for the download. |
| `contentType` | string\|null | on `Completed` | MIME type, e.g. `video/mp4`. |
| `fileBytes` | integer\|null | on `Completed` | Size of the output in bytes. |
| `expiresAtUtc` | string\|null | on `Completed` | When the `downloadUrl` expires. |
| `createdAtUtc` | string | always | Job creation time (UTC). |
| `startedAtUtc` | string\|null | when processing begins | Time the job entered `Processing`. |
| `completedAtUtc` | string\|null | terminal state | Time the job finished. |
| `updatedAtUtc` | string | always | Last state update time (UTC). |
| `timeline` | array\|null | always (once it exists) | Ordered event log. See [`timeline` entry](#timeline-entry). |

`status` values: **`Queued`**, **`Processing`**, **`Completed`**, **`Failed`**, **`Cancelled`**.

`stage` values: **`Queued`**, **`Resolving`**, **`Downloading`**, **`Validating`**, **`Finalizing`**
(the last is also reported at terminal states as the stage at which they completed/failed).

`error` object — present when `status === "Failed"`:

| Field | Type | Description |
|---|---|---|
| `code` | string | Stable machine-readable error code. See [Error Codes Reference](#job-error-codes-reference). |
| `message` | string | Human-readable explanation. |
| `retryable` | boolean | Whether the failure was classified as transient. |

`timeline` entry — one record per state transition/failure:

| Field | Type | Description |
|---|---|---|
| `atUtc` | string | UTC timestamp of the event. |
| `message` | string | Description of the transition/failure. |

#### Examples

**Queued (early poll)**

```json
{
  "jobId": "job_4f3a438375",
  "requestId": "req_9c3f7a1b2d4e",
  "status": "Queued",
  "stage": "Queued",
  "progress": 0,
  "attempts": 0,
  "requestedFormat": "mp4",
  "requestedQuality": "720p",
  "createdAtUtc": "2025-09-01T11:04:32Z",
  "updatedAtUtc": "2025-09-01T11:04:32Z",
  "timeline": [
    { "atUtc": "2025-09-01T11:04:32Z", "message": "Job created and queued" }
  ]
}
```

**Processing (downloading)**

```json
{
  "jobId": "job_4f3a438375",
  "requestId": "req_9c3f7a1b2d4e",
  "status": "Processing",
  "stage": "Downloading",
  "progress": 40,
  "etaSeconds": 3,
  "attempts": 1,
  "message": "Downloading (attempt 1/3)",
  "formats": [
    { "id": "22", "container": "Mp4", "label": "720p", "height": 720, "extension": "mp4", "estimatedBytes": 24500000 },
    { "id": "137", "container": "Mp4", "label": "1080p", "height": 1080, "extension": "mp4", "estimatedBytes": 51200000 }
  ],
  "requestedFormat": "mp4",
  "requestedQuality": "720p",
  "createdAtUtc": "2025-09-01T11:04:32Z",
  "startedAtUtc": "2025-09-01T11:04:33Z",
  "updatedAtUtc": "2025-09-01T11:04:35Z",
  "timeline": [
    { "atUtc": "2025-09-01T11:04:32Z", "message": "Job created and queued" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Processing started" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Resolution started (provider: simulated)" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Selected 720p Mp4 (format id 22)" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Download attempt 1/3 started" }
  ]
}
```

**Completed (after automatic retry recovery)** — `attempts` is `2` because the first
attempt failed transitively and was auto-recovered:

```json
{
  "jobId": "job_4f3a438375",
  "requestId": "req_9c3f7a1b2d4e",
  "status": "Completed",
  "stage": "Finalizing",
  "progress": 100,
  "etaSeconds": 0,
  "attempts": 2,
  "message": "Job completed successfully",
  "formats": [
    { "id": "22", "container": "Mp4", "label": "720p", "height": 720, "extension": "mp4", "estimatedBytes": 24500000 }
  ],
  "requestedFormat": "mp4",
  "requestedQuality": "720p",
  "downloadUrl": "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375/content",
  "fileName": "dQw4w9WgXcQ_720p.mp4",
  "contentType": "video/mp4",
  "fileBytes": 3062500,
  "expiresAtUtc": "2025-09-01T12:04:32Z",
  "createdAtUtc": "2025-09-01T11:04:32Z",
  "startedAtUtc": "2025-09-01T11:04:33Z",
  "completedAtUtc": "2025-09-01T11:04:41Z",
  "updatedAtUtc": "2025-09-01T11:04:41Z",
  "timeline": [
    { "atUtc": "2025-09-01T11:04:32Z", "message": "Job created and queued" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Processing started" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Resolution started (provider: simulated)" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Selected 720p Mp4 (format id 22)" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Download attempt 1/3 started" },
    { "atUtc": "2025-09-01T11:04:34Z", "message": "Attempt 1 failed (DownloadFailed): The media connection was reset during download. — retrying" },
    { "atUtc": "2025-09-01T11:04:35Z", "message": "Download attempt 2/3 started" },
    { "atUtc": "2025-09-01T11:04:39Z", "message": "Output validated (3062500 bytes, video/mp4)" },
    { "atUtc": "2025-09-01T11:04:41Z", "message": "Completed: dQw4w9WgXcQ_720p.mp4 (3062500 bytes)" }
  ]
}
```

**Failed (non-recoverable)**

```json
{
  "jobId": "job_4f3a438375",
  "requestId": "req_9c3f7a1b2d4e",
  "status": "Failed",
  "stage": "Resolving",
  "progress": 0,
  "attempts": 1,
  "message": "The requested media is unavailable (removed, private or access-restricted).",
  "error": {
    "code": "VideoUnavailable",
    "message": "The requested media is unavailable (removed, private or access-restricted).",
    "retryable": false
  },
  "requestedFormat": "mp4",
  "requestedQuality": "720p",
  "createdAtUtc": "2025-09-01T11:04:32Z",
  "startedAtUtc": "2025-09-01T11:04:33Z",
  "completedAtUtc": "2025-09-01T11:04:33Z",
  "updatedAtUtc": "2025-09-01T11:04:33Z",
  "timeline": [
    { "atUtc": "2025-09-01T11:04:32Z", "message": "Job created and queued" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Processing started" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Resolution started (provider: simulated)" },
    { "atUtc": "2025-09-01T11:04:33Z", "message": "Failed (VideoUnavailable): The requested media is unavailable..." }
  ]
}
```

#### Errors

| Code | When |
|---|---|
| `400 INVALID_REQUEST` | `jobId` is missing/blank. |
| `404 NOT_FOUND` | No job exists with the given `jobId`. |

### 3. List Available Formats — `GET /v1/download/{jobId}/formats`

Returns the media format/quality variants discovered during the **Resolving** stage. Only
available once the job has progressed past resolution; otherwise an empty array or a `404`.

> This is a convenience accessor — formats are also included in the status response while a
> job is `Processing` or terminal.

```bash
curl -X GET "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375/formats" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

#### Response — `200 OK`

Array of `MediaFormatOptionDto`:

| Field | Type | Description |
|---|---|---|
| `id` | string | Provider-specific format identifier (e.g. `"22"`, `"137"`, `"140"`). |
| `container` | string | Container codec family. One of: `Mp4`, `WebM`, `Mkv`, `Mp3`, `M4a`, `Wav`. |
| `label` | string | Human-readable quality label, e.g. `720p`, `1080p`, `audio`. |
| `height` | integer | Vertical resolution in pixels. `0` for audio-only tracks. |
| `extension` | string | File extension, e.g. `mp4`, `webm`, `m4a`. |
| `estimatedBytes` | integer\|null | Estimated output size in bytes (for pre-flight sizing). |

**Example**

```json
[
  { "id": "18",   "container": "Mp4",  "label": "360p",   "height": 360,  "extension": "mp4",  "estimatedBytes": 8400000 },
  { "id": "22",   "container": "Mp4",  "label": "720p",   "height": 720,  "extension": "mp4",  "estimatedBytes": 24500000 },
  { "id": "137",  "container": "Mp4",  "label": "1080p",  "height": 1080, "extension": "mp4",  "estimatedBytes": 51200000 },
  { "id": "244",  "container": "WebM", "label": "480p",   "height": 480,  "extension": "webm", "estimatedBytes": 9900000 },
  { "id": "248",  "container": "WebM", "label": "1080p",  "height": 1080, "extension": "webm", "estimatedBytes": 48100000 },
  { "id": "140",  "container": "M4a",  "label": "audio",  "height": 0,    "extension": "m4a",  "estimatedBytes": 3800000 }
]
```

> Format selection never upscales or transcodes: given a requested `quality`, the provider
> picks the highest available resolution **at or below** it.

#### Errors

Same as [Get Job Status](#2-get-job-status-get-v1downloadjobid) (`400` / `404`).

### 4. Cancel a Job — `DELETE /v1/download/{jobId}`

Cancels an active job. In-flight downloads are aborted mid-flight via cancellation
propagation (not just the next retry).

```bash
curl -X DELETE "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

#### Response — `200 OK`

The full job status, with `status` now `Cancelled`:

```json
{
  "jobId": "job_4f3a438375",
  "requestId": "req_9c3f7a1b2d4e",
  "status": "Cancelled",
  "stage": "Queued",
  "progress": 0,
  "attempts": 0,
  "message": "Cancelled by caller",
  "requestedFormat": "mp4",
  "requestedQuality": "720p",
  "createdAtUtc": "2025-09-01T11:04:32Z",
  "completedAtUtc": "2025-09-01T11:04:40Z",
  "updatedAtUtc": "2025-09-01T11:04:40Z",
  "timeline": [ /* ... */ ]
}
```

#### Errors

| Code | Condition |
|---|---|
| `400 INVALID_REQUEST` | `jobId` missing/blank. |
| `404 NOT_FOUND` | Job not found. |
| `409 CONFLICT` | Job is already terminal (`Completed`/`Failed`/`Cancelled`) and cannot be cancelled — the server returns the current job status instead. |

### 5. Download Output — `GET /v1/download/{jobId}/content`

Streams the completed output file. The response body is binary; headers describe the file.

> Use the `downloadUrl` returned in the completed job status, or call this path directly with
> your RapidAPI key. The URL is temporary — see `expiresAtUtc`.

```bash
curl -L -OJ "https://reliable-youtube-download.p.rapidapi.com/v1/download/job_4f3a438375/content" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

The `-L` follows redirects, `-O` writes to the filename from `Content-Disposition`, and `-J`
uses that filename. Range requests are supported for resumable downloads.

#### Response — `200 OK`

**Headers**

| Header | Description |
|---|---|
| `Content-Type` | MIME type of the file, e.g. `video/mp4`, `audio/mp4`, `audio/mpeg`. |
| `Content-Disposition` | `attachment; filename="dQw4w9WgXcQ_720p.mp4"` |
| `Content-Length` | Size in bytes. |
| `Accept-Ranges` | `bytes` (range requests supported). |
| `Last-Modified` | Job completion time. |

**Body** — the raw media bytes (binary file).

#### Errors

| Code | Condition |
|---|---|
| `404 NOT_FOUND` | Job not completed, output already expired, or no stored file. |
| `409 CONFLICT` | Job exists but has not completed yet (poll status first). |

### 6. Reliability Metrics — `GET /v1/account/usage`

A point-in-time snapshot of the reliability telemetry tracked since startup. These are the
numbers behind claims like *"X% of transient failures are automatically recovered"* rather
than a generic "reliable".

```bash
curl -X GET "https://reliable-youtube-download.p.rapidapi.com/v1/account/usage" \
  -H "X-RapidAPI-Key: YOUR_RAPIDAPI_KEY" \
  -H "X-RapidAPI-Host: reliable-youtube-download.p.rapidapi.com"
```

#### Response — `200 OK`

**Body** — `ReliabilitySnapshot`

| Field | Type | Description |
|---|---|---|
| `jobsCreated` | long | Total jobs submitted. |
| `jobsCompleted` | long | Jobs that finished successfully. |
| `jobsFailed` | long | Jobs that reached a terminal Failed state. |
| `jobsCancelled` | long | Jobs cancelled by the caller. |
| `resolutionAttempts` | long | Number of format-resolution attempts. |
| `resolutionSuccesses` | long | Resolutions that returned at least one format. |
| `downloadAttempts` | long | Total download attempts (including retries). |
| `downloadSuccesses` | long | Downloads that succeeded. |
| `retryRecoveries` | long | Transient failures that recovered automatically on retry. |
| `recoveryAttempts` | long | Total retry attempts made. |
| `averageProcessingSeconds` | double | Mean end-to-end processing time (seconds). |
| `p95ProcessingSeconds` | double | 95th-percentile processing time (seconds). |
| `retryRecoveryRatePercent` | double | `retryRecoveries / recoveryAttempts × 100`. |

**Example**

```json
{
  "jobsCreated": 1423,
  "jobsCompleted": 1310,
  "jobsFailed": 95,
  "jobsCancelled": 18,
  "resolutionAttempts": 1423,
  "resolutionSuccesses": 1398,
  "downloadAttempts": 1423,
  "downloadSuccesses": 1350,
  "retryRecoveries": 78,
  "recoveryAttempts": 113,
  "averageProcessingSeconds": 12.405,
  "p95ProcessingSeconds": 28.703,
  "retryRecoveryRatePercent": 69.02
}
```

### 7. Health — `GET /health`

Liveness probe — does **not** require an API key. Useful for load balancers and uptime
monitoring.

```bash
curl "https://reliable-youtube-download.p.rapidapi.com/health"
```

#### Response — `200 OK`

```json
{
  "status": "healthy",
  "utc": "2025-09-01T11:04:32Z"
}
```

## Job Lifecycle (State Machine)

```
                ┌────────┐
                │ Queued │
                └────┬───┘
                     │ MarkProcessing()
          ┌──────────▼──────────┐
          │ Processing          │
          │  stage: Resolving   │ ── video unavailable / no formats ──► Failed
          │  stage: Downloading │ ── transient (retryable) ──────────► retry within Download
          │  stage: Validating  │ ── truncated/empty/non-media ───────► Failed
          │  stage: Finalizing  │
          └──────────┬──────────┘
                     │ all good
                ┌────▼────┐
                │Completed│── downloadUrl + fileBytes + expiresAtUtc
                └─────────┘
                     │
            (expires / 60 min default)
```

**Terminal states:** `Completed`, `Failed`, `Cancelled`.

**Recovery behaviour:**
- Transient failures (`DownloadFailed`, `ValidationFailed`, `TimedOut`, `InternalError`)
  with `retryable = true` are retried up to **`MaxAttempts`** (default `3`) with exponential
  backoff (250 ms, 500 ms, …). Each recovered job increments `retryRecoveries`.
- Non-recoverable failures (`VideoUnavailable`, `FormatUnavailable`, `QualityUnavailable`,
  `UnsupportedUrl`, `InvalidUrl`) terminate immediately.
- Per-job time budget (`JobTimeoutSeconds`, default `300`) is enforced on every attempt; a
  hung provider call fails the job with `TimedOut`.

## Async Polling Pattern

Because downloads run in the background, clients **must poll** the status endpoint:

1. `POST /v1/download` → returns `202` + `jobId`.
2. `GET /v1/download/{jobId}` → repeat until `status` is one of the terminal states
   (`Completed`, `Failed`, `Cancelled`).
3. If `Completed` → fetch `GET /v1/download/{jobId}/content` (**before `expiresAtUtc`**).

**Recommended polling cadence:** 1–2 seconds between requests. The status response changes
quickly during `Downloading` (progress + `etaSeconds`) and includes a `timeline` of every
event, so a single poll typically tells the whole story once the job reaches `Processing`.

**On `Failed`:** inspect `error.code` and `error.retryable`:
- `retryable === false` → do not retry (the media is unavailable or unsupported).
- `retryable === true` → the server has already exhausted its retries; you may resubmit with
  a different quality/format if the failure was `DownloadFailed`/`ValidationFailed`.

## Error Handling

The API returns two shapes of errors:

### HTTP-level errors (gateway / validation / auth)

All transport and input errors produce a uniform body:

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "'url' must be a YouTube video URL (youtube.com/watch, youtu.be, or youtube.com/shorts)."
  }
}
```

| Field | Description |
|---|---|
| `error.code` | Stable machine-readable string (see table below). |
| `error.message` | Human-readable explanation. |

| HTTP Status | `error.code` | Meaning |
|---|---|---|
| `400` | `INVALID_REQUEST` | The request body or parameters are missing/invalid (also thrown by the job service for bad URLs, formats, qualities). |
| `401` | `UNAUTHORIZED` | Missing or invalid API credentials (RapidAPI key / proxy secret). |
| `404` | `NOT_FOUND` | The requested job (or its output) does not exist. |
| `409` | `CONFLICT` | The job is in a state that precludes the operation (e.g. cancelling a terminal job, or fetching content before completion). |
| `500` | `INTERNAL_ERROR` | An unexpected internal error. The job (if any) is marked `Failed` with code `InternalError` + `retryable: true`. |

### Job-level errors (returned inside a `Failed` job status)

When a job fails after processing, the failure is surfaced in the status response's `error`
object (see [Get Job Status](#2-get-job-status-get-v1downloadjobid)) rather than as a
top-level HTTP error. **Always** inspect `status.response.error` for terminal jobs — a
`200 OK` response can still describe a `Failed` job with a structured, recoverable error.

## Job Error Codes Reference

These codes are part of the public API contract and are stable. They appear inside the
`error.code` field of a `Failed` job status response (and the corresponding domain enum).

| Code | Numeric | Retryable? | Meaning |
|---|---|---|---|
| `VideoUnavailable` | 1002 | No | Removed, private, or geo-restricted media. |
| `FormatUnavailable` | 1003 | No | The requested container has no available tracks. |
| `QualityUnavailable` | 1004 | No | No format matched the requested quality. |
| `DownloadFailed` | 1005 | Yes | The download failed but recovered (or could recover) on retry. |
| `ValidationFailed` | 1006 | Yes | Output was empty, non-media, or truncated. |
| `TimedOut` | 1007 | Yes | The job exceeded its processing time budget. |
| `RateLimited` | 1008 | Yes | (Reserved) The account exceeded a rate/concurrency limit. |
| `InternalError` | 1009 | Yes | An unexpected internal error occurred. |
| `InvalidUrl` | 1000 | No | The URL is not valid HTTP(S). *(reported as `INVALID_REQUEST` before the job is created)* |
| `UnsupportedUrl` | 1001 | No | The URL is not a recognised YouTube link. *(reported as `INVALID_REQUEST` before the job is created)* |

## Data Types

### `JobStatus` (enum → string in `status`)

| Value | Description |
|---|---|
| `Queued` | Awaiting processing. |
| `Processing` | Actively running through the pipeline. |
| `Completed` | Finished successfully; output ready. |
| `Failed` | Terminal failure after retries were exhausted or a non-recoverable error. |
| `Cancelled` | Cancelled by the caller. |

### `JobStage` (enum → string in `stage`)

| Value | Description |
|---|---|
| `Queued` | Not yet started. |
| `Resolving` | Discovering available formats. |
| `Downloading` | Fetching the selected format. |
| `Validating` | Checking output integrity. |
| `Finalizing` | Storing & publishing the download URL. |

### `MediaFormat` (enum → string in `container`)

| Value | Extension | Description |
|---|---|---|
| `Mp4` | `mp4` | MP4 container (default, primary). |
| `WebM` | `webm` | WebM container. |
| `Mkv` | `mkv` | Matroska container. |
| `Mp3` | `mp3` | MP3 audio. |
| `M4a` | `m4a` | M4A audio (MP4 audio). |
| `Wav` | `wav` | WAV audio. |

### Content-Type mapping

| `MediaFormat` | Content-Type |
|---|---|
| `Mp4` | `video/mp4` |
| `WebM` | `video/webm` |
| `Mkv` | `video/x-matroska` |
| `Mp3` | `audio/mpeg` |
| `M4a` | `audio/mp4` |
| `Wav` | `audio/wav` |

## Configuration & Deployment Notes

### Running locally

```bash
dotnet run --project src/YoutubeDownload.Api --urls http://localhost:5000
```

The default configuration uses the **`Simulated`** provider, which requires no external
dependencies and demonstrates the full pipeline (including automatic retry recovery). URLs
containing `unavailable` simulate `VideoUnavailable`; URLs containing `flaky` fail the first
download attempt and recover on retry.

### Running with real downloads (yt-dlp)

Set the environment variable to enable the real provider:

```bash
DownloadJobs__Provider=YtDlp
DownloadJobs__YtDlpPath=/usr/bin/yt-dlp
```

### Running behind RapidAPI (Docker)

The `docker-compose.yml` ships a production-ready configuration. **Set `PublicBaseUrl` to the
RapidAPI gateway URL** so returned `downloadUrl`s are reachable by consumers through the
gateway:

```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Production
  DownloadJobs__PublicBaseUrl: "https://reliable-youtube-download.p.rapidapi.com"
  DownloadJobs__Provider: "YtDlp"          # or "Simulated"
  DownloadJobs__Persistence: "FileSystem"  # durable: survives restarts
  DownloadJobs__OutputRetentionMinutes: "60"
  # Authentication — Gateway mode verifies the RapidAPI proxy secret:
  Authentication__Enabled: "true"
  Authentication__Mode: "Gateway"
  Authentication__RapidApiProxySecret: "<your-rapidapi-proxy-secret>"
```

Key production behaviours worth highlighting:

| Feature | Details |
|---|---|
| **Durable persistence** | `FileJobStore` writes one JSON document per job (atomic temp-file + rename) and `FileSystemFileStorage` writes outputs to disk. Jobs and files survive restarts. |
| **Streaming delivery** | `/content` streams from disk with range-request support; large files never fit fully in memory. |
| **Per-job timeout** | A linked cancellation token (`JobTimeoutSeconds`, default 300 s) cancels any hung provider call with `TimedOut`. |
| **Periodic cleanup** | Expired outputs are evicted every 2 minutes. |
| **Cancellation propagation** | `DELETE` aborts an in-flight download immediately, not just the next retry. |
| **Customer identity** | `X-RapidAPI-User` is captured as `accountId` on every job for attribution. |
| **Truncation guard** | An output far below the estimated size fails `ValidationFailed` rather than being served. |
| **Request IDs + timeline** | Every job carries a `requestId` and a full event timeline for support/debugging. |
| **Gateway hardening** | `Mode = "Gateway"` rejects any request missing the correct `X-RapidAPI-Proxy-Secret`, blocking direct backend access. |

### Recommended RapidAPI listing configuration

When registering this API on RapidAPI, expose the routes exactly as mounted (the service
maps controllers under `/v1/`). Recommend the following subscription plan defaults:

- **Authentication** on the RapidAPI side requires the `X-RapidAPI-Key` (and `X-RapidAPI-Host`)
  headers — supply these automatically to consumers.
- Enable **Response Headers** forwarding so `Content-Type`, `Content-Disposition`, and
  `Accept-Ranges` pass through the gateway unchanged for the `/content` streaming endpoint.
- Set the backend `PublicBaseUrl` to your RapidAPI gateway hostname so the temporary
  `downloadUrl` returned on `Completed` jobs is directly consumable by subscribers.

---

## Appendix: Endpoint Quick Reference

```
POST   /v1/download                     Create a download job           → 202 (+ Location)
GET    /v1/download/{jobId}              Poll job status                 → 200
GET    /v1/download/{jobId}/formats      List discovered formats         → 200
DELETE /v1/download/{jobId}              Cancel a job                    → 200
GET    /v1/download/{jobId}/content      Stream the output file          → 200 (binary)
GET    /v1/account/usage                 Reliability metrics snapshot    → 200
GET    /health                           Liveness probe (no auth)        → 200
```

---

*Generated for the Reliable & Fast YouTube Video Download API (ASP.NET Core, Clean Architecture).*
