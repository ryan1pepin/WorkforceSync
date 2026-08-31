namespace WorkforceSync.Core.Models;

/// <summary>
/// A normalized employee record — the target shape after transforming HCM events.
/// </summary>
/// <param name="EmployeeId">Stable employee identifier.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Email address.</param>
/// <param name="PositionId">Current position id.</param>
/// <param name="JobTitle">Current job title.</param>
/// <param name="Department">Current department.</param>
/// <param name="StartDate">Hire date.</param>
/// <param name="BaseSalary">Current base salary.</param>
/// <param name="Currency">ISO currency code.</param>
/// <param name="IsActive">Whether the employee is currently active.</param>
/// <param name="TerminationDate">Termination date, if terminated.</param>
public sealed record Employee(
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
    DateTime? TerminationDate);
