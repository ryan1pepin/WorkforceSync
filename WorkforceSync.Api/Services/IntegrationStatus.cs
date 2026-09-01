namespace WorkforceSync.Api.Services;

/// <summary>
/// Live state of the integration pipeline. Updated by the background ingestion
/// service (M3); read by the health endpoint. Singleton so all consumers see
/// the same instance.
/// </summary>
public sealed class IntegrationStatus
{
    public string Status { get; set; } = "Healthy";
    public DateTime? LastIngestAtUtc { get; set; }
    public int QueueDepth { get; set; }
}
