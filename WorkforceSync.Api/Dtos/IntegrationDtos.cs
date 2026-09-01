namespace WorkforceSync.Api.Dtos;

/// <summary>Integration pipeline health.</summary>
public sealed record HealthDto(
    string Status,
    bool Database,
    DateTime? LastIngestAtUtc,
    int QueueDepth,
    DateTime TimestampUtc);

/// <summary>A single integration audit log entry.</summary>
public sealed record AuditDto(
    long Id,
    DateTime AtUtc,
    string Source,
    string EventType,
    string? EmployeeId,
    string Status,
    string? Message);
