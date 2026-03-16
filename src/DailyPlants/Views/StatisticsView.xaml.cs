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

/// <summary>
/// Converts a progress value (0-1) to a height for vertical bar charts.
/// </summary>
public class ProgressToHeightConverter : IValueConverter
{
    public double MaxHeight { get; set; } = 60;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double progress)
        {
            return Math.Max(2, progress * MaxHeight); // Minimum 2px so bar is visible
        }
        return 2;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
