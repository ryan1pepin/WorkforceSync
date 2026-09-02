namespace WorkforceSync.Api.Services;

/// <summary>
/// Configuration for the background ingestion pipeline (bound from the
/// "Ingestion" section of appsettings.json).
/// </summary>
/// <param name="Enabled">Run the poller + processor at startup.</param>
/// <param name="FeedUrl">The mock HCM ATOM feed endpoint to poll.</param>
/// <param name="PollIntervalSeconds">How often the feed is polled.</param>
/// <param name="QueueCapacity">Bounded channel capacity (backpressure).</param>
public sealed record IngestionOptions(
    bool Enabled,
    string FeedUrl,
    int PollIntervalSeconds,
    int QueueCapacity);
