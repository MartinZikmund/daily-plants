using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// The Resources page's first tab: the newest couple of items from every feed, grouped under
/// section headings in tab order. An overview, so there is no paging - the sections are the way
/// deeper, and tapping a heading opens that feed's own tab.
/// </summary>
public partial class LatestOverviewViewModel : ObservableObject, IResourceTab
{
    /// <summary>How many items each section shows. Two is a taste, not a list.</summary>
    public const int ItemsPerSection = 2;

    private readonly IFeedService _feedService;
    private readonly Func<FeedKind, Task> _selectSection;

    public LatestOverviewViewModel(IFeedService feedService, string title, Func<FeedKind, Task> selectSection)
    {
        _feedService = feedService;
        _selectSection = selectSection;
        Title = title;
    }

    public string Title { get; }

    public string TabAutomationId => "ResourcesTabLatestButton";

    /// <summary>Null: the overview belongs to no single feed.</summary>
    public FeedKind? Kind => null;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>One entry per feed that had something to show; a feed that failed is simply absent.</summary>
    public ObservableCollection<FeedGroupViewModel> Groups { get; } = [];

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

    /// <summary>Resources_LoadError, or empty when all is well.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    private string _noticeText = string.Empty;

    /// <summary>Resources_Empty headline, or the status-specific replacement.</summary>
    [ObservableProperty]
    private string _emptyStateText = string.Empty;

    public bool ShowItems => Groups.Count > 0;

    public bool ShowEmptyState => !IsLoading && HasLoaded && Groups.Count == 0;

    public bool ShowNotice => !IsLoading && !string.IsNullOrEmpty(NoticeText);

    /// <summary>
    /// Loads every section. Sets IsLoading in a try/finally and never throws: a feed that could not
    /// be read contributes no section, and only all six failing reaches the empty state. The one
    /// exception is cancellation, which is rethrown having left the sections as it found them.
    /// </summary>
    /// <param name="forceRefresh">
    /// Refetches past the freshness window first. GetOverviewAsync has no force of its own, so the
    /// feeds are warmed through GetFeedAsync and the overview then reads the fresh memo.
    /// </param>
    public async Task LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        var cancelled = false;

        try
        {
            if (forceRefresh)
            {
                await Task.WhenAll(FeedKinds.Feeds.Select(kind => _feedService.GetFeedAsync(kind, true, cancellationToken)));
            }

            var groups = await _feedService.GetOverviewAsync(ItemsPerSection, cancellationToken);

            Groups.Clear();

            foreach (var group in groups)
            {
                if (!group.HasItems)
                {
                    continue;
                }

                Groups.Add(new FeedGroupViewModel(group, SelectSectionCommand));
            }

            NoticeText = string.Empty;
            EmptyStateText = Groups.Count == 0 && !_feedService.SupportsLiveFetch
                ? Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app.")
                : Localized("Resources_Empty", "Nothing here yet");
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        catch (Exception ex)
        {
            // The service reports failure as an empty group, so this is a guard against a ViewModel-side bug.
            AppLog.Error("Loading the Resources overview failed", ex);
            NoticeText = Localized("Resources_LoadError", "We couldn't load the newest posts.");
            EmptyStateText = Localized("Resources_LoadError", "We couldn't load the newest posts.");
        }
        finally
        {
            if (!cancelled)
            {
                IsLoading = false;
                HasLoaded = true;
                OnPropertyChanged(nameof(ShowItems));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    /// <summary>Opens the tab a section heading stands for.</summary>
    [RelayCommand]
    private async Task SelectSectionAsync(FeedKind kind) => await _selectSection(kind);

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
