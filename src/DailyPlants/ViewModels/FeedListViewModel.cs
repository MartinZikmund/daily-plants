using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// A paginated list of feed items with its loading state - one feed tab on the Resources page, the
/// results of a site-wide search, or one nutritionfacts.org topic. The two latter modes have no
/// <see cref="Kind"/> of their own and fetch by <see cref="Query"/> instead.
/// </summary>
public partial class FeedListViewModel : ObservableObject, IResourceTab
{
    /// <summary>What this list fetches, and therefore which service call pages it.</summary>
    private enum ListMode
    {
        Feed,
        Search,
        Topic
    }

    private readonly IFeedService _feedService;

    private readonly ListMode _mode;

    /// <summary>Ids already on screen; the dedupe that decides where a paginated list ends.</summary>
    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);

    private string _query = string.Empty;

    /// <summary>The last page that actually contributed items. Only that page advances it.</summary>
    private int _loadedPage;

    public FeedListViewModel(IFeedService feedService, FeedKind kind, string title)
        : this(feedService, kind, title, ListMode.Feed)
    {
    }

    private FeedListViewModel(IFeedService feedService, FeedKind? kind, string title, ListMode mode)
    {
        _feedService = feedService;
        Kind = kind;
        Title = title;
        _mode = mode;
    }

    /// <summary>The list that holds search results: no feed of its own, one query at a time.</summary>
    public static FeedListViewModel CreateSearch(IFeedService feedService, string title)
        => new(feedService, null, title, ListMode.Search);

    /// <summary>
    /// The list that holds one topic's items: no feed of its own, one slug at a time, and a
    /// <see cref="Title"/> that changes with the topic rather than naming a tab.
    /// </summary>
    public static FeedListViewModel CreateTopic(IFeedService feedService, string title)
        => new(feedService, null, title, ListMode.Topic);

    /// <summary>The feed this list pages through, or null in search and topic mode.</summary>
    public FeedKind? Kind { get; }

    /// <summary>
    /// Localized tab label (Resources_Tab*), the search heading, or - in topic mode - the name of
    /// the topic currently loaded, which is why it notifies.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>
    /// AutomationProperties.AutomationId for this tab's button. The search and topic lists are not
    /// tabs and never appear in the strip, so they get names of their own rather than a tab id.
    /// </summary>
    public string TabAutomationId => Kind is { } kind
        ? $"ResourcesTab{kind}Button"
        : IsTopic ? "ResourcesTopicResults" : "ResourcesSearchResults";

    /// <summary>True while this is the selected tab. Maintained by <see cref="ResourcesViewModel"/>.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>True for the search list: it fetches by query rather than by feed, and is never cached.</summary>
    public bool IsSearch => _mode == ListMode.Search;

    /// <summary>True for the topic list: it fetches by slug rather than by feed, and is never cached.</summary>
    public bool IsTopic => _mode == ListMode.Topic;

    /// <summary>
    /// The query these results are for - the search text, or the topic slug in topic mode. Empty in
    /// tab mode and before the first search.
    /// </summary>
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
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowItems))]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    public partial bool HasLoaded { get; set; }

    /// <summary>Resources_Offline / _BrowserUnavailable / _LoadError, or empty when all is well.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    public partial string NoticeText { get; set; } = string.Empty;

    /// <summary>Resources_Empty headline, or the status-specific replacement.</summary>
    [ObservableProperty]
    public partial string EmptyStateText { get; set; } = string.Empty;

    /// <summary>Resources_UpdatedAt formatted, or empty when never fetched.</summary>
    [ObservableProperty]
    public partial string UpdatedText { get; set; } = string.Empty;

    /// <summary>True while the next page is on its way; the first page uses IsLoading instead.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadingMore))]
    public partial bool IsLoadingMore { get; set; }

    /// <summary>True while asking for another page is still worth it. False is the end of the list.</summary>
    [ObservableProperty]
    public partial bool HasMore { get; set; }

    public bool ShowItems => Items.Count > 0;

    public bool ShowEmptyState => !IsLoading && HasLoaded && Items.Count == 0;

    public bool ShowNotice => !IsLoading && !string.IsNullOrEmpty(NoticeText);

    public bool ShowLoadingMore => IsLoadingMore;

    /// <summary>
    /// Loads (or reloads) the first page, resetting paging. In search and topic mode this re-runs
    /// whatever is already loaded, which is what makes Refresh work in those modes too. Sets
    /// IsLoading in a try/finally; never throws, except for cancellation, which is rethrown having
    /// left the list, the spinner and the notice exactly as it found them.
    /// </summary>
    public async Task LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (IsSearch)
        {
            await SearchAsync(Query, cancellationToken);
            return;
        }

        if (IsTopic)
        {
            await LoadTopicAsync(Query, Title, cancellationToken);
            return;
        }

        IsLoading = true;
        var cancelled = false;

        try
        {
            var result = await _feedService.GetFeedAsync(Kind!.Value, forceRefresh, cancellationToken);

            // The response may be a late one for a load that has already been superseded.
            cancellationToken.ThrowIfCancellationRequested();

            Reset(result.Items);
            _loadedPage = 1;

            // An offline first page must not offer a "load more" that can only fail.
            HasMore = _feedService.SupportsLiveFetch
                && result.Items.Count > 0
                && result.Status is FeedResultStatus.Fresh or FeedResultStatus.Cached;

            ApplyStatus(result);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Loading the {Kind} feed failed", ex);
            NoticeText = Localizer.GetString("Resources_LoadError");
            HasMore = false;
        }
        finally
        {
            if (!cancelled)
            {
                IsLoading = false;
                HasLoaded = true;
                OnPropertyChanged(nameof(ShowItems));
            }
        }
    }

    /// <summary>
    /// Search mode only: runs <paramref name="query"/> from page 1. A blank query clears the list
    /// without a request. Never throws, except for cancellation: a superseded search rethrows
    /// <see cref="OperationCanceledException"/> having left the results, the spinner and the notice
    /// exactly as it found them, so the search that replaced it owns the screen.
    /// </summary>
    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
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
        var cancelled = false;

        try
        {
            var page = await _feedService.SearchAsync(trimmed, 1, cancellationToken);

            // The response may be a late one for a query the user has already typed past.
            cancellationToken.ThrowIfCancellationRequested();

            Reset(page.Items);
            _loadedPage = 1;
            HasMore = _feedService.SupportsLiveFetch && page.MayHaveMore && Items.Count > 0;

            ApplyPageStatus(page);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Searching for \"{trimmed}\" failed", ex);
            EmptyStateText = Localizer.GetString("Resources_SearchOffline");
            HasMore = false;
        }
        finally
        {
            if (!cancelled)
            {
                IsLoading = false;
                HasLoaded = true;
                OnPropertyChanged(nameof(ShowItems));
            }
        }
    }

    /// <summary>
    /// Topic mode only: loads page 1 of <paramref name="slug"/> and retitles the list to
    /// <paramref name="title"/>. A blank slug clears the list without a request. Never throws,
    /// except for cancellation, which is rethrown having left the list exactly as it found it.
    /// </summary>
    public async Task LoadTopicAsync(string slug, string title, CancellationToken cancellationToken = default)
    {
        if (!IsTopic)
        {
            return;
        }

        var trimmed = slug?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            Clear();
            return;
        }

        Title = title;
        Query = trimmed;
        IsLoading = true;
        var cancelled = false;

        try
        {
            var page = await _feedService.GetTopicPageAsync(trimmed, 1, cancellationToken);

            // The response may be a late one for a topic the user has already navigated past.
            cancellationToken.ThrowIfCancellationRequested();

            Reset(page.Items);
            _loadedPage = 1;
            HasMore = _feedService.SupportsLiveFetch && page.MayHaveMore && Items.Count > 0;

            ApplyPageStatus(page);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Loading the \"{trimmed}\" topic failed", ex);
            EmptyStateText = Localizer.GetString("Resources_LoadError");
            HasMore = false;
        }
        finally
        {
            if (!cancelled)
            {
                IsLoading = false;
                HasLoaded = true;
                OnPropertyChanged(nameof(ShowItems));
            }
        }
    }

    /// <summary>
    /// Appends the next page. No-ops when a load is already running, when the list has ended, or in
    /// search and topic mode before there is anything to page. Never throws.
    /// </summary>
    public async Task LoadMoreAsync()
    {
        // Set synchronously, before the first await, so a second caller cannot slip past the guard.
        if (IsLoading || IsLoadingMore || !HasMore)
        {
            return;
        }

        if (_mode != ListMode.Feed && string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        IsLoadingMore = true;
        var page = _loadedPage + 1;

        try
        {
            var result = _mode switch
            {
                ListMode.Search => await _feedService.SearchAsync(Query, page),
                ListMode.Topic => await _feedService.GetTopicPageAsync(Query, page),
                _ => await _feedService.GetFeedPageAsync(Kind!.Value, page)
            };

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
            AppLog.Error($"Loading page {page} of the {SourceDescription} failed", ex);
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
        IsLoading = false;
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
                Localizer.GetString("Resources_UpdatedAt"),
                fetchedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture))
            : string.Empty;

        switch (result.Status)
        {
            case FeedResultStatus.Stale:
                NoticeText = Localizer.GetString("Resources_Offline");
                EmptyStateText = Localizer.GetString("Resources_Empty");
                break;

            case FeedResultStatus.Unavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localizer.GetString("Resources_LoadError");
                break;

            case FeedResultStatus.LiveFetchUnavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localizer.GetString("Resources_BrowserUnavailable");
                break;

            default:
                NoticeText = string.Empty;
                EmptyStateText = Localizer.GetString("Resources_Empty");
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
                    Localizer.GetString("Resources_SearchEmpty"),
                    Query)
                : Localizer.GetString("Resources_Empty");
            return;
        }

        string unreachable;

        if (page.Status is FeedResultStatus.LiveFetchUnavailable)
        {
            // The browser head cannot fetch at all, which is not the same thing as a request that
            // failed, so it says so even in search mode where the user does have a connection.
            unreachable = Localizer.GetString("Resources_BrowserUnavailable");
        }
        else if (IsSearch)
        {
            unreachable = Localizer.GetString("Resources_SearchOffline");
        }
        else
        {
            unreachable = Localizer.GetString("Resources_LoadError");
        }

        NoticeText = Items.Count > 0 ? unreachable : string.Empty;
        EmptyStateText = unreachable;
    }

    /// <summary>What this list is, for a log line: the feed name, "search results" or the slug.</summary>
    private string SourceDescription => _mode switch
    {
        ListMode.Search => "search results",
        ListMode.Topic => $"\"{Query}\" topic",
        _ => Kind.ToString() ?? string.Empty
    };
}
