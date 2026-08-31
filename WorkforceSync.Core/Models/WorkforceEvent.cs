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
/// <param name="FirstName">Employee first name (if provided).</param>
/// <param name="LastName">Employee last name (if provided).</param>
/// <param name="Email">Employee email (if provided).</param>
/// <param name="PositionId">Position id (if provided).</param>
/// <param name="JobTitle">Job title (if provided).</param>
/// <param name="Department">Department (if provided).</param>
/// <param name="StartDate">Hire / effective start date (if provided).</param>
/// <param name="EndDate">Termination / effective end date (if provided).</param>
/// <param name="BaseSalary">Base salary (if provided).</param>
/// <param name="Currency">ISO currency code (if provided).</param>
public sealed record WorkforceEvent(
    string EventId,
    WorkforceEventType Type,
    DateTime OccurredAt,
    string EmployeeId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PositionId,
    string? JobTitle,
    string? Department,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? BaseSalary,
    string? Currency);
