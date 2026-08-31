using WorkforceSync.Core.Models;
using WorkforceSync.Core.Validation;
using Xunit;

namespace WorkforceSync.Core.Tests;

public class WorkforceEventValidatorTests
{
    private readonly WorkforceEventValidator _validator = new();

    [Fact]
    public void Validate_ValidHire_NoErrors()
    {
        var errors = _validator.Validate(AtomFixtures.ValidHire);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_Hire_MissingRequiredFields_ReturnsErrors()
    {
        var bad = AtomFixtures.ValidHire with
        {
            FirstName = null,
            LastName = null,
            Email = null,
            PositionId = null,
            JobTitle = null,
            Department = null,
            StartDate = null,
            BaseSalary = null,
            Currency = null,
        };

        var errors = _validator.Validate(bad);

        Assert.Contains(errors, e => e.Contains("FirstName"));
        Assert.Contains(errors, e => e.Contains("LastName"));
        Assert.Contains(errors, e => e.Contains("Email"));
        Assert.Contains(errors, e => e.Contains("PositionId"));
        Assert.Contains(errors, e => e.Contains("JobTitle"));
        Assert.Contains(errors, e => e.Contains("Department"));
        Assert.Contains(errors, e => e.Contains("StartDate"));
        Assert.Contains(errors, e => e.Contains("BaseSalary"));
        Assert.Contains(errors, e => e.Contains("Currency"));
    }

    [Fact]
    public void Validate_Hire_BadEmail_ReturnsError()
    {
        var bad = AtomFixtures.ValidHire with { Email = "not-an-email" };
        var errors = _validator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("Email"));
    }

    [Fact]
    public void Validate_Hire_ZeroSalary_ReturnsError()
    {
        var bad = AtomFixtures.ValidHire with { BaseSalary = 0m };
        var errors = _validator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("BaseSalary"));
    }

    [Fact]
    public void Validate_Termination_EndDateAfterOccurred_ReturnsError()
    {
        var bad = AtomFixtures.ValidTermination with
        {
            EndDate = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc), // after OccurredAt (9/2)
        };

        var errors = _validator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("EndDate"));
    }

    [Fact]
    public void Validate_Termination_Valid_NoErrors()
    {
        var errors = _validator.Validate(AtomFixtures.ValidTermination);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_CompensationChange_ZeroSalary_ReturnsError()
    {
        var bad = AtomFixtures.ValidCompensationChange with { BaseSalary = 0m };
        var errors = _validator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("BaseSalary"));
    }

    [Fact]
    public void Validate_CompensationChange_Valid_NoErrors()
    {
        var errors = _validator.Validate(AtomFixtures.ValidCompensationChange);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_FutureOccurredAt_WithNow_ReturnsError()
    {
        var future = AtomFixtures.ValidHire with
        {
            OccurredAt = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var now = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var errors = _validator.Validate(future, now);
        Assert.Contains(errors, e => e.Contains("future"));
    }

    [Fact]
    public void Validate_MissingEmployeeId_ReturnsError()
    {
        var bad = AtomFixtures.ValidHire with { EmployeeId = "" };
        var errors = _validator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("EmployeeId"));
    }
}
