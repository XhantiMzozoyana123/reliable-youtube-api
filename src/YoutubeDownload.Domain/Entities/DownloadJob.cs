using System.Text.Json.Serialization;
using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Domain.Entities;

/// <summary>
/// The central aggregate representing a single download operation.
/// Owns its lifecycle transitions so invalid state changes are prevented.
/// </summary>
public sealed class DownloadJob
{
    public DownloadJob(string jobId, string url, string? accountId, string? requestedFormat, string? requestedQuality, DateTimeOffset createdAtUtc, string? requestId = null)
    {
        JobId = jobId;
        Url = url;
        AccountId = accountId ?? "";
        RequestedFormat = requestedFormat;
        RequestedQuality = requestedQuality;
        Status = JobStatus.Queued;
        Stage = JobStage.Queued;
        UtilizedFormatOptions = new List<MediaFormatOption>();
        Error = null;
        CreatedAtUtc = createdAtUtc;
        Attempts = 0;
        UpdatedAtUtc = createdAtUtc;
        RequestId = requestId ?? $"req_{Guid.NewGuid().ToString("N")[..12]}";
        Events.Add(new JobEvent(createdAtUtc, "Job created and queued"));
    }

    // [JsonInclude] allows the durable store to round-trip state through non-public setters
    // while the public mutators below remain the only way application code changes state.

    [JsonInclude] public string JobId { get; }
    [JsonInclude] public string Url { get; }
    [JsonInclude] public string AccountId { get; }
    [JsonInclude] public string RequestId { get; }
    [JsonInclude] public string? RequestedFormat { get; }
    [JsonInclude] public string? RequestedQuality { get; }

    /// <summary>Chronological failure/transition timeline — the basis of §17 support answers.</summary>
    [JsonInclude] public List<JobEvent> Events { get; private set; } = [];

    [JsonInclude] public JobStatus Status { get; private set; }
    [JsonInclude] public JobStage Stage { get; private set; }
    [JsonInclude] public int Progress { get; private set; }
    [JsonInclude] public int? EtaSeconds { get; private set; }
    [JsonInclude] public int Attempts { get; private set; }
    [JsonInclude] public string? Message { get; private set; }

    [JsonInclude] public List<MediaFormatOption> UtilizedFormatOptions { get; private set; }
    [JsonInclude] public MediaFormatOption? SelectedOption { get; private set; }

    /// <summary>Temporary download URL for the completed file (expires).</summary>
    [JsonInclude] public string? DownloadUrl { get; private set; }
    [JsonInclude] public string? FileName { get; private set; }
    [JsonInclude] public string? ContentType { get; private set; }
    [JsonInclude] public long? FileBytes { get; private set; }
    [JsonInclude] public DateTimeOffset? ExpiresAtUtc { get; private set; }

    [JsonInclude] public JobError? Error { get; private set; }
    [JsonInclude] public DateTimeOffset CreatedAtUtc { get; }
    [JsonInclude] public DateTimeOffset? StartedAtUtc { get; private set; }
    [JsonInclude] public DateTimeOffset? CompletedAtUtc { get; private set; }
    [JsonInclude] public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal => Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;
    public bool IsActive => !IsTerminal;

    public void RecordEvent(string message) { Events.Add(new JobEvent(DateTimeOffset.UtcNow, message)); Touch(); }

    public void MarkProcessing()
    {
        if (IsTerminal) throw new InvalidOperationException("Cannot modify a job after it has reached a terminal state.");
        Status = JobStatus.Processing;
        StartedAtUtc ??= Now();
        Events.Add(new JobEvent(DateTimeOffset.UtcNow, "Processing started"));
        Touch();
    }

    public void SetStage(JobStage stage) { Stage = stage; Touch(); }

    public void SetProgress(int percent, int? etaSeconds = null)
    {
        Progress = Math.Clamp(percent, 0, 100);
        EtaSeconds = Math.Max(0, etaSeconds ?? 0);
        Touch();
    }

    public void SetMessage(string message) { Message = message; Touch(); }

    public void SetFormats(IReadOnlyCollection<MediaFormatOption> formats)
    {
        UtilizedFormatOptions = formats.ToList();
        Touch();
    }

    public void SelectOption(MediaFormatOption option) { SelectedOption = option; Touch(); }

    /// <summary>Registers one retry attempt. Returns the (new) attempt number.</summary>
    public int RegisterAttempt() { Attempts++; Touch(); return Attempts; }

    /// <summary>
    /// Transitions the job to Completed once the output has passed validation.
    /// </summary>
    public void Complete(string downloadUrl, string fileName, string contentType, long fileBytes, DateTimeOffset expiresAtUtc)
    {
        if (Status == JobStatus.Completed) return;
        if (Status is JobStatus.Failed or JobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot complete a job in state '{Status}'.");
        Status = JobStatus.Completed;
        Stage = JobStage.Finalizing;
        Progress = 100;
        EtaSeconds = 0;
        DownloadUrl = downloadUrl;
        FileName = fileName;
        ContentType = contentType;
        FileBytes = fileBytes;
        ExpiresAtUtc = expiresAtUtc;
        CompletedAtUtc = Now();
        Message = "Job completed successfully";
        Events.Add(new JobEvent(DateTimeOffset.UtcNow, $"Completed: {fileName} ({fileBytes} bytes)"));
        Touch();
    }

    /// <summary>Transitions the job to a terminal Failed state.</summary>
    public void Fail(JobError error, string? message = null)
    {
        if (Status is JobStatus.Completed or JobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot fail a job in state '{Status}'.");
        Status = JobStatus.Failed;
        Error = error;
        Message = message ?? error.Message;
        CompletedAtUtc = Now();
        Events.Add(new JobEvent(DateTimeOffset.UtcNow, $"Failed ({error.Code}): {Message}"));
        Touch();
    }

    /// <summary>Transitions an active job to Cancelled.</summary>
    public void Cancel(string? reason = null)
    {
        if (IsTerminal) return;
        Status = JobStatus.Cancelled;
        Message = reason ?? "Job cancelled by caller";
        CompletedAtUtc = Now();
        Events.Add(new JobEvent(DateTimeOffset.UtcNow, Message));
        Touch();
    }

    /// <summary>True when the stored download URL has not yet expired.</summary>
    public bool IsDownloadAvailable() => DownloadUrl is not null && ExpiresAtUtc > Now();

    private DateTimeOffset Now() => DateTimeOffset.UtcNow;

    private void _active() { if (IsTerminal) throw new InvalidOperationException("Cannot modify a job after it has reached a terminal state."); }
    private void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}