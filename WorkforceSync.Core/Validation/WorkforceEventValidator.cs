using WorkforceSync.Core.Models;

namespace WorkforceSync.Core.Validation;

/// <summary>
/// Validates a <see cref="WorkforceEvent"/> and returns human-readable error strings.
/// An empty list means the event is valid.
/// </summary>
public sealed class WorkforceEventValidator
{
    /// <summary>
    /// Validates the event, returning a list of error messages (empty if valid).
    /// </summary>
    /// <param name="evt">The event to validate.</param>
    /// <param name="nowUtc">
    /// Optional reference "now" (UTC). If supplied, an event whose <c>OccurredAt</c>
    /// is in the future is flagged.
    /// </param>
    public IReadOnlyList<string> Validate(WorkforceEvent evt, DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(evt.EmployeeId))
        {
            errors.Add("EmployeeId is required.");
        }

        if (!Enum.IsDefined(evt.Type))
        {
            errors.Add($"Unknown event type '{evt.Type}'.");
            return errors; // can't meaningfully validate an unknown type
        }

        if (nowUtc is not null && evt.OccurredAt > nowUtc.Value)
        {
            errors.Add($"OccurredAt ({evt.OccurredAt:u}) is in the future.");
        }

        switch (evt.Type)
        {
            case WorkforceEventType.Hire:
                ValidateHire(evt, errors);
                break;
            case WorkforceEventType.Termination:
                ValidateTermination(evt, errors);
                break;
            case WorkforceEventType.PositionChange:
                ValidatePositionChange(evt, errors);
                break;
            case WorkforceEventType.CompensationChange:
                ValidateCompensationChange(evt, errors);
                break;
        }

        return errors;
    }

    private static void ValidateHire(WorkforceEvent evt, List<string> errors)
    {
        Require(evt.FirstName, "FirstName", errors);
        Require(evt.LastName, "LastName", errors);
        Require(evt.PositionId, "PositionId", errors);
        Require(evt.JobTitle, "JobTitle", errors);
        Require(evt.Department, "Department", errors);
        Require(evt.Currency, "Currency", errors);

        if (string.IsNullOrWhiteSpace(evt.Email))
        {
            errors.Add("Email is required.");
        }
        else if (!evt.Email.Contains('@'))
        {
            errors.Add($"Email '{evt.Email}' is not a valid address.");
        }

        if (evt.StartDate is null)
        {
            errors.Add("StartDate is required for a hire.");
        }

        if (evt.BaseSalary is null)
        {
            errors.Add("BaseSalary is required for a hire.");
        }
        else if (evt.BaseSalary <= 0m)
        {
            errors.Add($"BaseSalary must be greater than zero (got {evt.BaseSalary}).");
        }
    }

    private static void ValidateTermination(WorkforceEvent evt, List<string> errors)
    {
        // A termination is a past/present event: its effective end date should not be
        // after the moment the event was recorded.
        if (evt.EndDate is not null && evt.EndDate > evt.OccurredAt)
        {
            errors.Add(
                $"EndDate ({evt.EndDate:u}) is after OccurredAt ({evt.OccurredAt:u}).");
        }
    }

    private static void ValidatePositionChange(WorkforceEvent evt, List<string> errors)
    {
        Require(evt.PositionId, "PositionId", errors);
        Require(evt.JobTitle, "JobTitle", errors);
    }

    private static void ValidateCompensationChange(WorkforceEvent evt, List<string> errors)
    {
        if (evt.BaseSalary is null)
        {
            errors.Add("BaseSalary is required for a compensation change.");
        }
        else if (evt.BaseSalary <= 0m)
        {
            errors.Add($"BaseSalary must be greater than zero (got {evt.BaseSalary}).");
        }

        Require(evt.Currency, "Currency", errors);
    }

    private static void Require(string? value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required.");
        }
    }
}
