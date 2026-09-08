namespace WorkforceSync.Core.Transform;

/// <summary>
/// Thrown when a workforce event is not a valid state transition for the
/// employee it targets (e.g. a role or compensation change for someone who is
/// already terminated). Distinct from a generic error: the event is well-formed
/// but business-invalid, so the pipeline rejects it and records the reason in
/// the audit trail rather than applying it.
/// </summary>
public sealed class WorkforceEventValidationException : Exception
{
    public WorkforceEventValidationException(string message) : base(message)
    {
    }
}
