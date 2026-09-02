using WorkforceSync.Core.Models;

namespace WorkforceSync.Api.Services;

/// <summary>
/// The background ingestion pipeline: poll the mock HCM ATOM feed → enqueue
/// new events → transform/validate → persist. Runs as a
/// <see cref="BackgroundService"/> with two loops (poller + processor) so a
/// slow consumer applies backpressure to the poller via the bounded
/// <see cref="EventQueue"/>.
///
/// Failure handling: a transient poll failure (feed down, network blip) is
/// logged and retried on the next interval — the pipeline rides it out. A
/// malformed feed is logged to the audit trail and skipped. One bad event
/// never wedges the pipeline (the processor isolates per-event failures).
/// </summary>
public sealed class IngestionService : BackgroundService
{
    private readonly HcmFeedClient _feedClient;
    private readonly EventQueue _queue;
    private readonly IntegrationStatus _status;
    private readonly IngestionOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestionService> _logger;
    private readonly HashSet<string> _seen = new();

    public IngestionService(
        HcmFeedClient feedClient,
        EventQueue queue,
        IntegrationStatus status,
        IngestionOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<IngestionService> logger)
    {
        _feedClient = feedClient;
        _queue = queue;
        _status = status;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Ingestion started: feed={FeedUrl}, poll={Poll}s, queue={Queue}",
            _options.FeedUrl, _options.PollIntervalSeconds, _options.QueueCapacity);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var poller = Task.Run(() => PollLoopAsync(cts.Token), CancellationToken.None);
        var processor = Task.Run(() => ProcessLoopAsync(cts.Token), CancellationToken.None);

        try
        {
            await Task.WhenAll(poller, processor);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            _queue.Complete();
        }
    }

    /// <summary>Polls the feed and enqueues events we haven't seen yet.</summary>
    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        do
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var events = await _feedClient.FetchEventsAsync(ct);
                var fresh = events.Where(e => _seen.Add(e.EventId)).ToList();

                foreach (var evt in fresh)
                {
                    await _queue.EnqueueAsync(evt, ct);
                }

                if (fresh.Count > 0)
                {
                    _status.LastIngestAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("Polled feed: {Total} events, {Fresh} new.", events.Count, fresh.Count);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Transient failure — log and ride it out on the next poll.
                _logger.LogWarning(ex, "Feed poll failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Drains the queue, processing each event in its own scope.</summary>
    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            WorkforceEvent? evt;
            try
            {
                evt = await _queue.DequeueAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (evt is null)
            {
                return; // channel completed and fully drained
            }

            await ProcessOneAsync(evt, ct);
        }
    }

    private async Task ProcessOneAsync(WorkforceEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<EventProcessor>();

        var result = await processor.ProcessAsync(evt, ct);
        _status.QueueDepth = _queue.Count;

        if (result is "Error")
        {
            _logger.LogError("Event {EventId} ({Type}) failed to process.", evt.EventId, evt.Type);
        }
    }
}
