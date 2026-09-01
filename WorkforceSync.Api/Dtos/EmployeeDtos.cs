using System.ComponentModel.DataAnnotations;

namespace WorkforceSync.Api.Dtos;

/// <summary>A workforce employee as exposed by the API.</summary>
public sealed record EmployeeDto(
    string EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string PositionId,
    string JobTitle,
    string Department,
    DateTime StartDate,
    decimal BaseSalary,
    string Currency,
    bool IsActive,
    DateTime? TerminationDate,
    DateTime UpdatedAtUtc);

/// <summary>Request body for POST /employees.</summary>
public sealed record EmployeeCreate(
    string? EmployeeId,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Email,
    [Required] string PositionId,
    [Required] string JobTitle,
    [Required] string Department,
    [Required] DateTime StartDate,
    [Required] decimal BaseSalary,
    [Required] string Currency);

/// <summary>Request body for PATCH /employees/{id}. All fields optional.</summary>
public sealed record EmployeeUpdate(
    string? FirstName,
    string? LastName,
    string? Email,
    string? PositionId,
    string? JobTitle,
    string? Department,
    DateTime? StartDate,
    decimal? BaseSalary,
    string? Currency,
    bool? IsActive);

/// <summary>A page of results with total count.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
