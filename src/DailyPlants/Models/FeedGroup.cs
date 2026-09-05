namespace DailyPlants.Models;

/// <summary>
/// One section of the Resources overview: the newest few items of a single feed.
/// </summary>
/// <param name="Kind">The feed this section came from.</param>
/// <param name="Items">Its newest items, feed order. Empty when that one feed could not be read.</param>
public sealed record FeedGroup(FeedKind Kind, IReadOnlyList<FeedItem> Items)
{
    /// <summary>True when there is something to render under the heading.</summary>
    public bool HasItems => Items.Count > 0;
}
