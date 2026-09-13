namespace WorkforceSync.Core.Models;

/// <summary>
/// A single workforce event parsed from the HCM ATOM feed. Immutable.
/// Optional fields are nullable; required fields are validated by
/// <see cref="Validation.WorkforceEventValidator"/>.
/// </summary>
/// <param name="EventId">Feed entry id (unique within the feed).</param>
/// <param name="Type">The event kind.</param>
/// <param name="OccurredAt">When the event occurred (UTC).</param>
/// <param name="EmployeeId">The affected employee's id.</param>
/// <param name="PersonNumber">The HCM person number (if provided).</param>
/// <param name="FirstName">Employee first name (if provided).</param>
/// <param name="LastName">Employee last name (if provided).</param>
/// <param name="Email">Employee email (if provided).</param>
/// <param name="LegalEmployer">The legal employer / entity (if provided).</param>
/// <param name="PositionId">Position id (if provided).</param>
/// <param name="Job">The job (role) the position belongs to (if provided).</param>
/// <param name="Grade">The pay grade (if provided).</param>
/// <param name="JobTitle">Job title (if provided).</param>
/// <param name="Department">Department (if provided).</param>
/// <param name="WorkLocation">Work location (if provided).</param>
/// <param name="Supervisor">Supervisor name (if provided).</param>
/// <param name="EmploymentType">Employment type, e.g. Regular / Temporary (if provided).</param>
/// <param name="PayBasis">Pay basis, e.g. Annual / Hourly (if provided).</param>
/// <param name="StartDate">Hire / effective start date (if provided).</param>
/// <param name="EndDate">Termination / effective end date (if provided).</param>
/// <param name="TerminationReason">Termination reason (if provided).</param>
/// <param name="BaseSalary">Base salary (if provided).</param>
/// <param name="Currency">ISO currency code (if provided).</param>
public sealed record WorkforceEvent(
    string EventId,
    WorkforceEventType Type,
    DateTime OccurredAt,
    string EmployeeId,
    string? PersonNumber,
    string? FirstName,
    string? LastName,
    string? Email,
    string? LegalEmployer,
    string? PositionId,
    string? Job,
    string? Grade,
    string? JobTitle,
    string? Department,
    string? WorkLocation,
    string? Supervisor,
    string? EmploymentType,
    string? PayBasis,
    DateTime? StartDate,
    DateTime? EndDate,
    string? TerminationReason,
    decimal? BaseSalary,
    string? Currency);
