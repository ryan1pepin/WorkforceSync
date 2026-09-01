using System.ComponentModel.DataAnnotations;

namespace WorkforceSync.Api.Dtos;

/// <summary>Request body for POST /auth/register.</summary>
public sealed record RegisterRequest(
    [Required] string Email,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Password);

/// <summary>Request body for POST /auth/login.</summary>
public sealed record LoginRequest(
    [Required] string Email,
    [Required] string Password);

/// <summary>Request body for POST /auth/refresh and POST /auth/logout.</summary>
public sealed record RefreshRequest(
    [Required] string RefreshToken);

/// <summary>A user, safe to expose to clients (no password hash).</summary>
public sealed record UserDto(int Id, string Email, string FirstName, string LastName, DateTime CreatedAtUtc);

/// <summary>Response for register/login/refresh.</summary>
public sealed record AuthResponse(UserDto User, string AccessToken, string RefreshToken);
