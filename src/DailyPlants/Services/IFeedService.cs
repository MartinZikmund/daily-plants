using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Fetches nutritionfacts.org feeds with a one-hour freshness window, an in-memory memo,
/// and a disk cache that doubles as the offline fallback. Never throws for network failure.
/// </summary>
public interface IFeedService
{
    /// <summary>
    /// False when this head cannot reach the feeds at all (browserwasm, where the site sends no
    /// CORS header). Callers use it to avoid offering a next page that can only fail.
    /// </summary>
    bool SupportsLiveFetch { get; }

    /// <summary>The freshness window: cache newer than this is served without a network call.</summary>
    static TimeSpan FreshnessWindow => TimeSpan.FromHours(1);

    /// <summary>
    /// Runaway guard. Paging stops here however many pages the site would go on serving; the blog
    /// runs about 50 pages deep, so this is past the end rather than a limit anyone will feel.
    /// </summary>
    static int MaxPage => 50;

    /// <summary>
    /// Returns the newest items for one feed. Order: in-memory memo (if fresh), disk cache (if fresh),
    /// then network. On network failure, falls back to whatever is cached and reports
    /// <see cref="FeedResultStatus.Stale"/>, or <see cref="FeedResultStatus.Unavailable"/> with an empty list.
    /// Concurrent calls for the same <paramref name="kind"/> are coalesced.
    /// </summary>
    /// <param name="forceRefresh">Bypasses the freshness check and goes to the network (still falls back on failure).</param>
    Task<FeedResult> GetFeedAsync(FeedKind kind, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest <paramref name="count"/> items across Blog, Videos and Podcast, deduped by
    /// <see cref="FeedItem.Id"/> and ordered by <see cref="FeedItem.PublishedAt"/> descending
    /// (items with no date sort last). Feeds that fail contribute nothing; the call still succeeds.
    /// </summary>
    /// <remarks>
    /// Deliberately three feeds, not <see cref="FeedKinds.Feeds"/>: this is the Diary teaser, and
    /// six fetches on the app's first page is not worth two more cards.
    /// </remarks>
    Task<IReadOnlyList<FeedItem>> GetLatestAcrossFeedsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest <paramref name="perFeed"/> items from each of the feeds in
    /// <see cref="FeedKinds.Feeds"/>, one group per feed, in that display order. A feed that fails
    /// contributes an empty group; the call still succeeds. Goes through the same memo, cache and
    /// freshness window as <see cref="GetFeedAsync"/>, so a second visit costs no network.
    /// </summary>
    Task<IReadOnlyList<FeedGroup>> GetOverviewAsync(int perFeed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Page <paramref name="page"/> (1-based) of one feed. Page 1 goes through the memo, cache and
    /// freshness window exactly like <see cref="GetFeedAsync"/>. Pages 2 and up are network-only and
    /// are never written to the cache, so the offline story stays "you get the first page".
    /// Never throws: a failure arrives as an end-of-list page.
    /// </summary>
    Task<FeedPage> GetFeedPageAsync(FeedKind kind, int page, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of results from the whole nutritionfacts.org archive. Network-only, never cached,
    /// and the items may be of any <see cref="FeedKind"/> - search crosses every feed and turns
    /// up pages that belong to none of them. A blank query returns an empty page without a request.
    /// Never throws.
    /// </summary>
    Task<FeedPage> SearchAsync(string query, int page, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a single topic's feed, e.g. <c>topics/berries/feed/</c>. Network-only, never
    /// cached, and the items may be of any <see cref="FeedKind"/> - a topic collects videos, posts
    /// and podcasts alike. A blank slug returns an empty page without a request. Never throws.
    /// </summary>
    Task<FeedPage> GetTopicPageAsync(string slug, int page, CancellationToken cancellationToken = default);
}
