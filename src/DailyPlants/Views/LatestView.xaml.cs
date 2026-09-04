using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlants.Views;

public sealed partial class LatestView : Page
{
    private FeedKind? _initialKind;

    public LatestView()
    {
        var feedService = App.Current.Services!.GetRequiredService<IFeedService>();
        ViewModel = new LatestViewModel(feedService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += LatestView_Loaded;
    }

    public LatestViewModel ViewModel { get; }

    // Qualified in XAML so x:Bind emits a static call; an unqualified function binding
    // is emitted as an instance call and will not compile against a static method.
    public static double TabOpacity(bool isSelected) => isSelected ? 1.0 : 0.55;

    /// <summary>The Diary teaser passes a FeedKind name so the page opens on the matching tab.</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // ignoreCase to match LatestViewModel.SelectTabAsync, so one spelling works on both paths.
        _initialKind = e.Parameter is string name && Enum.TryParse<FeedKind>(name, ignoreCase: true, out var kind) ? kind : null;
    }

    private async void LatestView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync(_initialKind);
        _initialKind = null;
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
}
