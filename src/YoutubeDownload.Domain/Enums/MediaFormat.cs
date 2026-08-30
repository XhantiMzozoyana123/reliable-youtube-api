namespace YoutubeDownload.Domain.Enums;

/// <summary>
/// Supported output container/codec packaging options.
/// MP4 is the primary V1 container; audio formats are exposed for later phases.
/// </summary>
public enum MediaFormat
{
    Mp4 = 0,
    WebM = 1,
    Mkv = 2,
    Mp3 = 3,
    M4a = 4,
    Wav = 5
}