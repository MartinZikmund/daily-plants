using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Resources page - an overview tab plus one independently loaded tab per feed,
/// and a search that replaces them all with results from the whole nutritionfacts.org archive.
/// </summary>
public partial class ResourcesViewModel : ObservableObject
{
    /// <summary>How long typing must pause before an automatic search fires. Tests set this to zero.</summary>
    internal TimeSpan SearchDebounce { get; set; } = TimeSpan.FromMilliseconds(450);

    /// <summary>Shorter queries are not searched automatically; Enter still searches them.</summary>
    internal const int MinimumAutoQueryLength = 3;

    private readonly Dictionary<FeedKind, FeedListViewModel> _feedTabs;

    /// <summary>
    /// Cancels the pending debounce and any search already in flight. Deliberately not disposed:
    /// a superseded search may still be holding the token when the next keystroke replaces it.
    /// </summary>
    private CancellationTokenSource? _searchCts;

    /// <summary>True while the ViewModel itself is writing SearchQuery, so it does not re-trigger.</summary>
    private bool _isWritingQuery;

    public ResourcesViewModel(IFeedService feedService)
    {
        Latest = new LatestOverviewViewModel(feedService, Localized("Resources_TabLatest", "Latest"), SelectKindAsync);
        _feedTabs = FeedKinds.Feeds.ToDictionary(kind => kind, kind => new FeedListViewModel(feedService, kind, FeedKindLabel.For(kind)));
        SearchResults = FeedListViewModel.CreateSearch(feedService, Localized("Resources_SearchPlaceholder", "Search NutritionFacts.org"));

        // FeedKinds.Feeds is the display order; the overview goes in front of it.
        Tabs = [Latest, .. FeedKinds.Feeds.Select(kind => (IResourceTab)_feedTabs[kind])];

        _selectedTab = Latest;
        ApplySelection();
    }

    /// <summary>The overview tab: the newest couple of items from every feed.</summary>
    public LatestOverviewViewModel Latest { get; }

    public FeedListViewModel Blog => _feedTabs[FeedKind.Blog];

    public FeedListViewModel Videos => _feedTabs[FeedKind.Videos];

    public FeedListViewModel Podcast => _feedTabs[FeedKind.Podcast];

    public FeedListViewModel Recipes => _feedTabs[FeedKind.Recipes];

    public FeedListViewModel Questions => _feedTabs[FeedKind.Questions];

    public FeedListViewModel Webinars => _feedTabs[FeedKind.Webinars];

    /// <summary>The archive search results. Never cached, and empty until a query is submitted.</summary>
    public FeedListViewModel SearchResults { get; }

    /// <summary>Every tab in display order: Latest, then one per feed.</summary>
    public IReadOnlyList<IResourceTab> Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveList))]
    [NotifyPropertyChangedFor(nameof(IsOverviewActive))]
    private IResourceTab _selectedTab;

    /// <summary>What is in the search box; bound two-way so the clear button can empty it.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>True while search results stand in for the tabs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveList))]
    [NotifyPropertyChangedFor(nameof(IsOverviewActive))]
    private bool _isSearchActive;

    /// <summary>
    /// The paginated list the page renders: search results when searching, otherwise the selected
    /// feed tab. Null on the overview tab, which renders <see cref="Latest"/> instead.
    /// </summary>
    public FeedListViewModel? ActiveList => IsSearchActive ? SearchResults : SelectedTab as FeedListViewModel;

    /// <summary>True when the overview - not a list - is what the page should show.</summary>
    public bool IsOverviewActive => !IsSearchActive && ReferenceEquals(SelectedTab, Latest);

    /// <summary>
    /// The automatic search queued or in flight, or null when nothing is pending. Tests await it;
    /// nothing in the app does.
    /// </summary>
    internal Task? AutoSearchTask { get; private set; }

    /// <summary>
    /// How the debounce waits out <see cref="SearchDebounce"/>. Tests that fire a burst of
    /// keystrokes replace it with a gate they open themselves, so the coalescing is decided by the
    /// test rather than by the thread pool.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> DebounceDelay { get; set; } = WaitAsync;

    /// <summary>
    /// Selects a tab. Accepts an <see cref="IResourceTab"/> (the strip and the picker), a
    /// <see cref="FeedKind"/> (an overview section heading) or a kind name including "Latest"
    /// (the Diary deep link). Anything else is ignored.
    /// </summary>
    [RelayCommand]
    private async Task SelectTabAsync(object? target)
    {
        if (Resolve(target) is not { } tab)
        {
            return;
        }

        SelectedTab = tab;

        // Picking a tab is how you leave the results; the query text stays so it can be re-run.
        // A search still pending would otherwise land on top of the tab you just asked for.
        CancelPendingSearch();
        IsSearchActive = false;

        await EnsureLoadedAsync(tab);
    }

    /// <summary>Force-refreshes whichever tab is on screen.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (ActiveList is { } list)
        {
            await list.LoadAsync(forceRefresh: true);
            return;
        }

        await Latest.LoadAsync(forceRefresh: true);
    }

    /// <summary>
    /// Appends the next page of whichever list is on screen. No-ops at the end of the list, and on
    /// the overview, which does not page.
    /// </summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (ActiveList is { } list)
        {
            await list.LoadMoreAsync();
        }
    }

    /// <summary>
    /// Runs a search from page 1, immediately - this is the Enter path, so a query too short to
    /// search automatically is searched anyway. Passing null uses whatever is in
    /// <see cref="SearchQuery"/>. A blank query clears instead of searching: an empty search is a
    /// request to see the tabs again.
    /// </summary>
    [RelayCommand]
    private async Task SubmitSearchAsync(string? query)
    {
        // Enter beats the pending debounce, and the in-flight search it would have superseded.
        CancelPendingSearch();
        var text = (query ?? SearchQuery).Trim();

        if (text.Length == 0)
        {
            ClearSearch();
            return;
        }

        WriteQuery(text);

        try
        {
            await RunSearchAsync(text, NewSearchToken());
        }
        catch (OperationCanceledException)
        {
            // Superseded while it was in flight; whoever superseded it owns the screen now.
        }
    }

    /// <summary>Drops the query and the results and puts the tabs back.</summary>
    [RelayCommand]
    private void ClearSearch()
    {
        CancelPendingSearch();
        IsSearchActive = false;
        WriteQuery(string.Empty);
        SearchResults.Clear();
    }

    /// <summary>
    /// Called from the page's Loaded handler. Selects <paramref name="initialKind"/> when given
    /// (the Diary deep link), then loads the selected tab; other tabs load lazily on first selection.
    /// </summary>
    public async Task LoadAsync(FeedKind? initialKind = null)
    {
        if (initialKind is { } kind && GetTab(kind) is { } tab)
        {
            SelectedTab = tab;
        }

        await EnsureLoadedAsync(SelectedTab);
    }

    /// <summary>
    /// Typing. Cancels the pending debounce and any in-flight search, then queues a new one -
    /// unless the box is now empty (leave search mode) or too short to be worth a request.
    /// </summary>
    partial void OnSearchQueryChanged(string value)
    {
        if (_isWritingQuery)
        {
            return;
        }

        CancelPendingSearch();
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            ClearSearch();
            return;
        }

        if (trimmed.Length < MinimumAutoQueryLength)
        {
            // Too short to search on its own, and too early to throw away what is on screen.
            return;
        }

        AutoSearchTask = RunDebouncedSearchAsync(trimmed, NewSearchToken());
    }

    private async Task RunDebouncedSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await DebounceDelay(SearchDebounce, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await RunSearchAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer typing. Not a failure: no notice, and no touching the results.
        }
    }

    private async Task RunSearchAsync(string query, CancellationToken cancellationToken)
    {
        IsSearchActive = true;
        await SearchResults.SearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// The debounce's wait. Zero yields rather than resuming inline - Task.Delay(Zero) hands back an
    /// already-completed task, which would turn a zero debounce into a search on every letter.
    /// </summary>
    private static async Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (duration > TimeSpan.Zero)
        {
            await Task.Delay(duration, cancellationToken);
            return;
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Drops whatever search is pending or already in flight.</summary>
    private void CancelPendingSearch()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        AutoSearchTask = null;
    }

    /// <summary>The token the next search runs under. Cancel the last one first.</summary>
    private CancellationToken NewSearchToken()
    {
        CancellationTokenSource cts = new();
        _searchCts = cts;
        return cts.Token;
    }

    private void WriteQuery(string value)
    {
        _isWritingQuery = true;

        try
        {
            SearchQuery = value;
        }
        finally
        {
            _isWritingQuery = false;
        }
    }

    private Task SelectKindAsync(FeedKind kind) => SelectTabAsync(kind);

    private IResourceTab? Resolve(object? target) => target switch
    {
        IResourceTab tab when Tabs.Contains(tab) => tab,
        FeedKind kind => GetTab(kind),
        string name => FromName(name),
        _ => null
    };

    private IResourceTab? FromName(string name)
    {
        if (string.Equals(name, "Latest", StringComparison.OrdinalIgnoreCase))
        {
            return Latest;
        }

        return Enum.TryParse<FeedKind>(name, ignoreCase: true, out var kind) ? GetTab(kind) : null;
    }

    /// <summary>The tab for a feed, or null for a kind that has none (<see cref="FeedKind.Other"/>).</summary>
    private FeedListViewModel? GetTab(FeedKind kind) => _feedTabs.GetValueOrDefault(kind);

    /// <summary>Tabs load on first view, so opening the page costs one fetch instead of seven.</summary>
    private static async Task EnsureLoadedAsync(IResourceTab tab)
    {
        switch (tab)
        {
            case LatestOverviewViewModel overview when !overview.HasLoaded:
                await overview.LoadAsync();
                break;

            case FeedListViewModel list when !list.HasLoaded:
                await list.LoadAsync();
                break;
        }
    }

    partial void OnSelectedTabChanged(IResourceTab value) => ApplySelection();

    private void ApplySelection()
    {
        Latest.IsSelected = ReferenceEquals(SelectedTab, Latest);

        foreach (var tab in _feedTabs.Values)
        {
            tab.IsSelected = ReferenceEquals(SelectedTab, tab);
        }
    }

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
