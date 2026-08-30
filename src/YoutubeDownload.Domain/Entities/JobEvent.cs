namespace YoutubeDownload.Domain.Entities;

/// <summary>A single entry in a job's timeline.</summary>
public sealed record JobEvent(DateTimeOffset AtUtc, string Message);