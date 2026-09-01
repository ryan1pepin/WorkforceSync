using System.ComponentModel.DataAnnotations;

namespace WorkforceSync.Api.Dtos;

/// <summary>A position (job slot) as exposed by the API.</summary>
public sealed record PositionDto(
    string PositionId,
    string JobTitle,
    string Department,
    string? Location,
    DateTime CreatedAtUtc);

/// <summary>Request body for POST /positions.</summary>
public sealed record PositionCreate(
    string? PositionId,
    [Required] string JobTitle,
    [Required] string Department,
    string? Location);
