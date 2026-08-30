namespace YoutubeDownload.Application.Common;

/// <summary>Abstraction over the system clock so time-dependent logic is testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}