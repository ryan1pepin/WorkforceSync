using System.Net;
using System.Text;

namespace WorkforceSync.HcmSource;

/// <summary>
/// A tiny HTTP server that serves the mock HCM ATOM feed at
/// <c>GET /feed</c>. Exists so the ingestion pipeline exercises a real
/// HTTP poll — the same shape it would use against a real HCM endpoint.
/// </summary>
public sealed class HcmFeedServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<IReadOnlyList<HcmEvent>> _eventProvider;
    private readonly Thread _thread;

    /// <summary>
    /// Creates a feed server on the given port.
    /// </summary>
    /// <param name="port">TCP port to listen on.</param>
    /// <param name="eventProvider">
    /// Returns the current set of events. Called on every request, so the
    /// feed can grow over time (new hires, terminations, ...).
    /// </param>
    public HcmFeedServer(int port, Func<IReadOnlyList<HcmEvent>> eventProvider)
    {
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _eventProvider = eventProvider;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "hcm-feed-server",
        };
    }

    /// <summary>The port the feed is served on.</summary>
    public int Port { get; }

    /// <summary>Starts listening in the background.</summary>
    public void Start()
    {
        _listener.Start();
        _thread.Start();
    }

    private void Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch
            {
                return; // listener stopped
            }

            try
            {
                Handle(ctx);
            }
            catch
            {
                // Never let one bad request kill the server thread.
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath.TrimEnd('/');
        if (path is not null && path != "/feed")
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        var xml = AtomFeedGenerator.GenerateFeed(_eventProvider());
        var bytes = Encoding.UTF8.GetBytes(xml);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/atom+xml; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch
        {
            // already stopped
        }
    }
}
