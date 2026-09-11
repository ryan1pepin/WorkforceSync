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

    /// <summary>
    /// Pipeline metrics for the observability card: totals by outcome,
    /// dead-letter backlog, recent throughput, and queue depth.
    /// </summary>
    [HttpGet("metrics")]
    [Authorize]
    [ProducesResponseType(typeof(MetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Metrics(CancellationToken ct)
    {
        var total = await _db.IntegrationAuditLog.CountAsync(ct);
        var applied = await _db.IntegrationAuditLog.CountAsync(a => a.Status == "Success", ct);
        var rejected = await _db.IntegrationAuditLog.CountAsync(a => a.Status == "Rejected", ct);
        var errors = await _db.IntegrationAuditLog.CountAsync(a => a.Status == "Error", ct);
        var dlqPending = await _db.DeadLetterEvents.CountAsync(d => d.Status == "Pending", ct);
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var lastMinute = await _db.IntegrationAuditLog.CountAsync(a => a.AtUtc >= oneMinuteAgo, ct);

        return Ok(new MetricsDto(
            EventsTotal: total,
            Applied: applied,
            Rejected: rejected,
            Errors: errors,
            DeadLetterPending: dlqPending,
            EventsLastMinute: lastMinute,
            QueueDepth: _status.QueueDepth,
            LastIngestAtUtc: _status.LastIngestAtUtc,
            TimestampUtc: DateTime.UtcNow));
    }

    /// <summary>
    /// Lists dead-lettered events (rejected or errored), newest first.
    /// Filterable by status (Pending | Replayed | Discarded).
    /// </summary>
    [HttpGet("dead-letter")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DeadLetterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeadLetter(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        var query = _db.DeadLetterEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status);
        }

        var items = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(items.Select(d => new DeadLetterDto(
            d.Id, d.EventId, d.EventType, d.EmployeeId, d.Reason,
            d.Status, d.CreatedAtUtc, d.ReplayedAtUtc, d.LastResult)).ToList());
    }

    /// <summary>
    /// Replays a dead-lettered event through the pipeline again (after the
    /// underlying state is corrected). Returns the outcome.
    /// </summary>
    [HttpPost("dead-letter/{id:long}/replay")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplayDeadLetter(long id, CancellationToken ct)
    {
        var processor = HttpContext.RequestServices.GetRequiredService<EventProcessor>();
        var result = await processor.ReplayAsync(id, ct);
        return result == "NotFound" ? NotFound() : Ok(new { id, result });
    }

    /// <summary>Discards a dead-lettered event (acknowledged, not replayed).</summary>
    [HttpDelete("dead-letter/{id:long}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DiscardDeadLetter(long id, CancellationToken ct)
    {
        var dlq = await _db.DeadLetterEvents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dlq is null)
        {
            return NotFound();
        }

        dlq.Status = "Discarded";
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
