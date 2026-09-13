namespace WorkforceSync.Core.Models;

/// <summary>
/// The kinds of workforce events the (mock) Oracle HCM Cloud can emit.
/// Named to match the HCM Cloud event vocabulary a real feed would use.
/// </summary>
public enum WorkforceEventType
{
    /// <summary>A new employee is hired.</summary>
    Hire,

    /// <summary>An employee is terminated.</summary>
    Termination,

    /// <summary>
    /// An employee moves to a different position (department / role / location)
    /// at the same grade. The HCM "Transfer" event.
    /// </summary>
    Transfer,

    /// <summary>
    /// An employee's base compensation changes, with no position change.
    /// The HCM "Pay Change" event.
    /// </summary>
    PayChange,

    /// <summary>
    /// An employee is promoted: a new position at a higher grade, typically
    /// carrying a pay change. The HCM "Promotion" event.
    /// </summary>
    Promotion,

    /// <summary>
    /// A previously terminated employee is rehired. The HCM "Rehire" event.
    /// </summary>
    Rehire,
}
