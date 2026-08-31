namespace WorkforceSync.Core.Models;

/// <summary>
/// The kinds of workforce events the (mock) Oracle HCM Cloud can emit.
/// </summary>
public enum WorkforceEventType
{
    /// <summary>A new employee is hired.</summary>
    Hire,

    /// <summary>An employee is terminated.</summary>
    Termination,

    /// <summary>An employee's position (title/department) changes.</summary>
    PositionChange,

    /// <summary>An employee's base compensation changes.</summary>
    CompensationChange,
}
