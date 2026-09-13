using System.Text.Json;
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

            // Capture the per-field old → new values for the change log.
            var changes = BuildChanges(evt, existing, employee);

            var entity = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == evt.EmployeeId, ct);
            if (entity is null)
            {
                entity = new Data.Employee { EmployeeId = employee.EmployeeId };
                _db.Employees.Add(entity);
            }

            entity.FirstName = employee.FirstName;
            entity.LastName = employee.LastName;
            entity.Email = employee.Email;
            entity.PersonNumber = employee.PersonNumber;
            entity.LegalEmployer = employee.LegalEmployer;
            entity.PositionId = employee.PositionId;
            entity.Job = employee.Job;
            entity.Grade = employee.Grade;
            entity.JobTitle = employee.JobTitle;
            entity.Department = employee.Department;
            entity.WorkLocation = employee.WorkLocation;
            entity.Supervisor = employee.Supervisor;
            entity.EmploymentType = employee.EmploymentType;
            entity.PayBasis = employee.PayBasis;
            entity.StartDate = employee.StartDate;
            entity.BaseSalary = employee.BaseSalary;
            entity.Currency = employee.Currency;
            entity.IsActive = employee.IsActive;
            entity.TerminationDate = employee.TerminationDate;
            entity.TerminationReason = employee.TerminationReason;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (evt.Type is WorkforceEventType.Hire or WorkforceEventType.Transfer
                or WorkforceEventType.Promotion or WorkforceEventType.Rehire)
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
                Message = BuildAuditMessage(evt, existing, employee, changes),
            }, ct);

            foreach (var change in changes)
            {
                await _db.EmployeeChangeLog.AddAsync(new EmployeeChangeLog
                {
                    EmployeeId = evt.EmployeeId,
                    EventId = evt.EventId,
                    AtUtc = DateTime.UtcNow,
                    EventType = evt.Type.ToString(),
                    Field = change.Field,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue,
                }, ct);
            }

            await _db.SaveChangesAsync(ct);
            return "Success";
        }
        catch (WorkforceEventValidationException vex)
        {
            // Business-invalid event (e.g. a role change for a terminated person).
            // Not applied — recorded as Rejected with the reason, and parked in
            // the dead-letter queue so it can be inspected and replayed.
            await _db.IntegrationAuditLog.AddAsync(new IntegrationAuditLog
            {
                AtUtc = DateTime.UtcNow,
                Source = "hcm-atom-feed",
                EventType = evt.Type.ToString(),
                EmployeeId = evt.EmployeeId,
                Status = "Rejected",
                Message = vex.Message,
            }, ct);
            await DeadLetterAsync(evt, "Rejected", vex.Message, ct);
            await _db.SaveChangesAsync(ct);
            return "Rejected";
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
            await DeadLetterAsync(evt, "Error", ex.Message, ct);
            await _db.SaveChangesAsync(ct);
            return "Error";
        }
    }

    /// <summary>
    /// Parks an event that could not be applied in the dead-letter queue,
    /// storing its full payload so a later replay can re-drive it.
    /// </summary>
    private Task DeadLetterAsync(WorkforceEvent evt, string status, string reason, CancellationToken ct)
    {
        _db.DeadLetterEvents.Add(new DeadLetterEvent
        {
            EventId = evt.EventId,
            EventType = evt.Type.ToString(),
            EmployeeId = evt.EmployeeId,
            PayloadJson = JsonSerializer.Serialize(evt),
            Reason = reason,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow,
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Replays a dead-lettered event: deserializes its stored payload and runs
    /// it through the normal pipeline again. Used after the underlying state
    /// is corrected (e.g. the employee is rehired, so a stale position change
    /// is now valid). Returns the same result codes as <see cref="ProcessAsync"/>.
    /// </summary>
    public async Task<string> ReplayAsync(long deadLetterId, CancellationToken ct)
    {
        var dlq = await _db.DeadLetterEvents.FirstOrDefaultAsync(d => d.Id == deadLetterId, ct);
        if (dlq is null)
        {
            return "NotFound";
        }

        var evt = JsonSerializer.Deserialize<WorkforceEvent>(dlq.PayloadJson);
        if (evt is null)
        {
            dlq.Status = "Discarded";
            dlq.LastResult = "Unreadable payload";
            await _db.SaveChangesAsync(ct);
            return "Error";
        }

        var result = await ProcessAsync(evt, ct);

        dlq.ReplayedAtUtc = DateTime.UtcNow;
        dlq.LastResult = result;
        dlq.Status = result == "Success" ? "Replayed" : "Pending";
        await _db.SaveChangesAsync(ct);
        return result;
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
                Job = evt.Job ?? "General",
                Grade = evt.Grade ?? "G-1",
                JobTitle = evt.JobTitle,
                Department = evt.Department ?? "Unknown",
                Location = evt.WorkLocation,
                Supervisor = evt.Supervisor,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.Positions.Add(position);
        }
        else
        {
            if (!string.IsNullOrEmpty(evt.Job))
            {
                position.Job = evt.Job;
            }
            if (!string.IsNullOrEmpty(evt.Grade))
            {
                position.Grade = evt.Grade;
            }
            position.JobTitle = evt.JobTitle;
            if (!string.IsNullOrEmpty(evt.Department))
            {
                position.Department = evt.Department;
            }
            if (!string.IsNullOrEmpty(evt.WorkLocation))
            {
                position.Location = evt.WorkLocation;
            }
            if (!string.IsNullOrEmpty(evt.Supervisor))
            {
                position.Supervisor = evt.Supervisor;
            }
        }
    }

    /// <summary>
    /// Computes the per-field old → new values for an applied event. A hire
    /// records every field as a new value (old is null); other events record
    /// only the fields that actually changed.
    /// </summary>
    private static List<Change> BuildChanges(
        WorkforceEvent evt, Data.Employee? existing, Core.Models.Employee employee)
    {
        var changes = new List<Change>();

        if (existing is null)
        {
            // New hire — everything is a new value.
            changes.Add(new Change("Person number", null, employee.PersonNumber));
            changes.Add(new Change("Name", null, $"{employee.FirstName} {employee.LastName}"));
            changes.Add(new Change("Email", null, employee.Email));
            changes.Add(new Change("Legal employer", null, employee.LegalEmployer));
            changes.Add(new Change("Job", null, employee.Job));
            changes.Add(new Change("Grade", null, employee.Grade));
            changes.Add(new Change("Job title", null, employee.JobTitle));
            changes.Add(new Change("Department", null, employee.Department));
            changes.Add(new Change("Work location", null, employee.WorkLocation));
            changes.Add(new Change("Supervisor", null, employee.Supervisor));
            changes.Add(new Change("Employment type", null, employee.EmploymentType));
            changes.Add(new Change("Pay basis", null, employee.PayBasis));
            changes.Add(new Change("Start date", null, employee.StartDate.ToString("yyyy-MM-dd")));
            changes.Add(new Change("Base salary", null, FormatSalary(employee.BaseSalary, employee.Currency)));
            changes.Add(new Change("Status", null, "Active"));
            return changes;
        }

        if (existing.JobTitle != employee.JobTitle)
        {
            changes.Add(new Change("Job title", existing.JobTitle, employee.JobTitle));
        }
        if (existing.Job != employee.Job)
        {
            changes.Add(new Change("Job", existing.Job, employee.Job));
        }
        if (existing.Grade != employee.Grade)
        {
            changes.Add(new Change("Grade", existing.Grade, employee.Grade));
        }
        if (existing.Department != employee.Department)
        {
            changes.Add(new Change("Department", existing.Department, employee.Department));
        }
        if (existing.WorkLocation != employee.WorkLocation)
        {
            changes.Add(new Change("Work location", existing.WorkLocation, employee.WorkLocation));
        }
        if (existing.Supervisor != employee.Supervisor)
        {
            changes.Add(new Change("Supervisor", existing.Supervisor, employee.Supervisor));
        }
        if (existing.BaseSalary != employee.BaseSalary)
        {
            changes.Add(new Change("Base salary",
                FormatSalary(existing.BaseSalary, existing.Currency),
                FormatSalary(employee.BaseSalary, employee.Currency)));
        }
        if (existing.IsActive != employee.IsActive)
        {
            changes.Add(new Change("Status",
                existing.IsActive ? "Active" : "Terminated",
                employee.IsActive ? "Active" : "Terminated"));
        }
        if (employee.TerminationDate is not null && existing.TerminationDate != employee.TerminationDate)
        {
            changes.Add(new Change("Last working day",
                existing.TerminationDate?.ToString("yyyy-MM-dd"),
                employee.TerminationDate.Value.ToString("yyyy-MM-dd")));
        }
        if (employee.TerminationReason is not null && existing.TerminationReason != employee.TerminationReason)
        {
            changes.Add(new Change("Termination reason",
                existing.TerminationReason,
                employee.TerminationReason));
        }

        return changes;
    }

    private static string FormatSalary(decimal salary, string currency) =>
        $"{currency} {salary:N0}";

    /// <summary>
    /// Builds a concise, human-readable audit message per event type, e.g.
    /// "Ada Lovelace — promoted to Staff Software Engineer, USD 128,699".
    /// </summary>
    private static string BuildAuditMessage(
        WorkforceEvent evt,
        Data.Employee? existing,
        Core.Models.Employee employee,
        List<Change> changes)
    {
        var name = $"{employee.FirstName} {employee.LastName}";
        var money = (decimal salary, string currency) => FormatSalary(salary, currency);

        return evt.Type switch
        {
            WorkforceEventType.Hire =>
                $"{name} — hired as {employee.JobTitle} ({employee.Department}), {money(employee.BaseSalary, employee.Currency)}",

            WorkforceEventType.Transfer =>
                $"{name} — transferred {existing?.JobTitle} → {employee.JobTitle}" +
                (employee.Department != (existing?.Department ?? "")
                    ? $", {existing!.Department} → {employee.Department}"
                    : ""),

            WorkforceEventType.Promotion =>
                $"{name} — promoted to {employee.JobTitle} ({employee.Grade})" +
                (employee.BaseSalary != (existing?.BaseSalary ?? 0m)
                    ? $", {money(existing!.BaseSalary, existing!.Currency)} → {money(employee.BaseSalary, employee.Currency)}"
                    : ""),

            WorkforceEventType.PayChange =>
                $"{name} — pay {money(existing!.BaseSalary, existing!.Currency)} → {money(employee.BaseSalary, employee.Currency)}",

            WorkforceEventType.Termination =>
                $"{name} — terminated {employee.TerminationDate:yyyy-MM-dd}" +
                (employee.TerminationReason is not null ? $" ({employee.TerminationReason})" : ""),

            WorkforceEventType.Rehire =>
                $"{name} — rehired as {employee.JobTitle} ({employee.Department}), {money(employee.BaseSalary, employee.Currency)}",

            _ => $"{name} — {evt.Type}",
        };
    }

    private sealed record Change(string Field, string? OldValue, string? NewValue);

    private static Core.Models.Employee? ToCore(Data.Employee? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new Core.Models.Employee(
            entity.EmployeeId,
            entity.PersonNumber,
            entity.FirstName,
            entity.LastName,
            entity.Email,
            entity.LegalEmployer,
            entity.PositionId,
            entity.Job,
            entity.Grade,
            entity.JobTitle,
            entity.Department,
            entity.WorkLocation,
            entity.Supervisor,
            entity.EmploymentType,
            entity.PayBasis,
            entity.StartDate,
            entity.BaseSalary,
            entity.Currency,
            entity.IsActive,
            entity.TerminationDate,
            entity.TerminationReason);
    }
}
