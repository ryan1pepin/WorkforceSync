using System.Security.Cryptography;

namespace WorkforceSync.Api.Auth;

/// <summary>
/// PBKDF2-SHA256 password hashing. Stored format:
/// <c>PBKDF2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 key&gt;</c>.
/// Verification uses a fixed-time comparison to avoid timing attacks.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>Hashes a password with a fresh random salt.</summary>
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>Verifies a password against a stored hash. Returns false on any parse error.</summary>
    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        try
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2")
            {
                return false;
            }

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
