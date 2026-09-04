using DailyPlants.Services;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Input;

namespace DailyPlants.Controls;

/// <summary>
/// The "new from NutritionFacts.org" strip on the Diary page. Self-contained: it resolves
/// its own services so the Diary page needs one line of XAML and no ViewModel change.
/// </summary>
public sealed partial class LatestTeaser : UserControl
{
    public LatestTeaser()
    {
        var feedService = App.Current.Services!.GetRequiredService<IFeedService>();
        var navigator = App.Current.Services!.GetRequiredService<IAppNavigator>();
        ViewModel = new LatestTeaserViewModel(feedService, navigator);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += LatestTeaser_Loaded;
    }

    public LatestTeaserViewModel ViewModel { get; }

    private async void LatestTeaser_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void TeaserCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Tag first: ItemsRepeater does not set DataContext on x:Bind templates.
        if (sender is FrameworkElement element
            && (element.Tag as FeedItemViewModel ?? element.DataContext as FeedItemViewModel) is { } itemVm)
        {
            await itemVm.OpenCommand.ExecuteAsync(null);
        }
    }
}
