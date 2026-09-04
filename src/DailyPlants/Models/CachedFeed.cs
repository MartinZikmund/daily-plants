namespace DailyPlants.Models;

/// <summary>
/// The JSON document written to %LocalAppData%/DailyPlants/FeedCache/{kind}.json.
/// </summary>
public sealed record CachedFeed
{
    /// <summary>
    /// Which feed this document caches.
    /// </summary>
    public FeedKind Kind { get; init; }

    /// <summary>When the network response this document came from was received (UTC).</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>
    /// The parsed items, in feed order.
    /// </summary>
    public IReadOnlyList<FeedItem> Items { get; init; } = [];
}
