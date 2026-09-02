namespace WorkforceSync.HcmSource;

/// <summary>
/// A workforce event as the mock HCM would emit it. Plain data — the
/// <see cref="AtomFeedGenerator"/> turns these into ATOM XML.
/// </summary>
/// <param name="Id">Stable event id (unique per event).</param>
/// <param name="Type">Event type name (Hire / Termination / PositionChange / CompensationChange).</param>
/// <param name="OccurredAtUtc">When the event occurred.</param>
/// <param name="EmployeeId">Affected employee.</param>
/// <param name="FirstName">First name (hire events).</param>
/// <param name="LastName">Last name (hire events).</param>
/// <param name="Email">Email (hire events).</param>
/// <param name="PositionId">Position id (hire / position change).</param>
/// <param name="JobTitle">Job title (hire / position change).</param>
/// <param name="Department">Department (hire / position change).</param>
/// <param name="StartDate">Hire date (hire events).</param>
/// <param name="EndDate">Termination date (termination events).</param>
/// <param name="BaseSalary">Base salary (hire / comp change).</param>
/// <param name="Currency">ISO currency code.</param>
public sealed record HcmEvent(
    string Id,
    string Type,
    DateTime OccurredAtUtc,
    string EmployeeId,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? PositionId = null,
    string? JobTitle = null,
    string? Department = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal? BaseSalary = null,
    string? Currency = null);
