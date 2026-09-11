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

/// <summary>A dead-lettered event (rejected or errored), ready for inspection/replay.</summary>
public sealed record DeadLetterDto(
    long Id,
    string EventId,
    string EventType,
    string? EmployeeId,
    string Reason,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReplayedAtUtc,
    string? LastResult);

/// <summary>Pipeline metrics for the observability card.</summary>
public sealed record MetricsDto(
    int EventsTotal,
    int Applied,
    int Rejected,
    int Errors,
    int DeadLetterPending,
    int EventsLastMinute,
    int QueueDepth,
    DateTime? LastIngestAtUtc,
    DateTime TimestampUtc);
