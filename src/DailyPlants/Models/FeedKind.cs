namespace DailyPlants.Models;

/// <summary>
/// The six nutritionfacts.org feeds surfaced by the Resources page, plus everything else the
/// site-wide search can turn up.
/// </summary>
/// <remarks>
/// Serialized into the cache JSON as a number - do not renumber. <see cref="Other"/> is 0 so a kind
/// nobody set reads as "none of our feeds" rather than as the blog; the rest run in the order the
/// Resources page shows them, which <see cref="FeedKinds.Feeds"/> spells out.
/// </remarks>
public enum FeedKind
{
    /// <summary>A search hit that belongs to none of the feeds - a /topics/ page, say.</summary>
    Other = 0,

    Blog = 1,
    Videos = 2,
    Podcast = 3,
    Recipes = 4,
    Questions = 5,
    Webinars = 6
}

/// <summary>
/// Helpers over <see cref="FeedKind"/>.
/// </summary>
public static class FeedKinds
{
    /// <summary>
    /// The kinds that have a feed of their own, in the order the Resources page shows them.
    /// <see cref="FeedKind.Other"/> is deliberately absent: it has no URL to fetch.
    /// </summary>
    /// <remarks>
    /// Written out rather than derived from <c>Enum.GetValues</c> so a member added later joins a
    /// tab strip - or a background fetch - only when someone means it to.
    /// </remarks>
    public static IReadOnlyList<FeedKind> Feeds { get; } =
    [
        FeedKind.Blog,
        FeedKind.Videos,
        FeedKind.Podcast,
        FeedKind.Recipes,
        FeedKind.Questions,
        FeedKind.Webinars
    ];
}
