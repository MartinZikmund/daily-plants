using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// A paginated list of feed items with its loading state - one feed tab on the Resources page, or
/// the results of a site-wide search when <see cref="Kind"/> is null.
/// </summary>
public partial class FeedListViewModel : ObservableObject
{
    private readonly IFeedService _feedService;

    /// <summary>Ids already on screen; the dedupe that decides where a paginated list ends.</summary>
    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);

    private string _query = string.Empty;

    /// <summary>The last page that actually contributed items. Only that page advances it.</summary>
    private int _loadedPage;

    public FeedListViewModel(IFeedService feedService, FeedKind kind, string title)
        : this(feedService, kind, title, isSearch: false)
    {
    }

    private FeedListViewModel(IFeedService feedService, FeedKind? kind, string title, bool isSearch)
    {
        _feedService = feedService;
        Kind = kind;
        Title = title;
        IsSearch = isSearch;
    }

    /// <summary>The list that holds search results: no feed of its own, one query at a time.</summary>
    public static FeedListViewModel CreateSearch(IFeedService feedService, string title)
        => new(feedService, null, title, isSearch: true);

    /// <summary>The feed this list pages through, or null in search mode.</summary>
    public FeedKind? Kind { get; }

    /// <summary>Localized tab label (Resources_TabBlog / _TabVideos / _TabPodcast), or the search heading.</summary>
    public string Title { get; }

    /// <summary>True for the search list: it fetches by query rather than by feed, and is never cached.</summary>
    public bool IsSearch { get; }

    /// <summary>The query these results are for. Empty in tab mode and before the first search.</summary>
    public string Query
    {
        get => _query;
        private set => SetProperty(ref _query, value);
    }

    public ObservableCollection<FeedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowItems))]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowItems))]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    private bool _hasLoaded;

    /// <summary>Resources_Offline / _BrowserUnavailable / _LoadError, or empty when all is well.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    private string _noticeText = string.Empty;

    /// <summary>Resources_Empty headline, or the status-specific replacement.</summary>
    [ObservableProperty]
    private string _emptyStateText = string.Empty;

    /// <summary>Resources_UpdatedAt formatted, or empty when never fetched.</summary>
    [ObservableProperty]
    private string _updatedText = string.Empty;

    /// <summary>True while the next page is on its way; the first page uses IsLoading instead.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadingMore))]
    private bool _isLoadingMore;

    /// <summary>True while asking for another page is still worth it. False is the end of the list.</summary>
    [ObservableProperty]
    private bool _hasMore;

    public bool ShowItems => Items.Count > 0;

    public bool ShowEmptyState => !IsLoading && HasLoaded && Items.Count == 0;

    public bool ShowNotice => !IsLoading && !string.IsNullOrEmpty(NoticeText);

    public bool ShowLoadingMore => IsLoadingMore;

    /// <summary>
    /// Loads (or reloads) the first page, resetting paging. In search mode this re-runs the current
    /// query. Sets IsLoading in a try/finally; never throws.
    /// </summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (IsSearch)
        {
            await SearchAsync(Query);
            return;
        }

        IsLoading = true;

        try
        {
            var result = await _feedService.GetFeedAsync(Kind!.Value, forceRefresh);

            Reset(result.Items);
            _loadedPage = 1;

            // An offline first page must not offer a "load more" that can only fail.
            HasMore = _feedService.SupportsLiveFetch
                && result.Items.Count > 0
                && result.Status is FeedResultStatus.Fresh or FeedResultStatus.Cached;

            ApplyStatus(result);
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Loading the {Kind} feed failed", ex);
            NoticeText = Localized("Resources_LoadError", "We couldn't load the newest posts.");
            HasMore = false;
        }
        finally
        {
            IsLoading = false;
            HasLoaded = true;
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    /// <summary>
    /// Search mode only: runs <paramref name="query"/> from page 1. A blank query clears the list
    /// without a request. Never throws.
    /// </summary>
    public async Task SearchAsync(string query)
    {
        if (!IsSearch)
        {
            return;
        }

        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            Clear();
            return;
        }

        Query = trimmed;
        IsLoading = true;

        try
        {
            var page = await _feedService.SearchAsync(trimmed, 1);

            Reset(page.Items);
            _loadedPage = 1;
            HasMore = _feedService.SupportsLiveFetch && page.MayHaveMore && Items.Count > 0;

            ApplyPageStatus(page);
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Searching for \"{trimmed}\" failed", ex);
            EmptyStateText = Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org.");
            HasMore = false;
        }
        finally
        {
            IsLoading = false;
            HasLoaded = true;
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    /// <summary>
    /// Appends the next page. No-ops when a load is already running, when the list has ended, or in
    /// search mode with no query. Never throws.
    /// </summary>
    public async Task LoadMoreAsync()
    {
        // Set synchronously, before the first await, so a second caller cannot slip past the guard.
        if (IsLoading || IsLoadingMore || !HasMore)
        {
            return;
        }

        if (IsSearch && string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        IsLoadingMore = true;
        var page = _loadedPage + 1;

        try
        {
            var result = IsSearch
                ? await _feedService.SearchAsync(Query, page)
                : await _feedService.GetFeedPageAsync(Kind!.Value, page);

            var added = Append(result.Items);

            // A page that contributed nothing new is the end of the list, whatever the server claims -
            // this is what stops a feed that wraps around from paging forever.
            HasMore = result.MayHaveMore && added > 0;

            if (added > 0)
            {
                _loadedPage = page;
            }

            ApplyPageStatus(result);
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Loading page {page} of the {(IsSearch ? "search results" : Kind.ToString())} failed", ex);
            HasMore = false;
        }
        finally
        {
            IsLoadingMore = false;
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    /// <summary>Empties the list and its paging state, back to how it looked before the first load.</summary>
    public void Clear()
    {
        Query = string.Empty;
        Reset([]);
        _loadedPage = 0;
        HasMore = false;
        HasLoaded = false;
        NoticeText = string.Empty;
        EmptyStateText = string.Empty;
        UpdatedText = string.Empty;
        OnPropertyChanged(nameof(ShowItems));
    }

    private void Reset(IReadOnlyList<FeedItem> items)
    {
        Items.Clear();
        _seenIds.Clear();
        Append(items);
    }

    /// <summary>Appends the items not already on screen and returns how many that was.</summary>
    private int Append(IReadOnlyList<FeedItem> items)
    {
        var added = 0;

        foreach (var item in items)
        {
            if (!_seenIds.Add(item.Id))
            {
                continue;
            }

            Items.Add(new FeedItemViewModel(item));
            added++;
        }

        return added;
    }

    private void ApplyStatus(FeedResult result)
    {
        UpdatedText = result.FetchedAt is { } fetchedAt
            ? string.Format(
                CultureInfo.CurrentCulture,
                Localized("Resources_UpdatedAt", "Updated {0}"),
                fetchedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture))
            : string.Empty;

        switch (result.Status)
        {
            case FeedResultStatus.Stale:
                NoticeText = Localized("Resources_Offline", "Showing saved posts — we couldn't reach NutritionFacts.org.");
                EmptyStateText = Localized("Resources_Empty", "Nothing here yet");
                break;

            case FeedResultStatus.Unavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Resources_LoadError", "We couldn't load the newest posts.");
                break;

            case FeedResultStatus.LiveFetchUnavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app.");
                break;

            default:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Resources_Empty", "Nothing here yet");
                break;
        }
    }

    /// <summary>
    /// Status for an uncached page. A page that could not be fetched speaks through the empty state
    /// when there is nothing on screen, and through the notice bar when it stopped a list mid-way.
    /// </summary>
    private void ApplyPageStatus(FeedPage page)
    {
        if (page.Status is FeedResultStatus.Fresh or FeedResultStatus.Cached)
        {
            NoticeText = string.Empty;
            EmptyStateText = IsSearch
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Localized("Resources_SearchEmpty", "Nothing found for “{0}”"),
                    Query)
                : Localized("Resources_Empty", "Nothing here yet");
            return;
        }

        string unreachable;

        if (page.Status is FeedResultStatus.LiveFetchUnavailable)
        {
            // The browser head cannot fetch at all, which is not the same thing as a request that
            // failed, so it says so even in search mode where the user does have a connection.
            unreachable = Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app.");
        }
        else if (IsSearch)
        {
            unreachable = Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org.");
        }
        else
        {
            unreachable = Localized("Resources_LoadError", "We couldn't load the newest posts.");
        }

        NoticeText = Items.Count > 0 ? unreachable : string.Empty;
        EmptyStateText = unreachable;
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
