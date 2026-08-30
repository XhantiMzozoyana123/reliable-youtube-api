using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoutubeDownload.Application.Common;
using YoutubeDownload.Application.Features.Jobs;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Infrastructure.Media;
using YoutubeDownload.Infrastructure.Persistence;
using YoutubeDownload.Infrastructure.Processing;
using YoutubeDownload.Infrastructure.Storage;

namespace YoutubeDownload.Infrastructure;

/// <summary>Composition root for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("DownloadJobs").Get<DownloadJobsOptions>() ?? new DownloadJobsOptions();
        services.AddSingleton(options);

        // Ports — select durable (FileSystem) or ephemeral (Memory) implementations.
        var useFileSystem = !string.Equals(options.Persistence, "Memory", StringComparison.OrdinalIgnoreCase);
        if (useFileSystem)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, options.OutputDirectory);
            services.AddSingleton<IJobStore>(_ => new FileJobStore(dir));
            services.AddSingleton<IFileStorage>(_ => new FileSystemFileStorage(Path.Combine(dir, "output")));
        }
        else
        {
            services.AddSingleton<IJobStore, InMemoryJobStore>();
            services.AddSingleton<IFileStorage, InMemoryFileStorage>();
        }
        services.AddSingleton<IJobIdGenerator, JobIdGenerator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJobMetrics, InMemoryJobMetrics>();
        services.AddSingleton<IFileStorage, InMemoryFileStorage>();

        // Media provider (swap "Simulated" for "YtDlp" in configuration)
        if (string.Equals(options.Provider, "YtDlp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IMediaProvider, YtDlpMediaProvider>();
        else
            services.AddSingleton<IMediaProvider, SimulatedMediaProvider>();

        // Job queue + background workers
        services.AddSingleton<ChannelJobScheduler>();
        services.AddSingleton<IJobScheduler>(sp => sp.GetRequiredService<ChannelJobScheduler>());
        services.AddHostedService<JobProcessingService>();
        services.AddHostedService<ExpiredOutputCleanupService>();

        // Application services
        services.AddScoped<IDownloadJobsService, DownloadJobsService>();

        return services;
    }
}