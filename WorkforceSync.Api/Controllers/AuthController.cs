using Microsoft.AspNetCore.Mvc;
using WorkforceSync.Api.Auth;
using WorkforceSync.Api.Dtos;
using WorkforceSync.Api.Services;

namespace WorkforceSync.Api.Controllers;

/// <summary>Authentication endpoints: register, login, refresh, logout.</summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Registers a new user. Returns 201 with an auth token pair.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _auth.RegisterAsync(
                request.Email, request.FirstName, request.LastName, request.Password, ct);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (AuthException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>Logs in. Returns 200 with an auth token pair.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _auth.LoginAsync(request.Email, request.Password, ct);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>Rotates a refresh token. Returns 200 with a new token pair.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _auth.RefreshAsync(request.RefreshToken, ct);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.StatusCode);
        }
    }

    /// <summary>Revokes a refresh token (and its family). Returns 204.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
