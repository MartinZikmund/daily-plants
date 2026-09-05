namespace DailyPlants.Models;

/// <summary>
/// One page of feed or search results. Only page 1 of a feed goes through the cache, so a page
/// carries no fetch timestamp - <see cref="FeedResult"/> stays the shape for the cached first page.
/// </summary>
/// <param name="Items">The items on this page, in feed order. Empty when the fetch failed.</param>
/// <param name="Status">How this page was obtained.</param>
/// <param name="MayHaveMore">
/// False once the list has ended: the fetch failed, the page held no items, or the runaway cap was
/// reached. True only means asking for the next page is worth it - the caller still stops when a
/// page contributes nothing new after dedupe.
/// </param>
public sealed record FeedPage(
    IReadOnlyList<FeedItem> Items,
    FeedResultStatus Status,
    bool MayHaveMore)
{
    /// <summary>An end-of-list page: nothing to append, nothing more to ask for.</summary>
    public static FeedPage End(FeedResultStatus status) => new([], status, false);

    /// <summary>True when there is something to append.</summary>
    public bool HasItems => Items.Count > 0;
}
