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
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PositionId { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string Department { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public decimal BaseSalary { get; set; }
    public string Currency { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? TerminationDate { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>A position (job slot) that employees can hold.</summary>
public class Position
{
    public string PositionId { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string? Location { get; set; }
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

/// <summary>EF Core DbContext for WorkforceSync.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<IntegrationAuditLog> IntegrationAuditLog => Set<IntegrationAuditLog>();

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

        modelBuilder.Entity<IntegrationAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AtUtc);
        });
    }
}
