namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Dictionary-backed implementation of <see cref="IFeedCache"/> for FeedService tests.
/// Mirrors the persistence semantics of <see cref="JsonFeedCache"/>: one document per
/// <see cref="FeedKind"/>, a write replaces the previous document, and neither operation
/// ever throws. <see cref="Store"/> is public so a test can pre-seed a stale entry.
/// </summary>
internal sealed class InMemoryFeedCache : IFeedCache
{
    public Dictionary<FeedKind, CachedFeed> Store { get; } = new();

    public Task<CachedFeed?> ReadAsync(FeedKind kind, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.TryGetValue(kind, out var cached) ? cached : null);

    public Task WriteAsync(CachedFeed feed, CancellationToken cancellationToken = default)
    {
        Store[feed.Kind] = feed;
        return Task.CompletedTask;
    }
}
