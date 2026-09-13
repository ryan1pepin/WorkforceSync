using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;

namespace WorkforceSync.HcmSource;

/// <summary>
/// Builds an ATOM feed of workforce events — the shape the mock Oracle HCM
/// Cloud publishes. The consumer (WorkforceSync.Api) polls this feed and
/// parses it with <c>WorkforceSync.Core.Atom.AtomFeedParser</c>.
/// </summary>
public static class AtomFeedGenerator
{
    private const string AtomNs = "http://www.w3.org/2005/Atom";
    private const string HcmNs = "http://workforcesync.local/hcm";

    /// <summary>Renders the given events as a full ATOM feed document.</summary>
    public static string GenerateFeed(IEnumerable<HcmEvent> events)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append($"<feed xmlns=\"{AtomNs}\">");
        sb.Append("<title>Workforce Events (mock Oracle HCM Cloud)</title>");
        sb.Append($"<updated>{DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}</updated>");

        foreach (var e in events)
        {
            sb.Append("<entry>");
            sb.Append($"<id>{e.Id}</id>");
            sb.Append($"<title>{e.Type}</title>");
            sb.Append($"<updated>{e.OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}</updated>");
            sb.Append("<content type=\"application/xml\">");
            sb.Append($"<event xmlns=\"{HcmNs}\">");
            AppendField(sb, "employeeId", e.EmployeeId);
            AppendField(sb, "personNumber", e.PersonNumber);
            AppendField(sb, "firstName", e.FirstName);
            AppendField(sb, "lastName", e.LastName);
            AppendField(sb, "email", e.Email);
            AppendField(sb, "legalEmployer", e.LegalEmployer);
            AppendField(sb, "positionId", e.PositionId);
            AppendField(sb, "job", e.Job);
            AppendField(sb, "grade", e.Grade);
            AppendField(sb, "jobTitle", e.JobTitle);
            AppendField(sb, "department", e.Department);
            AppendField(sb, "workLocation", e.WorkLocation);
            AppendField(sb, "supervisor", e.Supervisor);
            AppendField(sb, "employmentType", e.EmploymentType);
            AppendField(sb, "payBasis", e.PayBasis);
            AppendField(sb, "startDate", e.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AppendField(sb, "endDate", e.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AppendField(sb, "terminationReason", e.TerminationReason);
            AppendField(sb, "baseSalary", e.BaseSalary?.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, "currency", e.Currency);
            sb.Append("</event>");
            sb.Append("</content>");
            sb.Append("</entry>");
        }

        sb.Append("</feed>");
        return sb.ToString();
    }

    private static void AppendField(StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        // Escape XML special characters (&, <, >) so values like "Data & Analytics"
        // don't break the feed document.
        sb.Append($"<{name}>{SecurityElement.Escape(value)}</{name}>");
    }
}
