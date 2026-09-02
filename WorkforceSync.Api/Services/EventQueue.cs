using System.Threading.Channels;
using WorkforceSync.Core.Models;

namespace WorkforceSync.Api.Services;

/// <summary>
/// A bounded <see cref="Channel{T}"/> of workforce events. The poller
/// writes; the processor reads. Bounded so a slow consumer applies
/// backpressure to the poller instead of growing memory without limit.
/// </summary>
public sealed class EventQueue
{
    private readonly Channel<WorkforceEvent> _channel;

    public EventQueue(int capacity)
    {
        _channel = Channel.CreateBounded<WorkforceEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    /// <summary>Approximate number of events waiting to be processed.</summary>
    public int Count => _channel.Reader.TryPeek(out _) ? 1 : 0;

    /// <summary>Enqueues an event, waiting if the channel is full (backpressure).</summary>
    public ValueTask EnqueueAsync(WorkforceEvent evt, CancellationToken ct)
        => _channel.Writer.WriteAsync(evt, ct);

    /// <summary>Reads the next event, completing when the writer is complete.</summary>
    public ValueTask<WorkforceEvent> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);

    /// <summary>Signals that no more events will be written (graceful shutdown).</summary>
    public void Complete() => _channel.Writer.Complete();
}
