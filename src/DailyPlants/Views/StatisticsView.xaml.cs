using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Data;

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
}
