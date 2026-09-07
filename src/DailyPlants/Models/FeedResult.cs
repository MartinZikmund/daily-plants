namespace DailyPlants.Models;

/// <summary>
/// How a <see cref="FeedResult"/> was obtained. Drives the notice/empty state on the Resources page.
/// </summary>
public enum FeedResultStatus
{
    /// <summary>Fetched from the network during this call.</summary>
    Fresh = 0,

    /// <summary>Served from cache that is still inside the freshness window; no network call was made.</summary>
    Cached = 1,

    /// <summary>Cache was past the freshness window and the refresh failed; stale items are being served.</summary>
    Stale = 2,

    /// <summary>Nothing cached and the network call failed. <see cref="FeedResult.Items"/> is empty.</summary>
    Unavailable = 3,

    /// <summary>This head cannot fetch live (browserwasm/CORS) and nothing is cached. Items is empty.</summary>
    LiveFetchUnavailable = 4
}

/// <summary>
/// The outcome of a feed request.
/// </summary>
public sealed record FeedResult(
    FeedKind Kind,
    IReadOnlyList<FeedItem> Items,
    FeedResultStatus Status,
    DateTimeOffset? FetchedAt)
{
    /// <summary>
    /// True when there is something to render, whatever the status.
    /// </summary>
    public bool HasItems => Items.Count > 0;
}
