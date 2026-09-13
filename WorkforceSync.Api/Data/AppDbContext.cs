using Microsoft.EntityFrameworkCore;

namespace WorkforceSync.Api.Data;

/// <summary>An API user (login credential holder).</summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A stored refresh token. Only the SHA-256 hash is persisted (never the raw token).
/// Tokens belong to a <see cref="FamilyId"/> so that reuse of a rotated token can
/// revoke the entire family (defense against refresh-token theft).
/// </summary>
public class RefreshToken
{
    public string Id { get; set; } = null!;
    public int UserId { get; set; }
    public User? User { get; set; }
    public string FamilyId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenId { get; set; }
}

/// <summary>A workforce employee (normalized from HCM events).</summary>
public class Employee
{
    public string EmployeeId { get; set; } = null!;
    public string PersonNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string LegalEmployer { get; set; } = null!;
    public string PositionId { get; set; } = null!;
    public string Job { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string WorkLocation { get; set; } = null!;
    public string Supervisor { get; set; } = null!;
    public string EmploymentType { get; set; } = null!;
    public string PayBasis { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public decimal BaseSalary { get; set; }
    public string Currency { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Records an event id that has already been applied. Makes the pipeline
/// idempotent: a redelivered event (poll overlap, replay) is skipped.
/// </summary>
public class ProcessedEvent
{
    public string EventId { get; set; } = null!;
    public DateTime ProcessedAtUtc { get; set; }
}

/// <summary>A position (job slot) that employees can hold.</summary>
public class Position
{
    public string PositionId { get; set; } = null!;
    public string Job { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string? Location { get; set; }
    public string? Supervisor { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>An audit entry for the integration pipeline (ingest, transform, persist).</summary>
public class IntegrationAuditLog
{
    public long Id { get; set; }
    public DateTime AtUtc { get; set; }
    public string Source { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string? EmployeeId { get; set; }
    public string Status { get; set; } = null!; // "Success" | "Error"
    public string? Message { get; set; }
}

/// <summary>
/// A per-field change to an employee, captured when an HCM event is applied.
/// Groups by <see cref="EventId"/> so the UI can show one card per event with
/// its old → new values (e.g. a salary change shows the old and new pay).
/// </summary>
public class EmployeeChangeLog
{
    public long Id { get; set; }
    public string EmployeeId { get; set; } = null!;
    public string EventId { get; set; } = null!;
    public DateTime AtUtc { get; set; }
    public string EventType { get; set; } = null!;
    public string Field { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// A dead-lettered event: one the pipeline could not apply (rejected as
/// business-invalid, or errored) and parked for inspection and replay instead
/// of dropping or wedging the feed. The full event payload is stored so a
/// replay can re-drive it once the underlying state is corrected.
/// </summary>
public class DeadLetterEvent
{
    public long Id { get; set; }
    public string EventId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string? EmployeeId { get; set; }
    /// <summary>Serialized <c>WorkforceEvent</c> — the source of truth for a replay.</summary>
    public string PayloadJson { get; set; } = null!;
    /// <summary>Why it was dead-lettered (the rejection / error reason).</summary>
    public string Reason { get; set; } = null!;
    /// <summary>"Pending" | "Replayed" | "Discarded".</summary>
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReplayedAtUtc { get; set; }
    /// <summary>Outcome of the most recent replay attempt, if any.</summary>
    public string? LastResult { get; set; }
}

/// <summary>EF Core DbContext for WorkforceSync.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<IntegrationAuditLog> IntegrationAuditLog => Set<IntegrationAuditLog>();
    public DbSet<EmployeeChangeLog> EmployeeChangeLog => Set<EmployeeChangeLog>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => x.FamilyId);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.EmployeeId);
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.Department);
            e.Property(x => x.BaseSalary).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Position>(e =>
        {
            e.HasKey(x => x.PositionId);
            e.HasIndex(x => x.Department);
        });

        modelBuilder.Entity<ProcessedEvent>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasIndex(x => x.ProcessedAtUtc);
        });

        modelBuilder.Entity<IntegrationAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AtUtc);
        });

        modelBuilder.Entity<EmployeeChangeLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EmployeeId, x.AtUtc });
            e.HasIndex(x => x.EventId);
            e.Property(x => x.Field).HasMaxLength(64);
            e.Property(x => x.OldValue).HasMaxLength(512);
            e.Property(x => x.NewValue).HasMaxLength(512);
        });

        modelBuilder.Entity<DeadLetterEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.EventType).HasMaxLength(32);
            e.Property(x => x.Reason).HasMaxLength(1024);
            e.Property(x => x.Status).HasMaxLength(16);
        });
    }
}
