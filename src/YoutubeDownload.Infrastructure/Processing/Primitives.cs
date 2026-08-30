using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Infrastructure.Processing;

/// <summary>System clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Short, URL-safe, prefix-based job ids: "job_xxxxxxxx".</summary>
public sealed class JobIdGenerator : IJobIdGenerator
{
    public string Generate() => $"job_{Guid.NewGuid().ToString("N")[..10]}";
}