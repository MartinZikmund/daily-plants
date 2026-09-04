using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// State for one feed tab on the Latest page.
/// </summary>
public partial class FeedTabViewModel : ObservableObject
{
    private readonly IFeedService _feedService;

    public FeedTabViewModel(IFeedService feedService, FeedKind kind, string title)
    {
        _feedService = feedService;
        Kind = kind;
        Title = title;
    }

    public FeedKind Kind { get; }

    /// <summary>Localized tab label (Latest_TabBlog / _TabVideos / _TabPodcast).</summary>
    public string Title { get; }

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

    /// <summary>Latest_Offline / _BrowserUnavailable / _LoadError, or empty when all is well.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotice))]
    private string _noticeText = string.Empty;

    /// <summary>Latest_Empty headline, or the status-specific replacement.</summary>
    [ObservableProperty]
    private string _emptyStateText = string.Empty;

    /// <summary>Latest_UpdatedAt formatted, or empty when never fetched.</summary>
    [ObservableProperty]
    private string _updatedText = string.Empty;

    public bool ShowItems => Items.Count > 0;

    public bool ShowEmptyState => !IsLoading && HasLoaded && Items.Count == 0;

    public bool ShowNotice => !IsLoading && !string.IsNullOrEmpty(NoticeText);

    /// <summary>Loads (or reloads) this tab. Sets IsLoading in a try/finally; never throws.</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;

        try
        {
            var result = await _feedService.GetFeedAsync(Kind, forceRefresh);

            Items.Clear();
            foreach (var item in result.Items)
            {
                Items.Add(new FeedItemViewModel(item));
            }

            ApplyStatus(result);
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            AppLog.Error($"Loading the {Kind} feed failed", ex);
            NoticeText = Localized("Latest_LoadError", "We couldn't load the newest posts.");
        }
        finally
        {
            IsLoading = false;
            HasLoaded = true;
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    private void ApplyStatus(FeedResult result)
    {
        UpdatedText = result.FetchedAt is { } fetchedAt
            ? string.Format(
                CultureInfo.CurrentCulture,
                Localized("Latest_UpdatedAt", "Updated {0}"),
                fetchedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture))
            : string.Empty;

        switch (result.Status)
        {
            case FeedResultStatus.Stale:
                NoticeText = Localized("Latest_Offline", "Showing saved posts — we couldn't reach NutritionFacts.org.");
                EmptyStateText = Localized("Latest_Empty", "Nothing here yet");
                break;

            case FeedResultStatus.Unavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Latest_LoadError", "We couldn't load the newest posts.");
                break;

            case FeedResultStatus.LiveFetchUnavailable:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Latest_BrowserUnavailable", "New posts can't be fetched in the browser version of the app.");
                break;

            default:
                NoticeText = string.Empty;
                EmptyStateText = Localized("Latest_Empty", "Nothing here yet");
                break;
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
