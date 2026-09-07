using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyPlants.ViewModels;

/// <summary>
/// The "Latest on ..." block of the item detail dialog: the three newest posts for that item's
/// nutritionfacts.org topic, and a "See all" that deep-links the Resources page to the same topic.
/// An item with no <see cref="ChecklistItem.TopicSlug"/> gets no block at all, and a fetch that
/// fails or comes back empty leaves the block absent rather than showing an error - the dialog is
/// not the place to report a feed problem.
/// </summary>
public partial class ItemTopicViewModel : ObservableObject
{
    /// <summary>How many posts the dialog shows. The rest are a tap away behind "See all".</summary>
    private const int HighlightCount = 3;

    private readonly IFeedService _feedService;
    private readonly IAppNavigator _navigator;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;

    /// <summary>The trimmed slug, or null when this item has no topic.</summary>
    private readonly string? _slug;

    private readonly string _name;

    public ItemTopicViewModel(
        IFeedService feedService,
        IAppNavigator navigator,
        ChecklistItem item,
        ILoggerFactory? loggerFactory = null)
    {
        _feedService = feedService;
        _navigator = navigator;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ItemTopicViewModel>() ?? NullLogger<ItemTopicViewModel>.Instance;
        _name = item.Name;

        // A head that cannot reach the topic feed counts as "no topic": no header, no fetch, no
        // "See all" onto a Resources page it does not have either.
        var slug = feedService.SupportsLiveFetch ? item.TopicSlug?.Trim() : null;
        _slug = string.IsNullOrEmpty(slug) ? null : slug;
    }

    /// <summary>
    /// False when the item has no topic, or when this head cannot fetch one. The dialog builds
    /// nothing at all in that case, so this is the one flag it has to read before anything else.
    /// </summary>
    public bool HasTopic => _slug is not null;

    /// <summary>Resources_TopicHeader filled in, e.g. "Latest on Berries".</summary>
    public string Header => string.Format(
        CultureInfo.CurrentCulture,
        Localizer.GetString("Resources_TopicHeader"),
        _name);

    /// <summary>At most <see cref="HighlightCount"/> cards, in the order the topic feed gave them.</summary>
    public ObservableCollection<FeedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    [NotifyPropertyChangedFor(nameof(ShowItems))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowItems))]
    public partial bool HasItems { get; set; }

    /// <summary>The small spinner in the block, up only while the fetch is out.</summary>
    public bool ShowProgress => HasTopic && IsLoading;

    /// <summary>The cards. Nothing fetched means nothing shown - no empty state, no error.</summary>
    public bool ShowItems => HasTopic && HasItems && !IsLoading;

    /// <summary>
    /// Raised by <see cref="SeeAllCommand"/> before the navigation, so the dialog can close itself
    /// first - navigating out from under an open ContentDialog leaves it on screen.
    /// </summary>
    public event EventHandler? SeeAllRequested;

    /// <summary>Closes the dialog and opens the Resources page in topic mode on this slug.</summary>
    [RelayCommand]
    private void SeeAll()
    {
        if (_slug is not { } slug)
        {
            return;
        }

        SeeAllRequested?.Invoke(this, EventArgs.Empty);
        _navigator.RequestNavigation("Resources", new ResourcesTopicRequest(slug, _name));
    }

    /// <summary>
    /// Fetches the newest posts for the topic. Called after the dialog is already on screen, so
    /// nothing waits on it - and it never throws: a failure just leaves the block hidden.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_slug is not { } slug)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var page = await _feedService.GetTopicPageAsync(slug, 1);

            Items.Clear();
            foreach (var item in page.Items.Take(HighlightCount))
            {
                Items.Add(new FeedItemViewModel(item, _loggerFactory));
            }

            HasItems = Items.Count > 0;
        }
        catch (Exception ex)
        {
            // The service reports failure as a status, so this is a guard against a ViewModel-side bug.
            _logger.LogError(ex, "Loading the \"{Slug}\" topic highlights failed", slug);
            HasItems = false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
