using System.ComponentModel.DataAnnotations;

namespace WorkforceSync.Api.Dtos;

/// <summary>A workforce employee as exposed by the API.</summary>
public sealed record EmployeeDto(
    string EmployeeId,
    string PersonNumber,
    string FirstName,
    string LastName,
    string Email,
    string LegalEmployer,
    string PositionId,
    string Job,
    string Grade,
    string JobTitle,
    string Department,
    string WorkLocation,
    string Supervisor,
    string EmploymentType,
    string PayBasis,
    DateTime StartDate,
    decimal BaseSalary,
    string Currency,
    bool IsActive,
    DateTime? TerminationDate,
    string? TerminationReason,
    DateTime UpdatedAtUtc);

/// <summary>Request body for POST /employees.</summary>
public sealed record EmployeeCreate(
    string? EmployeeId,
    [Required] string PersonNumber,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Email,
    [Required] string LegalEmployer,
    [Required] string PositionId,
    [Required] string Job,
    [Required] string Grade,
    [Required] string JobTitle,
    [Required] string Department,
    [Required] string WorkLocation,
    [Required] string Supervisor,
    [Required] string EmploymentType,
    [Required] string PayBasis,
    [Required] DateTime StartDate,
    [Required] decimal BaseSalary,
    [Required] string Currency);

/// <summary>Request body for PATCH /employees/{id}. All fields optional.</summary>
public sealed record EmployeeUpdate(
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
    decimal? BaseSalary,
    string? Currency,
    bool? IsActive);

/// <summary>A page of results with total count.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>One field's old → new value within an employee change event.</summary>
public sealed record EmployeeChangeFieldDto(string Field, string? OldValue, string? NewValue);

/// <summary>
/// A single event in an employee's change history (e.g. a hire, promotion,
/// pay change, or termination) with the fields it changed.
/// </summary>
public sealed record EmployeeChangeDto(
    string EventId,
    DateTime AtUtc,
    string EventType,
    IReadOnlyList<EmployeeChangeFieldDto> Fields);
