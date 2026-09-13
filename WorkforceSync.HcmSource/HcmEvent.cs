namespace WorkforceSync.HcmSource;

/// <summary>
/// A workforce event as the mock HCM would emit it. Plain data — the
/// <see cref="AtomFeedGenerator"/> turns these into ATOM XML.
/// </summary>
/// <param name="Id">Stable event id (unique per event).</param>
/// <param name="Type">Event type name (Hire / Termination / Transfer / PayChange / Promotion).</param>
/// <param name="OccurredAtUtc">When the event occurred.</param>
/// <param name="EmployeeId">Affected employee.</param>
/// <param name="PersonNumber">The HCM person number.</param>
/// <param name="FirstName">First name (hire events).</param>
/// <param name="LastName">Last name (hire events).</param>
/// <param name="Email">Email (hire events).</param>
/// <param name="LegalEmployer">The legal employer / entity.</param>
/// <param name="PositionId">Position id (hire / transfer / promotion).</param>
/// <param name="Job">The job (role) the position belongs to.</param>
/// <param name="Grade">The pay grade.</param>
/// <param name="JobTitle">Job title (hire / transfer / promotion).</param>
/// <param name="Department">Department (hire / transfer / promotion).</param>
/// <param name="WorkLocation">Work location.</param>
/// <param name="Supervisor">Supervisor name.</param>
/// <param name="EmploymentType">Employment type (Regular / Temporary).</param>
/// <param name="PayBasis">Pay basis (Annual / Hourly).</param>
/// <param name="StartDate">Hire date (hire events).</param>
/// <param name="EndDate">Last working day (termination events).</param>
/// <param name="TerminationReason">Termination reason (termination events).</param>
/// <param name="BaseSalary">Base salary (hire / pay change / promotion).</param>
/// <param name="Currency">ISO currency code.</param>
public sealed record HcmEvent(
    string Id,
    string Type,
    DateTime OccurredAtUtc,
    string EmployeeId,
    string? PersonNumber = null,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? LegalEmployer = null,
    string? PositionId = null,
    string? Job = null,
    string? Grade = null,
    string? JobTitle = null,
    string? Department = null,
    string? WorkLocation = null,
    string? Supervisor = null,
    string? EmploymentType = null,
    string? PayBasis = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? TerminationReason = null,
    decimal? BaseSalary = null,
    string? Currency = null);
