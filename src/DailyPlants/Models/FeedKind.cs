namespace DailyPlants.Models;

/// <summary>
/// The three nutritionfacts.org feeds surfaced by the Resources page.
/// </summary>
/// <remarks>
/// Serialized into the cache JSON as a number - do not reorder or renumber.
/// </remarks>
public enum FeedKind
{
    Blog = 0,
    Videos = 1,
    Podcast = 2
}
