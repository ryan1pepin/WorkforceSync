using Microsoft.EntityFrameworkCore;
using WorkforceSync.Api.Data;
using WorkforceSync.Core.Models;
using WorkforceSync.Core.Transform;

namespace WorkforceSync.Api.Services;

/// <summary>
/// Consumes events from the <see cref="EventQueue"/> and applies them to the
/// database: transform → upsert employee → upsert position → audit log.
///
/// Idempotency: each event id is recorded in <see cref="ProcessedEvent"/>.
/// A redelivered event (poll overlap, replay) is skipped, so the pipeline is
/// safe to re-run. The event carries an id + occurred-at timestamp and the
/// consumer re-reads current state, so redelivery is cheap.
/// </summary>
public sealed class EventProcessor
{
    private readonly AppDbContext _db;
    private readonly WorkforceEventTransformer _transformer = new();

    public EventProcessor(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Processes one event end-to-end.
    /// </summary>
    /// <returns>
    /// "Success" | "Skipped" (already processed) | "Error" (with the reason
    /// in the audit log).
    /// </returns>
    public async Task<string> ProcessAsync(WorkforceEvent evt, CancellationToken ct)
    {
        if (await AlreadyProcessedAsync(evt.EventId, ct))
        {
            return "Skipped";
        }

        try
        {
            var existing = await _db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == evt.EmployeeId, ct);

            var employee = _transformer.Apply(evt, ToCore(existing));

            var entity = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == evt.EmployeeId, ct);
            if (entity is null)
            {
                entity = new Data.Employee { EmployeeId = employee.EmployeeId };
                _db.Employees.Add(entity);
            }

            entity.FirstName = employee.FirstName;
            entity.LastName = employee.LastName;
            entity.Email = employee.Email;
            entity.PositionId = employee.PositionId;
            entity.JobTitle = employee.JobTitle;
            entity.Department = employee.Department;
            entity.StartDate = employee.StartDate;
            entity.BaseSalary = employee.BaseSalary;
            entity.Currency = employee.Currency;
            entity.IsActive = employee.IsActive;
            entity.TerminationDate = employee.TerminationDate;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (evt.Type is WorkforceEventType.Hire or WorkforceEventType.PositionChange)
            {
                await UpsertPositionAsync(evt, ct);
            }

            await _db.ProcessedEvents.AddAsync(new ProcessedEvent
            {
                EventId = evt.EventId,
                ProcessedAtUtc = DateTime.UtcNow,
            }, ct);

            await _db.IntegrationAuditLog.AddAsync(new IntegrationAuditLog
            {
                AtUtc = DateTime.UtcNow,
                Source = "hcm-atom-feed",
                EventType = evt.Type.ToString(),
                EmployeeId = evt.EmployeeId,
                Status = "Success",
                Message = $"Event {evt.EventId} applied.",
            }, ct);

            await _db.SaveChangesAsync(ct);
            return "Success";
        }
        catch (Exception ex)
        {
            await _db.IntegrationAuditLog.AddAsync(new IntegrationAuditLog
            {
                AtUtc = DateTime.UtcNow,
                Source = "hcm-atom-feed",
                EventType = evt.Type.ToString(),
                EmployeeId = evt.EmployeeId,
                Status = "Error",
                Message = ex.Message,
            }, ct);
            await _db.SaveChangesAsync(ct);
            return "Error";
        }
    }

    private async Task<bool> AlreadyProcessedAsync(string eventId, CancellationToken ct)
    {
        return await _db.ProcessedEvents.AsNoTracking()
            .AnyAsync(p => p.EventId == eventId, ct);
    }

    private async Task UpsertPositionAsync(WorkforceEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(evt.PositionId) || string.IsNullOrEmpty(evt.JobTitle))
        {
            return;
        }

        var position = await _db.Positions.FirstOrDefaultAsync(p => p.PositionId == evt.PositionId, ct);
        if (position is null)
        {
            position = new Position
            {
                PositionId = evt.PositionId,
                JobTitle = evt.JobTitle,
                Department = evt.Department ?? "Unknown",
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.Positions.Add(position);
        }
        else
        {
            position.JobTitle = evt.JobTitle;
            if (!string.IsNullOrEmpty(evt.Department))
            {
                position.Department = evt.Department;
            }
        }
    }

    private static Core.Models.Employee? ToCore(Data.Employee? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new Core.Models.Employee(
            entity.EmployeeId,
            entity.FirstName,
            entity.LastName,
            entity.Email,
            entity.PositionId,
            entity.JobTitle,
            entity.Department,
            entity.StartDate,
            entity.BaseSalary,
            entity.Currency,
            entity.IsActive,
            entity.TerminationDate);
    }
}
