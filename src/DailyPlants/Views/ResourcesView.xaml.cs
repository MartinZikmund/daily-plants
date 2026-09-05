using System.ComponentModel;
using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlants.Views;

public sealed partial class ResourcesView : Page
{
    /// <summary>How close the viewport has to get to the end of the content before the next page is asked for.</summary>
    private const double LoadMoreThreshold = 600;

    private FeedKind? _initialKind;

    public ResourcesView()
    {
        var feedService = App.Current.Services!.GetRequiredService<IFeedService>();
        ViewModel = new ResourcesViewModel(feedService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += ResourcesView_Loaded;

        // The ViewModel is owned by this page and dies with it, so there is nothing to unhook.
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public ResourcesViewModel ViewModel { get; }

    // Qualified in XAML so x:Bind emits a static call; an unqualified function binding
    // is emitted as an instance call and will not compile against a static method.
    public static double TabOpacity(bool isSelected) => isSelected ? 1.0 : 0.55;

    /// <summary>The tabs and the search results header are alternatives, never both at once.</summary>
    public static Visibility TabsVisibility(bool isSearchActive) => isSearchActive ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The paginated list and the Latest overview are alternatives too.</summary>
    public static Visibility ListVisibility(bool isOverviewActive) => isOverviewActive ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// The tab icon for a feed, or the overview's own when <paramref name="kind"/> is null. The
    /// icons live in Styles/Controls.xaml, which is merged into the application's resources. They
    /// are stored as PathIcons because XAML converts the path mini-language only when it lands on
    /// a Geometry-typed property; only their Data is ever used.
    /// </summary>
    public static DataTemplate IconFor(FeedKind? kind) => (DataTemplate)Application.Current.Resources[kind switch
    {
        FeedKind.Blog => "DpResIconBlog",
        FeedKind.Videos => "DpResIconVideos",
        FeedKind.Podcast => "DpResIconPodcast",
        FeedKind.Recipes => "DpResIconRecipes",
        FeedKind.Questions => "DpResIconQuestions",
        FeedKind.Webinars => "DpResIconWebinars",
        null => "DpResIconLatest",
        _ => "DpResIconBlog"
    }];

    /// <summary>
    /// <see cref="IconFor"/> for an overview section, whose Kind is not nullable. A separate name
    /// rather than an overload, so the function binding cannot resolve to the wrong one.
    /// </summary>
    public static DataTemplate SectionIcon(FeedKind kind) => IconFor(kind);

    /// <summary>Resources_SearchResultsFor with the query the results are actually for.</summary>
    public static string SearchResultsHeader(string query)
        => string.Format(CultureInfo.CurrentCulture, Localized("Resources_SearchResultsFor", "Results for “{0}”"), query);

    /// <summary>The Diary teaser passes a FeedKind name so the page opens on the matching tab.</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // ignoreCase to match ResourcesViewModel.SelectTabAsync, so one spelling works on both paths.
        _initialKind = e.Parameter is string name && Enum.TryParse<FeedKind>(name, ignoreCase: true, out var kind) ? kind : null;
    }

    private async void ResourcesView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync(_initialKind);
        _initialKind = null;
    }

    /// <summary>A different list means different items, so the old scroll offset means nothing in it.</summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResourcesViewModel.ActiveList))
        {
            FeedScrollViewer.ChangeView(null, 0, null, disableAnimation: true);
        }
    }

    /// <summary>
    /// Infinite scroll. This fires continuously while a scroll is in flight, so it stays arithmetic
    /// only - LoadMoreAsync does the guarding and no-ops when a page is already on its way or the
    /// list has ended.
    /// </summary>
    private void FeedScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        var remaining = FeedScrollViewer.ExtentHeight - FeedScrollViewer.VerticalOffset - FeedScrollViewer.ViewportHeight;

        if (remaining <= LoadMoreThreshold)
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }
    }

    /// <summary>
    /// A tab in the wide strip. The command lives on the page's ViewModel rather than on the item,
    /// which an ItemsRepeater template cannot reach, so the tab arrives through the button instead.
    /// </summary>
    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element
            && (element.Tag as IResourceTab ?? element.DataContext as IResourceTab) is { } tab)
        {
            ViewModel.SelectTabCommand.Execute(tab);
        }
    }

    /// <summary>
    /// The narrow-window picker. SelectedItem is bound one way, so this is the only path that
    /// selects a tab - and it ignores the echo of a selection that came from somewhere else, which
    /// is what keeps the picker and the strip from both driving the ViewModel.
    /// </summary>
    private void TabSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabSelector.SelectedItem is not IResourceTab tab)
        {
            // A ComboBox drops its selection while its items are being (re)applied. Put it back
            // rather than letting an empty picker claim the ViewModel has no tab selected.
            TabSelector.SelectedItem = ViewModel.SelectedTab;
            return;
        }

        if (!ReferenceEquals(tab, ViewModel.SelectedTab))
        {
            ViewModel.SelectTabCommand.Execute(tab);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => ViewModel.SubmitSearchCommand.Execute(args.QueryText);

    /// <summary>
    /// The box's own clear button only empties the text, so without this the results would stay
    /// on screen until the user pressed Enter on an empty box.
    /// </summary>
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput
            && string.IsNullOrWhiteSpace(sender.Text))
        {
            ViewModel.ClearSearchCommand.Execute(null);
        }
    }

    private async void FeedCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Tag first: ItemsRepeater does not set DataContext on x:Bind templates.
        if (sender is FrameworkElement element
            && (element.Tag as FeedItemViewModel ?? element.DataContext as FeedItemViewModel) is { } itemVm)
        {
            await itemVm.OpenCommand.ExecuteAsync(null);
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

/// <summary>
/// <see cref="ResourcesView.TabOpacity"/> as a converter, for the tab strip.
/// </summary>
/// <remarks>
/// The strip's items are typed as <see cref="IResourceTab"/>, which does not implement
/// <see cref="INotifyPropertyChanged"/>, so an <c>x:Bind</c> OneWay on <c>IsSelected</c> would
/// compile and then never fire. A classic binding finds the notification on the instance at
/// runtime instead - and a classic binding needs a converter where a function binding would not.
/// </remarks>
public sealed class TabOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => ResourcesView.TabOpacity(value is true);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
