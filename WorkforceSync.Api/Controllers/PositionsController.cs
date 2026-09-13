using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceSync.Api.Data;
using WorkforceSync.Api.Dtos;

namespace WorkforceSync.Api.Controllers;

/// <summary>Position (job slot) endpoints.</summary>
[ApiController]
[Authorize]
[Route("positions")]
public class PositionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PositionsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists all positions.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var items = await _db.Positions.AsNoTracking()
            .OrderBy(p => p.Department).ThenBy(p => p.JobTitle)
            .ToListAsync(ct);
        return Ok(items.Select(ToDto).ToList());
    }

    /// <summary>Gets a single position by id.</summary>
    [HttpGet("{positionId}")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string positionId, CancellationToken ct)
    {
        var pos = await _db.Positions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PositionId == positionId, ct);
        return pos is null ? NotFound() : Ok(ToDto(pos));
    }

    /// <summary>Creates a position. Returns 201.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] PositionCreate request, CancellationToken ct)
    {
        var id = string.IsNullOrWhiteSpace(request.PositionId)
            ? $"pos-{Guid.NewGuid():N}"[..12]
            : request.PositionId.Trim();

        if (await _db.Positions.AnyAsync(p => p.PositionId == id, ct))
        {
            return Problem(detail: $"Position '{id}' already exists.", statusCode: 409);
        }

        var pos = new Position
        {
            PositionId = id,
            Job = request.Job.Trim(),
            Grade = request.Grade.Trim(),
            JobTitle = request.JobTitle.Trim(),
            Department = request.Department.Trim(),
            Location = request.Location?.Trim(),
            Supervisor = request.Supervisor?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.Positions.Add(pos);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { positionId = id }, ToDto(pos));
    }

    private static PositionDto ToDto(Position p) => new(
        p.PositionId, p.Job, p.Grade, p.JobTitle, p.Department, p.Location, p.Supervisor, p.CreatedAtUtc);
}
