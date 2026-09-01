namespace WorkforceSync.Api.Auth;

/// <summary>
/// An authentication/authorization failure with an associated HTTP status code.
/// Caught by controllers and mapped to a ProblemDetails response.
/// </summary>
public sealed class AuthException : Exception
{
    public int StatusCode { get; }

    public AuthException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
