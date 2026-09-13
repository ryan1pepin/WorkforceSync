namespace WorkforceSync.Core.Models;

/// <summary>
/// A normalized employee record — the target shape after transforming HCM events.
/// Modeled on the Oracle HCM Cloud Person/Assignment shape: a person (name,
/// person number) with a current assignment (position, job, grade, department,
/// location, supervisor) and compensation.
/// </summary>
/// <param name="EmployeeId">Stable employee identifier.</param>
/// <param name="PersonNumber">The HCM person number.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Email address.</param>
/// <param name="LegalEmployer">The legal employer / entity.</param>
/// <param name="PositionId">Current position id.</param>
/// <param name="Job">Current job (role).</param>
/// <param name="Grade">Current pay grade.</param>
/// <param name="JobTitle">Current job title.</param>
/// <param name="Department">Current department.</param>
/// <param name="WorkLocation">Current work location.</param>
/// <param name="Supervisor">Current supervisor.</param>
/// <param name="EmploymentType">Employment type (Regular / Temporary).</param>
/// <param name="PayBasis">Pay basis (Annual / Hourly).</param>
/// <param name="StartDate">Hire date.</param>
/// <param name="BaseSalary">Current base salary.</param>
/// <param name="Currency">ISO currency code.</param>
/// <param name="IsActive">Whether the employee is currently active.</param>
/// <param name="TerminationDate">Last working day, if terminated.</param>
/// <param name="TerminationReason">Termination reason, if terminated.</param>
public sealed record Employee(
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
    string? TerminationReason);
