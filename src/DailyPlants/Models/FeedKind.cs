namespace DailyPlants.Models;

/// <summary>
/// The three nutritionfacts.org feeds surfaced by the Resources page, plus everything else the
/// site-wide search can turn up.
/// </summary>
/// <remarks>
/// Serialized into the cache JSON as a number - do not reorder or renumber.
/// </remarks>
public enum FeedKind
{
    Blog = 0,
    Videos = 1,
    Podcast = 2,

    /// <summary>A search hit that belongs to none of the three feeds - /questions/ pages today.</summary>
    Other = 3
}
