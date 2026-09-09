using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceSync.Api.Data;
using WorkforceSync.Api.Dtos;
using WorkforceSync.Api.Services;

namespace WorkforceSync.Api.Controllers;

/// <summary>Integration pipeline health + audit log.</summary>
[ApiController]
[Route("integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IntegrationStatus _status;

    public IntegrationsController(AppDbContext db, IntegrationStatus status)
    {
        _db = db;
        _status = status;
    }

    /// <summary>Pipeline health (public — useful for monitoring).</summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var dbOk = false;
        try
        {
            dbOk = await _db.Database.CanConnectAsync(ct);
        }
        catch
        {
            dbOk = false;
        }

        var dto = new HealthDto(
            Status: dbOk ? _status.Status : "Degraded",
            Database: dbOk,
            LastIngestAtUtc: _status.LastIngestAtUtc,
            QueueDepth: _status.QueueDepth,
            TimestampUtc: DateTime.UtcNow);

        return Ok(dto);
    }

    /// <summary>
    /// Recent integration audit log entries (newest first). Filterable by
    /// status, event type, and a free-text search over employee id / message.
    /// </summary>
    [HttpGet("audit")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AuditDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Audit(
        [FromQuery] int limit = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        var query = _db.IntegrationAuditLog.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(a => a.EventType == eventType);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a =>
                (a.EmployeeId != null && a.EmployeeId.Contains(s)) ||
                (a.Message != null && a.Message.Contains(s)));
        }

        var items = await query
            .OrderByDescending(a => a.AtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(items.Select(a => new AuditDto(
            a.Id, a.AtUtc, a.Source, a.EventType, a.EmployeeId, a.Status, a.Message)).ToList());
    }
}
