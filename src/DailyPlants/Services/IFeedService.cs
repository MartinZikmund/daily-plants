using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Fetches nutritionfacts.org feeds with a one-hour freshness window, an in-memory memo,
/// and a disk cache that doubles as the offline fallback. Never throws for network failure.
/// </summary>
public interface IFeedService
{
    /// <summary>The freshness window: cache newer than this is served without a network call.</summary>
    static TimeSpan FreshnessWindow => TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the newest items for one feed. Order: in-memory memo (if fresh), disk cache (if fresh),
    /// then network. On network failure, falls back to whatever is cached and reports
    /// <see cref="FeedResultStatus.Stale"/>, or <see cref="FeedResultStatus.Unavailable"/> with an empty list.
    /// Concurrent calls for the same <paramref name="kind"/> are coalesced.
    /// </summary>
    /// <param name="forceRefresh">Bypasses the freshness check and goes to the network (still falls back on failure).</param>
    Task<FeedResult> GetFeedAsync(FeedKind kind, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest <paramref name="count"/> items across all three feeds, deduped by
    /// <see cref="FeedItem.Id"/> and ordered by <see cref="FeedItem.PublishedAt"/> descending
    /// (items with no date sort last). Feeds that fail contribute nothing; the call still succeeds.
    /// </summary>
    Task<IReadOnlyList<FeedItem>> GetLatestAcrossFeedsAsync(int count, CancellationToken cancellationToken = default);
}
