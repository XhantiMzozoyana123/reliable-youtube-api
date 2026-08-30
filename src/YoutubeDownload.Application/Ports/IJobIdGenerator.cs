namespace YoutubeDownload.Application.Ports;

/// <summary>Generates public job identifiers (e.g. "job_ab12cd34").</summary>
public interface IJobIdGenerator
{
    string Generate();
}