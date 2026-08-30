using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YoutubeDownload.Application.Ports;
using YoutubeDownload.Infrastructure.Storage;

namespace YoutubeDownload.Infrastructure.Processing;

/// <summary>
/// Periodically evicts expired output files and stale temporary state so storage never
/// grows unboundedly (business spec §7G: temporary URLs, not permanent hosting).
/// </summary>
public sealed class ExpiredOutputCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    private readonly IServiceProvider _services;
    private readonly ILogger<ExpiredOutputCleanupService> _logger;

    public ExpiredOutputCleanupService(IServiceProvider services, ILogger<ExpiredOutputCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var removed = 0;
                foreach (var storage in scope.ServiceProvider.GetServices<IFileStorage>())
                {
                    if (storage is InMemoryFileStorage mem) removed += mem.RemoveExpired();
                    if (storage is FileSystemFileStorage fs) removed += fs.RemoveExpired();
                }
                if (removed > 0) _logger.LogInformation("Cleanup evicted {Count} expired output(s)", removed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Output cleanup failed");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}