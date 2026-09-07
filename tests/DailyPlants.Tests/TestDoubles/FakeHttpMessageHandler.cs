using System.Net;
using System.Net.Http;

namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Returns a canned response per request URI. Mirrors the DelegatingHandler shape of
/// <see cref="DailyPlants.Services.Endpoints.DebugHttpHandler"/> without an inner handler,
/// so FeedService can be exercised with fixture XML and no network.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

    private int _requestCount;

    /// <summary>Number of requests actually sent - assert on this to prove the cache short-circuited.</summary>
    public int RequestCount => _requestCount;

    public FakeHttpMessageHandler RespondWith(Uri uri, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _failures.Remove(uri.AbsoluteUri);
        _responses[uri.AbsoluteUri] = () => new HttpResponseMessage(status) { Content = new StringContent(body) };
        return this;
    }

    public FakeHttpMessageHandler RespondWith(Uri uri, Exception exception)
    {
        _responses.Remove(uri.AbsoluteUri);
        _failures[uri.AbsoluteUri] = exception;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);

        // Yield so a second caller really does arrive while this one is still in flight.
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var key = request.RequestUri?.AbsoluteUri ?? string.Empty;
        if (_failures.TryGetValue(key, out var failure))
        {
            throw failure;
        }

        return _responses.TryGetValue(key, out var factory)
            ? factory()
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
