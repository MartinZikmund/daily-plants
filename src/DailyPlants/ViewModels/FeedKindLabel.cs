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
        FeedKind.Blog => Localizer.GetString("Resources_TabBlog"),
        FeedKind.Videos => Localizer.GetString("Resources_TabVideos"),
        FeedKind.Podcast => Localizer.GetString("Resources_TabPodcast"),
        FeedKind.Recipes => Localizer.GetString("Resources_TabRecipes"),
        FeedKind.Questions => Localizer.GetString("Resources_TabQuestions"),
        FeedKind.Webinars => Localizer.GetString("Resources_TabWebinars"),
        // A search hit from outside the feeds - "Article" rather than a feed name it isn't in.
        _ => Localizer.GetString("Resources_KindOther")
    };
}
