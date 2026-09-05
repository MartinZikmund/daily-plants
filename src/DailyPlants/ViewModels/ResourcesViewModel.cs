using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Resources page - three independently loaded feed tabs, plus a search that
/// replaces them with results from the whole nutritionfacts.org archive.
/// </summary>
public partial class ResourcesViewModel : ObservableObject
{
    public ResourcesViewModel(IFeedService feedService)
    {
        Blog = new FeedListViewModel(feedService, FeedKind.Blog, Localized("Resources_TabBlog", "Blog"));
        Videos = new FeedListViewModel(feedService, FeedKind.Videos, Localized("Resources_TabVideos", "Videos"));
        Podcast = new FeedListViewModel(feedService, FeedKind.Podcast, Localized("Resources_TabPodcast", "Podcast"));
        SearchResults = FeedListViewModel.CreateSearch(feedService, Localized("Resources_SearchPlaceholder", "Search NutritionFacts.org"));
        _selectedTab = Blog;
    }

    public FeedListViewModel Blog { get; }

    public FeedListViewModel Videos { get; }

    public FeedListViewModel Podcast { get; }

    /// <summary>The archive search results. Never cached, and empty until a query is submitted.</summary>
    public FeedListViewModel SearchResults { get; }

    public IReadOnlyList<FeedListViewModel> Tabs => [Blog, Videos, Podcast];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveList))]
    [NotifyPropertyChangedFor(nameof(IsBlogSelected))]
    [NotifyPropertyChangedFor(nameof(IsVideosSelected))]
    [NotifyPropertyChangedFor(nameof(IsPodcastSelected))]
    private FeedListViewModel _selectedTab;

    /// <summary>What is in the search box; bound two-way so the clear button can empty it.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>True while search results stand in for the tabs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveList))]
    private bool _isSearchActive;

    /// <summary>The list the page renders: search results when searching, the selected tab otherwise.</summary>
    public FeedListViewModel ActiveList => IsSearchActive ? SearchResults : SelectedTab;

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

        // Picking a tab is how you leave the results; the query text stays so it can be re-run.
        IsSearchActive = false;

        // Tabs load on first view, so opening the page costs one fetch instead of three.
        if (!tab.HasLoaded)
        {
            await tab.LoadAsync();
        }
    }

    /// <summary>Force-refreshes whichever list is on screen.</summary>
    [RelayCommand]
    private async Task RefreshAsync() => await ActiveList.LoadAsync(forceRefresh: true);

    /// <summary>Appends the next page of whichever list is on screen. No-ops at the end of the list.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync() => await ActiveList.LoadMoreAsync();

    /// <summary>
    /// Runs a search from page 1. Passing null uses whatever is in <see cref="SearchQuery"/>, so the
    /// command works both from the box's submit and from a bound button. A blank query clears
    /// instead of searching - an empty search is a request to see the tabs again.
    /// </summary>
    [RelayCommand]
    private async Task SubmitSearchAsync(string? query)
    {
        var text = (query ?? SearchQuery).Trim();

        if (text.Length == 0)
        {
            ClearSearch();
            return;
        }

        SearchQuery = text;
        IsSearchActive = true;

        await SearchResults.SearchAsync(text);
    }

    /// <summary>Drops the query and the results and puts the tabs back.</summary>
    [RelayCommand]
    private void ClearSearch()
    {
        IsSearchActive = false;
        SearchQuery = string.Empty;
        SearchResults.Clear();
    }

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

    private FeedListViewModel GetTab(FeedKind kind) => kind switch
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
