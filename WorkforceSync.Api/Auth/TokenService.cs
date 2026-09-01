using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WorkforceSync.Api.Data;

namespace WorkforceSync.Api.Auth;

/// <summary>
/// Creates access tokens (JWT, 5-minute expiry) and refresh tokens
/// (64 random bytes; only the SHA-256 hash is stored).
/// </summary>
public sealed class TokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IConfiguration config)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        _issuer = config["Jwt:Issuer"] ?? "WorkforceSync";
        _audience = config["Jwt:Audience"] ?? "WorkforceSync";
    }

    /// <summary>Creates a signed access token for the user (5-minute lifetime).</summary>
    public string CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            }),
        };

        return _handler.CreateToken(descriptor);
    }

    /// <summary>Creates a new random refresh token and its SHA-256 hash.</summary>
    public (string Token, string TokenHash) CreateRefreshToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, HashToken(token));
    }

    /// <summary>SHA-256 hash of a token (lowercase hex) — what gets stored in the DB.</summary>
    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
