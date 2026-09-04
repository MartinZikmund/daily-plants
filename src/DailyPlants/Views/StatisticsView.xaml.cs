using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class StatisticsView : Page
{
    public StatisticsViewModel ViewModel { get; }

    public StatisticsView()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var appPreferences = App.Current.Services!.GetRequiredService<IAppPreferences>();
        ViewModel = new StatisticsViewModel(dataService, appPreferences);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += StatisticsView_Loaded;
    }

    private async void StatisticsView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadStatisticsAsync();
    }

    public Visibility Not(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Visibility VisibleIfAny(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VisibleIfZero(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
