using WorkforceSync.Core.Models;
using WorkforceSync.Core.Transform;
using Xunit;

namespace WorkforceSync.Core.Tests;

public class WorkforceEventTransformerTests
{
    private readonly WorkforceEventTransformer _transformer = new();

    [Fact]
    public void Apply_Hire_CreatesActiveEmployee()
    {
        var emp = _transformer.Apply(AtomFixtures.ValidHire);

        Assert.Equal("emp-100", emp.EmployeeId);
        Assert.Equal("Ada", emp.FirstName);
        Assert.Equal("Lovelace", emp.LastName);
        Assert.Equal("ada@example.com", emp.Email);
        Assert.Equal("pos-1", emp.PositionId);
        Assert.Equal("Engineer", emp.JobTitle);
        Assert.Equal("Engineering", emp.Department);
        Assert.Equal(120000m, emp.BaseSalary);
        Assert.Equal("USD", emp.Currency);
        Assert.True(emp.IsActive);
        Assert.Null(emp.TerminationDate);
    }

    [Fact]
    public void Apply_Termination_OnExisting_DeactivatesAndSetsDate()
    {
        var hired = _transformer.Apply(AtomFixtures.ValidHire);
        var terminated = _transformer.Apply(AtomFixtures.ValidTermination, hired);

        Assert.False(terminated.IsActive);
        Assert.Equal(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), terminated.TerminationDate);
        // Other fields preserved
        Assert.Equal("Ada", terminated.FirstName);
        Assert.Equal("pos-1", terminated.PositionId);
    }

    [Fact]
    public void Apply_PositionChange_UpdatesFields()
    {
        var hired = _transformer.Apply(AtomFixtures.ValidHire);
        var changed = _transformer.Apply(AtomFixtures.ValidPositionChange, hired);

        Assert.Equal("pos-2", changed.PositionId);
        Assert.Equal("Senior Engineer", changed.JobTitle);
        Assert.Equal("Platform", changed.Department);
        // Unchanged fields preserved
        Assert.Equal("Ada", changed.FirstName);
        Assert.Equal(120000m, changed.BaseSalary);
    }

    [Fact]
    public void Apply_CompensationChange_UpdatesSalary()
    {
        var hired = _transformer.Apply(AtomFixtures.ValidHire);
        var changed = _transformer.Apply(AtomFixtures.ValidCompensationChange, hired);

        Assert.Equal(135000m, changed.BaseSalary);
        Assert.Equal("USD", changed.Currency);
        // Unchanged fields preserved
        Assert.Equal("Ada", changed.FirstName);
        Assert.Equal("pos-1", changed.PositionId);
    }

    [Fact]
    public void Apply_NonHire_WithNoExisting_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _transformer.Apply(AtomFixtures.ValidTermination));
        Assert.Throws<InvalidOperationException>(
            () => _transformer.Apply(AtomFixtures.ValidPositionChange));
        Assert.Throws<InvalidOperationException>(
            () => _transformer.Apply(AtomFixtures.ValidCompensationChange));
    }

    [Fact]
    public void Apply_Hire_MissingRequiredField_Throws()
    {
        var badHire = AtomFixtures.ValidHire with { Email = null };
        Assert.Throws<InvalidOperationException>(() => _transformer.Apply(badHire));
    }
}
