using System.Collections.Concurrent;
using System.Threading.Channels;
using YoutubeDownload.Application.Ports;

namespace YoutubeDownload.Infrastructure.Processing;

/// <summary>
/// Channel-backed job queue with bounded capacity. Bounded so back-pressure is visible
/// instead of silently growing unbounded under load.
/// </summary>
public sealed class ChannelJobScheduler : IJobScheduler, IDisposable
{
    private readonly Channel<string> _channel;

    public ChannelJobScheduler(int capacity = 512)
    {
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task EnqueueAsync(string jobId, CancellationToken ct = default)
    {
        while (!_channel.Writer.TryWrite(jobId))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(20, ct).ConfigureAwait(false);
        }
    }

    internal ChannelReader<string> Reader => _channel.Reader;
    public void Dispose() { /* channel needs no explicit disposal */ }
}