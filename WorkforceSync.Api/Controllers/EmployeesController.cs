using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkforceSync.Api.Data;
using WorkforceSync.Api.Dtos;

namespace WorkforceSync.Api.Controllers;

/// <summary>Workforce employee endpoints (CRUD + query).</summary>
[ApiController]
[Authorize]
[Route("employees")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists employees with optional filters and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? status = null,
        [FromQuery] string? department = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        var query = _db.Employees.AsNoTracking().AsQueryable();

        // `status` (active|terminated) takes precedence; `activeOnly` is kept
        // for backward compatibility.
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.Trim().ToLowerInvariant() switch
            {
                "active" => query.Where(e => e.IsActive),
                "terminated" => query.Where(e => !e.IsActive),
                _ => query,
            };
        }
        else if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(e => e.Department == department);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Multi-word search: every term must match one of the fields,
            // so "Leslie He" finds the person rather than requiring a
            // single field to contain the whole phrase.
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var t in terms)
            {
                query = query.Where(e =>
                    e.FirstName.Contains(t) ||
                    e.LastName.Contains(t) ||
                    e.Email.Contains(t) ||
                    e.JobTitle.Contains(t));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<EmployeeDto>(
            items.Select(ToDto).ToList(), totalCount, page, pageSize));
    }

    /// <summary>Gets a single employee by id.</summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string employeeId, CancellationToken ct)
    {
        var emp = await _db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);
        return emp is null ? NotFound() : Ok(ToDto(emp));
    }

    /// <summary>
    /// Gets an employee's change history (newest first): every event applied to
    /// them, with the old → new value of each field it changed.
    /// </summary>
    [HttpGet("{employeeId}/changes")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChanges(string employeeId, CancellationToken ct)
    {
        var exists = await _db.Employees.AsNoTracking()
            .AnyAsync(e => e.EmployeeId == employeeId, ct);
        if (!exists)
        {
            return NotFound();
        }

        var rows = await _db.EmployeeChangeLog.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.AtUtc)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        var changes = rows
            .GroupBy(c => c.EventId)
            .Select(g => new EmployeeChangeDto(
                g.Key,
                g.Max(c => c.AtUtc),
                g.First().EventType,
                g.Select(c => new EmployeeChangeFieldDto(c.Field, c.OldValue, c.NewValue)).ToList()))
            .ToList();

        return Ok(changes);
    }

    /// <summary>Creates an employee. Returns 201.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] EmployeeCreate request, CancellationToken ct)
    {
        var id = string.IsNullOrWhiteSpace(request.EmployeeId)
            ? $"emp-{Guid.NewGuid():N}"[..12]
            : request.EmployeeId.Trim();

        if (await _db.Employees.AnyAsync(e => e.EmployeeId == id, ct))
        {
            return Problem(detail: $"Employee '{id}' already exists.", statusCode: 409);
        }

        var now = DateTime.UtcNow;
        var emp = new Employee
        {
            EmployeeId = id,
            PersonNumber = request.PersonNumber.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            LegalEmployer = request.LegalEmployer.Trim(),
            PositionId = request.PositionId.Trim(),
            Job = request.Job.Trim(),
            Grade = request.Grade.Trim(),
            JobTitle = request.JobTitle.Trim(),
            Department = request.Department.Trim(),
            WorkLocation = request.WorkLocation.Trim(),
            Supervisor = request.Supervisor.Trim(),
            EmploymentType = request.EmploymentType.Trim(),
            PayBasis = request.PayBasis.Trim(),
            StartDate = request.StartDate,
            BaseSalary = request.BaseSalary,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            IsActive = true,
            UpdatedAtUtc = now,
        };

        _db.Employees.Add(emp);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { employeeId = id }, ToDto(emp));
    }

    /// <summary>Updates an employee. Returns 200.</summary>
    [HttpPatch("{employeeId}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string employeeId, [FromBody] EmployeeUpdate request, CancellationToken ct)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);
        if (emp is null)
        {
            return NotFound();
        }

        if (request.PersonNumber is not null) emp.PersonNumber = request.PersonNumber.Trim();
        if (request.FirstName is not null) emp.FirstName = request.FirstName.Trim();
        if (request.LastName is not null) emp.LastName = request.LastName.Trim();
        if (request.Email is not null) emp.Email = request.Email.Trim().ToLowerInvariant();
        if (request.LegalEmployer is not null) emp.LegalEmployer = request.LegalEmployer.Trim();
        if (request.PositionId is not null) emp.PositionId = request.PositionId.Trim();
        if (request.Job is not null) emp.Job = request.Job.Trim();
        if (request.Grade is not null) emp.Grade = request.Grade.Trim();
        if (request.JobTitle is not null) emp.JobTitle = request.JobTitle.Trim();
        if (request.Department is not null) emp.Department = request.Department.Trim();
        if (request.WorkLocation is not null) emp.WorkLocation = request.WorkLocation.Trim();
        if (request.Supervisor is not null) emp.Supervisor = request.Supervisor.Trim();
        if (request.EmploymentType is not null) emp.EmploymentType = request.EmploymentType.Trim();
        if (request.PayBasis is not null) emp.PayBasis = request.PayBasis.Trim();
        if (request.StartDate is not null) emp.StartDate = request.StartDate.Value;
        if (request.BaseSalary is not null) emp.BaseSalary = request.BaseSalary.Value;
        if (request.Currency is not null) emp.Currency = request.Currency.Trim().ToUpperInvariant();
        if (request.IsActive is not null) emp.IsActive = request.IsActive.Value;

        emp.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(emp));
    }

    /// <summary>Soft-deletes an employee (sets IsActive=false). Returns 204.</summary>
    [HttpDelete("{employeeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string employeeId, CancellationToken ct)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);
        if (emp is null)
        {
            return NotFound();
        }

        emp.IsActive = false;
        emp.TerminationDate ??= DateTime.UtcNow;
        emp.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static EmployeeDto ToDto(Employee e) => new(
        e.EmployeeId, e.PersonNumber, e.FirstName, e.LastName, e.Email, e.LegalEmployer,
        e.PositionId, e.Job, e.Grade, e.JobTitle, e.Department, e.WorkLocation,
        e.Supervisor, e.EmploymentType, e.PayBasis, e.StartDate, e.BaseSalary, e.Currency,
        e.IsActive, e.TerminationDate, e.TerminationReason, e.UpdatedAtUtc);
}
