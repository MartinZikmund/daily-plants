using DailyDozen.Services;
using DailyDozen.ViewModels;
using Microsoft.UI.Xaml.Data;

namespace DailyDozen.Views;

public sealed partial class StatisticsPage : Page
{
    public StatisticsViewModel ViewModel { get; }

    public StatisticsPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        ViewModel = new StatisticsViewModel(dataService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += StatisticsPage_Loaded;
    }

    private async void StatisticsPage_Loaded(object sender, RoutedEventArgs e)
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
