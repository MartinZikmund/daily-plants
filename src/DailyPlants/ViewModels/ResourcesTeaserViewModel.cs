using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// The "new from NutritionFacts.org" strip at the foot of the Diary page.
/// </summary>
public partial class ResourcesTeaserViewModel : ObservableObject
{
    private const int TeaserCount = 3;

    private readonly IFeedService _feedService;
    private readonly IAppNavigator _navigator;

    public ResourcesTeaserViewModel(IFeedService feedService, IAppNavigator navigator)
    {
        _feedService = feedService;
        _navigator = navigator;
    }

    public ObservableCollection<FeedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStrip))]
    private bool _isLoading;

    /// <summary>Collapses the whole strip until there is something worth showing - the Diary page must never grow an empty hole.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStrip))]
    private bool _hasItems;

    public bool ShowStrip => HasItems && !IsLoading;

    /// <summary>Deep-links to the Resources page, opening the tab the given item came from.</summary>
    [RelayCommand]
    private void OpenResources(string? kindName)
        => _navigator.RequestNavigation("Resources", kindName);

    /// <summary>Loads the three newest items across all feeds. Never throws.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var items = await _feedService.GetLatestAcrossFeedsAsync(TeaserCount);

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(new FeedItemViewModel(item));
            }

            HasItems = Items.Count > 0;
        }
        catch (Exception ex)
        {
            // The Diary page is not the place to report a feed problem - stay collapsed instead.
            AppLog.Error("Loading the Diary teaser failed", ex);
            HasItems = false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
