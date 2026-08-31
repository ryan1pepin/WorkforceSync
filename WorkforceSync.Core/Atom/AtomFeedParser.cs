using System.Globalization;
using System.Xml.Linq;
using WorkforceSync.Core.Models;

namespace WorkforceSync.Core.Atom;

/// <summary>
/// Parses an ATOM feed of workforce events (as emitted by the mock Oracle HCM Cloud)
/// into <see cref="WorkforceEvent"/> records.
///
/// Expected shape: an ATOM <c>&lt;feed&gt;</c> (namespace http://www.w3.org/2005/Atom)
/// whose <c>&lt;entry&gt;</c> elements carry <c>&lt;id&gt;</c>, <c>&lt;title&gt;</c>
/// (the event type name), <c>&lt;updated&gt;</c> (ISO-8601 UTC), and a
/// <c>&lt;content type="application/xml"&gt;</c> wrapping an <c>&lt;event&gt;</c>
/// element (namespace http://workforcesync.local/hcm) with the event fields.
/// </summary>
public sealed class AtomFeedParser
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Hcm = "http://workforcesync.local/hcm";

    /// <summary>
    /// Parses the given ATOM XML into a list of workforce events.
    /// </summary>
    /// <param name="atomXml">The ATOM feed as an XML string.</param>
    /// <exception cref="AtomFeedParseException">If the XML is malformed or an entry
    /// is missing required data.</exception>
    public IReadOnlyList<WorkforceEvent> Parse(string atomXml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(atomXml);
        }
        catch (Exception ex)
        {
            throw new AtomFeedParseException("ATOM feed is not well-formed XML.", ex);
        }

        var feed = doc.Root;
        if (feed is null || feed.Name != Atom + "feed")
        {
            throw new AtomFeedParseException("Root element is not an ATOM <feed>.");
        }

        var events = new List<WorkforceEvent>();
        foreach (var entry in feed.Elements(Atom + "entry"))
        {
            events.Add(ParseEntry(entry));
        }

        return events;
    }

    private static WorkforceEvent ParseEntry(XElement entry)
    {
        var id = entry.Element(Atom + "id")?.Value;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new AtomFeedParseException("ATOM entry is missing <id>.");
        }

        var title = entry.Element(Atom + "title")?.Value;
        var type = ParseEventType(title, id);

        var updated = entry.Element(Atom + "updated")?.Value;
        var occurredAt = ParseUtcDate(updated, id, "updated");

        var content = entry.Element(Atom + "content");
        var eventEl = content?.Element(Hcm + "event");
        if (eventEl is null)
        {
            throw new AtomFeedParseException(
                $"ATOM entry '{id}' is missing a <content> with an <event> element.");
        }

        var employeeId = ElementValue(eventEl, "employeeId");
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new AtomFeedParseException(
                $"ATOM entry '{id}' is missing required field 'employeeId'.");
        }

        return new WorkforceEvent(
            EventId: id,
            Type: type,
            OccurredAt: occurredAt,
            EmployeeId: employeeId,
            FirstName: ElementValue(eventEl, "firstName"),
            LastName: ElementValue(eventEl, "lastName"),
            Email: ElementValue(eventEl, "email"),
            PositionId: ElementValue(eventEl, "positionId"),
            JobTitle: ElementValue(eventEl, "jobTitle"),
            Department: ElementValue(eventEl, "department"),
            StartDate: ParseOptionalDate(ElementValue(eventEl, "startDate"), id, "startDate"),
            EndDate: ParseOptionalDate(ElementValue(eventEl, "endDate"), id, "endDate"),
            BaseSalary: ParseOptionalDecimal(ElementValue(eventEl, "baseSalary"), id, "baseSalary"),
            Currency: ElementValue(eventEl, "currency"));
    }

    private static WorkforceEventType ParseEventType(string? title, string entryId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AtomFeedParseException($"ATOM entry '{entryId}' is missing <title>.");
        }

        if (Enum.TryParse<WorkforceEventType>(title, ignoreCase: true, out var type))
        {
            return type;
        }

        throw new AtomFeedParseException(
            $"ATOM entry '{entryId}' has unknown event type '{title}'.");
    }

    private static DateTime ParseUtcDate(string? value, string entryId, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AtomFeedParseException(
                $"ATOM entry '{entryId}' is missing required date '{field}'.");
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            return dt.ToUniversalTime();
        }

        throw new AtomFeedParseException(
            $"ATOM entry '{entryId}' has an unparseable date '{value}' for '{field}'.");
    }

    private static DateTime? ParseOptionalDate(string? value, string entryId, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            return dt.ToUniversalTime();
        }

        throw new AtomFeedParseException(
            $"ATOM entry '{entryId}' has an unparseable date '{value}' for '{field}'.");
    }

    private static decimal? ParseOptionalDecimal(string? value, string entryId, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        throw new AtomFeedParseException(
            $"ATOM entry '{entryId}' has an unparseable decimal '{value}' for '{field}'.");
    }

    private static string? ElementValue(XElement parent, string localName)
    {
        var value = parent.Element(Hcm + localName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
