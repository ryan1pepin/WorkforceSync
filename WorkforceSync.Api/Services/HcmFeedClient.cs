using WorkforceSync.Core.Atom;
using WorkforceSync.Core.Models;

namespace WorkforceSync.Api.Services;

/// <summary>
/// Polls the mock HCM ATOM feed over HTTP and parses it into
/// <see cref="WorkforceEvent"/> records. Thin wrapper so the ingestion
/// service doesn't care how the feed is fetched.
/// </summary>
public sealed class HcmFeedClient
{
    private readonly HttpClient _http;
    private readonly AtomFeedParser _parser = new();

    public HcmFeedClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Fetches the feed and returns its events.
    /// </summary>
    /// <exception cref="HttpRequestException">If the feed is unreachable.</exception>
    /// <exception cref="AtomFeedParseException">If the feed is malformed.</exception>
    public async Task<IReadOnlyList<WorkforceEvent>> FetchEventsAsync(CancellationToken ct)
    {
        var xml = await _http.GetStringAsync("/feed", ct);
        return _parser.Parse(xml);
    }
}
