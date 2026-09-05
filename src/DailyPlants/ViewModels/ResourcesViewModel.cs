using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Resources page - three independently loaded feed tabs.
/// </summary>
public partial class ResourcesViewModel : ObservableObject
{
    public ResourcesViewModel(IFeedService feedService)
    {
        Blog = new FeedTabViewModel(feedService, FeedKind.Blog, Localized("Resources_TabBlog", "Blog"));
        Videos = new FeedTabViewModel(feedService, FeedKind.Videos, Localized("Resources_TabVideos", "Videos"));
        Podcast = new FeedTabViewModel(feedService, FeedKind.Podcast, Localized("Resources_TabPodcast", "Podcast"));
        _selectedTab = Blog;
    }

    public FeedTabViewModel Blog { get; }

    public FeedTabViewModel Videos { get; }

    public FeedTabViewModel Podcast { get; }

    public IReadOnlyList<FeedTabViewModel> Tabs => [Blog, Videos, Podcast];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBlogSelected))]
    [NotifyPropertyChangedFor(nameof(IsVideosSelected))]
    [NotifyPropertyChangedFor(nameof(IsPodcastSelected))]
    private FeedTabViewModel _selectedTab;

    public bool IsBlogSelected => ReferenceEquals(SelectedTab, Blog);

    public bool IsVideosSelected => ReferenceEquals(SelectedTab, Videos);

    public bool IsPodcastSelected => ReferenceEquals(SelectedTab, Podcast);

    /// <summary>Selects a tab by <see cref="FeedKind"/> name ("Blog"/"Videos"/"Podcast"); unknown values are ignored.</summary>
    [RelayCommand]
    private async Task SelectTabAsync(string? kindName)
    {
        if (!Enum.TryParse<FeedKind>(kindName, ignoreCase: true, out var kind))
        {
            return;
        }

        var tab = GetTab(kind);
        SelectedTab = tab;

        // Tabs load on first view, so opening the page costs one fetch instead of three.
        if (!tab.HasLoaded)
        {
            await tab.LoadAsync();
        }
    }

    /// <summary>Force-refreshes the selected tab.</summary>
    [RelayCommand]
    private async Task RefreshAsync() => await SelectedTab.LoadAsync(forceRefresh: true);

    /// <summary>
    /// Called from the page's Loaded handler. Selects <paramref name="initialKind"/> when given
    /// (the Diary deep link), then loads the selected tab; other tabs load lazily on first selection.
    /// </summary>
    public async Task LoadAsync(FeedKind? initialKind = null)
    {
        if (initialKind is { } kind)
        {
            SelectedTab = GetTab(kind);
        }

        if (!SelectedTab.HasLoaded)
        {
            await SelectedTab.LoadAsync();
        }
    }

    private FeedTabViewModel GetTab(FeedKind kind) => kind switch
    {
        FeedKind.Videos => Videos,
        FeedKind.Podcast => Podcast,
        _ => Blog
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
