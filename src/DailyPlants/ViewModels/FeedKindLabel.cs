using DailyPlants.Helpers;
using DailyPlants.Models;

namespace DailyPlants.ViewModels;

/// <summary>
/// The localized name of a feed. One table, so a tab, a section heading and a card's kicker can
/// never disagree about what to call the same feed.
/// </summary>
internal static class FeedKindLabel
{
    public static string For(FeedKind kind) => kind switch
    {
        FeedKind.Blog => Localized("Resources_TabBlog", "Blog"),
        FeedKind.Videos => Localized("Resources_TabVideos", "Videos"),
        FeedKind.Podcast => Localized("Resources_TabPodcast", "Podcast"),
        FeedKind.Recipes => Localized("Resources_TabRecipes", "Recipes"),
        FeedKind.Questions => Localized("Resources_TabQuestions", "Q&A"),
        FeedKind.Webinars => Localized("Resources_TabWebinars", "Webinars"),
        // A search hit from outside the feeds - "Article" rather than a feed name it isn't in.
        _ => Localized("Resources_KindOther", "Article")
    };

    /// <summary>
    /// Resource lookup with an English fallback, for keys that are not yet in every
    /// <c>Strings/*/Resources.resw</c>. <see cref="Localizer"/> returns "[Key]" on a miss.
    /// </summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }
}
