namespace WorkforceSync.Api.Services;

/// <summary>
/// Configuration for the background ingestion pipeline (bound from the
/// "Ingestion" section of appsettings.json).
/// </summary>
/// <param name="Enabled">Run the poller + processor at startup.</param>
/// <param name="FeedUrl">The mock HCM ATOM feed endpoint to poll.</param>
/// <param name="PollIntervalSeconds">How often the feed is polled.</param>
/// <param name="QueueCapacity">Bounded channel capacity (backpressure).</param>
/// <param name="ScenarioStepSeconds">Seconds between scripted mock-HCM events (demo pacing).</param>
/// <param name="ScenarioCycleSeconds">How long one mock-HCM cycle lasts before it resets and starts over.</param>
public sealed record IngestionOptions(
    bool Enabled,
    string FeedUrl,
    int PollIntervalSeconds,
    int QueueCapacity,
    int ScenarioStepSeconds = 15,
    int ScenarioCycleSeconds = 300);
