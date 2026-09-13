using WorkforceSync.Core.Atom;
using WorkforceSync.Core.Models;
using Xunit;

namespace WorkforceSync.Core.Tests;

public class AtomFeedParserTests
{
    private readonly AtomFeedParser _parser = new();

    [Fact]
    public void Parse_ValidMultiEntryFeed_MapsAllFields()
    {
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry(
                "evt-1", "Hire", "2026-09-01T09:00:00Z", "emp-100",
                firstName: "Ada", lastName: "Lovelace", email: "ada@example.com",
                positionId: "pos-1", jobTitle: "Engineer", department: "Engineering",
                startDate: "2026-09-01T00:00:00Z", baseSalary: "120000", currency: "USD"),
            AtomFixtures.Entry(
                "evt-2", "Termination", "2026-09-02T09:00:00Z", "emp-100",
                endDate: "2026-09-02T00:00:00Z"));

        var events = _parser.Parse(xml);

        Assert.Equal(2, events.Count);

        var hire = events[0];
        Assert.Equal("evt-1", hire.EventId);
        Assert.Equal(WorkforceEventType.Hire, hire.Type);
        Assert.Equal(new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), hire.OccurredAt);
        Assert.Equal("emp-100", hire.EmployeeId);
        Assert.Equal("Ada", hire.FirstName);
        Assert.Equal("Lovelace", hire.LastName);
        Assert.Equal("ada@example.com", hire.Email);
        Assert.Equal("pos-1", hire.PositionId);
        Assert.Equal("Engineer", hire.JobTitle);
        Assert.Equal("Engineering", hire.Department);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), hire.StartDate);
        Assert.Equal(120000m, hire.BaseSalary);
        Assert.Equal("USD", hire.Currency);

        var term = events[1];
        Assert.Equal(WorkforceEventType.Termination, term.Type);
        Assert.Equal(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), term.EndDate);
        Assert.Null(term.FirstName);
    }

    [Fact]
    public void Parse_SingleHire_ReturnsOneEvent()
    {
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry(
                "evt-1", "Hire", "2026-09-01T09:00:00Z", "emp-100",
                firstName: "Ada", lastName: "Lovelace", email: "ada@example.com",
                positionId: "pos-1", jobTitle: "Engineer", department: "Engineering",
                startDate: "2026-09-01T00:00:00Z", baseSalary: "120000", currency: "USD"));

        var events = _parser.Parse(xml);

        Assert.Single(events);
        Assert.Equal(WorkforceEventType.Hire, events[0].Type);
    }

    [Fact]
    public void Parse_AllSixEventTypes_ParsesEach()
    {
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry("e1", "Hire", "2026-09-01T09:00:00Z", "emp-1",
                firstName: "A", lastName: "B", email: "a@b.com", positionId: "p1",
                jobTitle: "T", department: "D", startDate: "2026-09-01T00:00:00Z",
                baseSalary: "100", currency: "USD"),
            AtomFixtures.Entry("e2", "Termination", "2026-09-02T09:00:00Z", "emp-1"),
            AtomFixtures.Entry("e3", "Transfer", "2026-09-03T09:00:00Z", "emp-1",
                positionId: "p2", jobTitle: "T2"),
            AtomFixtures.Entry("e4", "PayChange", "2026-09-04T09:00:00Z", "emp-1",
                baseSalary: "150", currency: "USD"),
            AtomFixtures.Entry("e5", "Promotion", "2026-09-05T09:00:00Z", "emp-1",
                positionId: "p3", jobTitle: "T3", baseSalary: "160", currency: "USD"),
            AtomFixtures.Entry("e6", "Rehire", "2026-09-06T09:00:00Z", "emp-1",
                firstName: "A", lastName: "B", email: "a@b.com", positionId: "p1",
                jobTitle: "T", department: "D", startDate: "2026-09-06T00:00:00Z",
                baseSalary: "110", currency: "USD"));

        var events = _parser.Parse(xml);

        Assert.Equal(6, events.Count);
        Assert.Equal(WorkforceEventType.Hire, events[0].Type);
        Assert.Equal(WorkforceEventType.Termination, events[1].Type);
        Assert.Equal(WorkforceEventType.Transfer, events[2].Type);
        Assert.Equal(WorkforceEventType.PayChange, events[3].Type);
        Assert.Equal(WorkforceEventType.Promotion, events[4].Type);
        Assert.Equal(WorkforceEventType.Rehire, events[5].Type);
    }

    [Fact]
    public void Parse_EscapedAmpersand_RoundTripsToLiteral()
    {
        // The generator XML-escapes values (e.g. "Data & Analytics" → "Data &amp; Analytics").
        // The parser must hand back the literal ampersand.
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry("e1", "Hire", "2026-09-01T09:00:00Z", "emp-1",
                firstName: "A", lastName: "B", email: "a@b.com", positionId: "p1",
                job: "Data &amp; Analytics", jobTitle: "T", department: "D",
                startDate: "2026-09-01T00:00:00Z", baseSalary: "100", currency: "USD"));

        var events = _parser.Parse(xml);

        Assert.Equal("Data & Analytics", events[0].Job);
    }

    [Fact]
    public void Parse_UnknownEventType_Throws()
    {
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry("e1", "UnknownEvent", "2026-09-01T09:00:00Z", "emp-1"));

        var ex = Assert.Throws<AtomFeedParseException>(() => _parser.Parse(xml));
        Assert.Contains("unknown event type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MissingEmployeeId_Throws()
    {
        var xml = AtomFixtures.Feed(
            AtomFixtures.Entry("e1", "Hire", "2026-09-01T09:00:00Z", employeeId: null,
                firstName: "A"));

        var ex = Assert.Throws<AtomFeedParseException>(() => _parser.Parse(xml));
        Assert.Contains("employeeId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MalformedXml_Throws()
    {
        Assert.Throws<AtomFeedParseException>(() => _parser.Parse("<feed><entry>"));
    }

    [Fact]
    public void Parse_NonFeedRoot_Throws()
    {
        Assert.Throws<AtomFeedParseException>(() => _parser.Parse("<notafeed/>"));
    }
}
