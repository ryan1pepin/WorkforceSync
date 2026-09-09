using WorkforceSync.Core.Models;

namespace WorkforceSync.Core.Transform;

/// <summary>
/// Transforms a <see cref="WorkforceEvent"/> into an <see cref="Employee"/>.
/// Pure and stateless — no I/O, no database.
/// </summary>
public sealed class WorkforceEventTransformer
{
    /// <summary>
    /// Applies the event to produce (or update) an employee.
    /// </summary>
    /// <param name="evt">The event to apply.</param>
    /// <param name="existing">
    /// The prior employee state, if any. Required for non-hire events
    /// (termination, position change, compensation change).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// If a non-hire event is applied with no existing employee.
    /// </exception>
    public Employee Apply(WorkforceEvent evt, Employee? existing = null)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return evt.Type switch
        {
            WorkforceEventType.Hire => ApplyHire(evt),
            WorkforceEventType.Termination => ApplyTermination(evt, RequireExisting(evt, existing)),
            WorkforceEventType.PositionChange => ApplyPositionChange(evt, RequireExisting(evt, existing)),
            WorkforceEventType.CompensationChange => ApplyCompensationChange(evt, RequireExisting(evt, existing)),
            _ => throw new InvalidOperationException(
                $"Unsupported workforce event type '{evt.Type}'.")
        };
    }

    private static Employee ApplyHire(WorkforceEvent evt)
    {
        return new Employee(
            EmployeeId: evt.EmployeeId,
            FirstName: Require(evt.FirstName, "FirstName"),
            LastName: Require(evt.LastName, "LastName"),
            Email: Require(evt.Email, "Email"),
            PositionId: Require(evt.PositionId, "PositionId"),
            JobTitle: Require(evt.JobTitle, "JobTitle"),
            Department: Require(evt.Department, "Department"),
            StartDate: evt.StartDate ?? evt.OccurredAt,
            BaseSalary: evt.BaseSalary ?? 0m,
            Currency: Require(evt.Currency, "Currency"),
            IsActive: true,
            TerminationDate: null);
    }

    private static Employee ApplyTermination(WorkforceEvent evt, Employee existing)
    {
        var terminationDate = evt.EndDate ?? evt.OccurredAt;
        return existing with
        {
            IsActive = false,
            TerminationDate = terminationDate,
        };
    }

    private static Employee ApplyPositionChange(WorkforceEvent evt, Employee existing)
    {
        EnsureActive(existing, evt);
        return existing with
        {
            PositionId = evt.PositionId ?? existing.PositionId,
            JobTitle = evt.JobTitle ?? existing.JobTitle,
            Department = evt.Department ?? existing.Department,
            // A promotion typically carries a pay change too — apply it when the
            // event includes compensation (a bare title change leaves pay as-is).
            BaseSalary = evt.BaseSalary ?? existing.BaseSalary,
            Currency = evt.Currency ?? existing.Currency,
        };
    }

    private static Employee ApplyCompensationChange(WorkforceEvent evt, Employee existing)
    {
        EnsureActive(existing, evt);
        return existing with
        {
            BaseSalary = evt.BaseSalary ?? existing.BaseSalary,
            Currency = evt.Currency ?? existing.Currency,
        };
    }

    /// <summary>
    /// A role or compensation change is only valid for an active employee. A
    /// terminated person getting a new role is a <em>rehire</em> — which is a
    /// Hire event that carries pay — not a bare PositionChange. Reject it so the
    /// pipeline never puts a terminated employee into a changed-but-inactive
    /// state.
    /// </summary>
    private static void EnsureActive(Employee existing, WorkforceEvent evt)
    {
        if (!existing.IsActive)
        {
            throw new WorkforceEventValidationException(
                $"A '{evt.Type}' event cannot be applied to terminated employee " +
                $"'{existing.EmployeeId}'. A new role or pay for a terminated person " +
                "is a rehire and must arrive as a Hire event with compensation.");
        }
    }

    private static Employee RequireExisting(WorkforceEvent evt, Employee? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException(
                $"Cannot apply a '{evt.Type}' event to a non-existent employee " +
                $"(EmployeeId '{evt.EmployeeId}'). A hire must occur first.");
        }

        return existing;
    }

    private static string Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"A hire event is missing required field '{field}'.");
        }

        return value;
    }
}
