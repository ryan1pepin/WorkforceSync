using System.Text;
using WorkforceSync.Core.Models;

namespace WorkforceSync.Core.Tests;

/// <summary>
/// Builds ATOM feed XML fixtures for parser tests.
/// </summary>
internal static class AtomFixtures
{
    private const string AtomNs = "http://www.w3.org/2005/Atom";
    private const string HcmNs = "http://workforcesync.local/hcm";

    /// <summary>Builds a full ATOM feed from the given entries.</summary>
    public static string Feed(params string[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append($"<feed xmlns=\"{AtomNs}\">");
        sb.Append("<title>Workforce Events</title>");
        sb.Append("<updated>2026-09-01T00:00:00Z</updated>");
        foreach (var e in entries)
        {
            sb.Append(e);
        }

        sb.Append("</feed>");
        return sb.ToString();
    }

    /// <summary>Builds a single ATOM entry wrapping an <event> payload.</summary>
    public static string Entry(
        string id,
        string type,
        string updated,
        string? employeeId,
        string? personNumber = null,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? legalEmployer = null,
        string? positionId = null,
        string? job = null,
        string? grade = null,
        string? jobTitle = null,
        string? department = null,
        string? workLocation = null,
        string? supervisor = null,
        string? employmentType = null,
        string? payBasis = null,
        string? startDate = null,
        string? endDate = null,
        string? terminationReason = null,
        string? baseSalary = null,
        string? currency = null)
    {
        var fields = new List<string>();
        AddField(fields, "employeeId", employeeId);
        AddField(fields, "personNumber", personNumber);
        AddField(fields, "firstName", firstName);
        AddField(fields, "lastName", lastName);
        AddField(fields, "email", email);
        AddField(fields, "legalEmployer", legalEmployer);
        AddField(fields, "positionId", positionId);
        AddField(fields, "job", job);
        AddField(fields, "grade", grade);
        AddField(fields, "jobTitle", jobTitle);
        AddField(fields, "department", department);
        AddField(fields, "workLocation", workLocation);
        AddField(fields, "supervisor", supervisor);
        AddField(fields, "employmentType", employmentType);
        AddField(fields, "payBasis", payBasis);
        AddField(fields, "startDate", startDate);
        AddField(fields, "endDate", endDate);
        AddField(fields, "terminationReason", terminationReason);
        AddField(fields, "baseSalary", baseSalary);
        AddField(fields, "currency", currency);

        var sb = new StringBuilder();
        sb.Append("<entry>");
        sb.Append($"<id>{id}</id>");
        sb.Append($"<title>{type}</title>");
        sb.Append($"<updated>{updated}</updated>");
        sb.Append("<content type=\"application/xml\">");
        sb.Append($"<event xmlns=\"{HcmNs}\">");
        sb.Append(string.Concat(fields));
        sb.Append("</event>");
        sb.Append("</content>");
        sb.Append("</entry>");
        return sb.ToString();
    }

    private static void AddField(List<string> fields, string name, string? value)
    {
        if (value is not null)
        {
            fields.Add($"<{name}>{value}</{name}>");
        }
    }

    /// <summary>A valid Hire event.</summary>
    public static WorkforceEvent ValidHire => new(
        EventId: "evt-1",
        Type: WorkforceEventType.Hire,
        OccurredAt: new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
        EmployeeId: "emp-100",
        PersonNumber: "P-1001",
        FirstName: "Ada",
        LastName: "Lovelace",
        Email: "ada@example.com",
        LegalEmployer: "Acme Corp",
        PositionId: "pos-1",
        Job: "Software Engineer",
        Grade: "G5",
        JobTitle: "Engineer",
        Department: "Engineering",
        WorkLocation: "Atlanta, GA",
        Supervisor: "Grace Hopper",
        EmploymentType: "Regular",
        PayBasis: "Annual",
        StartDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate: null,
        TerminationReason: null,
        BaseSalary: 120000m,
        Currency: "USD");

    /// <summary>A valid Termination event.</summary>
    public static WorkforceEvent ValidTermination => new(
        EventId: "evt-2",
        Type: WorkforceEventType.Termination,
        OccurredAt: new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc),
        EmployeeId: "emp-100",
        PersonNumber: null,
        FirstName: null,
        LastName: null,
        Email: null,
        LegalEmployer: null,
        PositionId: null,
        Job: null,
        Grade: null,
        JobTitle: null,
        Department: null,
        WorkLocation: null,
        Supervisor: null,
        EmploymentType: null,
        PayBasis: null,
        StartDate: null,
        EndDate: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
        TerminationReason: "Layoff",
        BaseSalary: null,
        Currency: null);

    /// <summary>A valid Transfer event (position change).</summary>
    public static WorkforceEvent ValidPositionChange => new(
        EventId: "evt-3",
        Type: WorkforceEventType.Transfer,
        OccurredAt: new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc),
        EmployeeId: "emp-100",
        PersonNumber: null,
        FirstName: null,
        LastName: null,
        Email: null,
        LegalEmployer: null,
        PositionId: "pos-2",
        Job: "Senior Software Engineer",
        Grade: "G6",
        JobTitle: "Senior Engineer",
        Department: "Platform",
        WorkLocation: null,
        Supervisor: null,
        EmploymentType: null,
        PayBasis: null,
        StartDate: null,
        EndDate: null,
        TerminationReason: null,
        BaseSalary: null,
        Currency: null);

    /// <summary>A valid PayChange event (compensation change).</summary>
    public static WorkforceEvent ValidCompensationChange => new(
        EventId: "evt-4",
        Type: WorkforceEventType.PayChange,
        OccurredAt: new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc),
        EmployeeId: "emp-100",
        PersonNumber: null,
        FirstName: null,
        LastName: null,
        Email: null,
        LegalEmployer: null,
        PositionId: null,
        Job: null,
        Grade: null,
        JobTitle: null,
        Department: null,
        WorkLocation: null,
        Supervisor: null,
        EmploymentType: null,
        PayBasis: null,
        StartDate: null,
        EndDate: null,
        TerminationReason: null,
        BaseSalary: 135000m,
        Currency: "USD");
}
