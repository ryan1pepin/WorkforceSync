namespace WorkforceSync.Core.Atom;

/// <summary>
/// Thrown when an ATOM feed cannot be parsed into workforce events.
/// </summary>
public sealed class AtomFeedParseException : Exception
{
    /// <summary>Creates an exception with the given message.</summary>
    public AtomFeedParseException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and inner exception.</summary>
    public AtomFeedParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
