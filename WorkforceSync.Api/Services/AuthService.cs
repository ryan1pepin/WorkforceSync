using Microsoft.EntityFrameworkCore;
using WorkforceSync.Api.Auth;
using WorkforceSync.Api.Data;
using WorkforceSync.Api.Dtos;

namespace WorkforceSync.Api.Services;

/// <summary>
/// Authentication flows: register, login, refresh (with rotation + reuse
/// detection), and logout.
///
/// Refresh-token security model:
/// - Only the SHA-256 hash of each refresh token is stored.
/// - Each login/register starts a new token <em>family</em> (FamilyId).
/// - On refresh, the presented token is revoked and a new one is issued in the
///   same family (rotation).
/// - If a <em>revoked</em> token is presented again (theft/reuse), the entire
///   family is revoked — the legitimate holder must log in again.
/// </summary>
public sealed class AuthService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthService(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    /// <summary>Registers a new user and returns an auth token pair.</summary>
    public async Task<AuthResponse> RegisterAsync(
        string email, string firstName, string lastName, string password, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
        {
            throw new AuthException(409, $"An account with email '{email}' already exists.");
        }

        var user = new User
        {
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await IssueTokenPairAsync(user, ct);
    }

    /// <summary>Verifies credentials and returns an auth token pair.</summary>
    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new AuthException(401, "Invalid email or password.");

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            throw new AuthException(401, "Invalid email or password.");
        }

        return await IssueTokenPairAsync(user, ct);
    }

    /// <summary>
    /// Rotates a refresh token: revokes the presented one, issues a new pair in
    /// the same family. Detects reuse of an already-revoked token and revokes
    /// the whole family.
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            throw new AuthException(401, "Refresh token is not recognized.");
        }

        if (stored.RevokedAtUtc is not null)
        {
            // Reuse of a rotated token — possible theft. Revoke the whole family.
            await RevokeFamilyAsync(stored.FamilyId, ct);
            throw new AuthException(401, "Refresh token reuse detected; token family revoked.");
        }

        if (stored.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new AuthException(401, "Refresh token has expired.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct)
            ?? throw new AuthException(401, "User no longer exists.");

        // Rotate: revoke old, issue new in the same family.
        var (newToken, newHash) = _tokens.CreateRefreshToken();
        stored.RevokedAtUtc = DateTime.UtcNow;

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid().ToString("n"),
            UserId = user.Id,
            FamilyId = stored.FamilyId,
            TokenHash = newHash,
            ExpiresAtUtc = DateTime.UtcNow + RefreshLifetime,
            CreatedAtUtc = DateTime.UtcNow,
        };
        stored.ReplacedByTokenId = replacement.Id;

        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(ToDto(user), _tokens.CreateAccessToken(user), newToken);
    }

    /// <summary>Revokes the presented refresh token (and its family).</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
        {
            return; // idempotent
        }

        await RevokeFamilyAsync(stored.FamilyId, ct);
    }

    private async Task<AuthResponse> IssueTokenPairAsync(User user, CancellationToken ct)
    {
        var (refreshToken, refreshHash) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid().ToString("n"),
            UserId = user.Id,
            FamilyId = Guid.NewGuid().ToString("n"),
            TokenHash = refreshHash,
            ExpiresAtUtc = DateTime.UtcNow + RefreshLifetime,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(ToDto(user), _tokens.CreateAccessToken(user), refreshToken);
    }

    private async Task RevokeFamilyAsync(string familyId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var family = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in family)
        {
            token.RevokedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static UserDto ToDto(User user) => new(
        user.Id, user.Email, user.FirstName, user.LastName, user.CreatedAtUtc);
}
