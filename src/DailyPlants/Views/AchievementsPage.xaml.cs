using DailyPlants.Services;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DailyPlants.Views;

public sealed partial class AchievementsPage : Page
{
    public AchievementsViewModel ViewModel { get; }

    public AchievementsPage()
    {
        var achievementService = App.Current.Services!.GetRequiredService<IAchievementService>();
        ViewModel = new AchievementsViewModel(achievementService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += AchievementsPage_Loaded;
    }

    private async void AchievementsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAchievementsAsync();
    }
}

/// <summary>
/// Converts a hex color string to a SolidColorBrush.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(255, r, g, b));
                }
            }
            catch
            {
                // Fall through to default
            }
        }
        return new SolidColorBrush(Color.FromArgb(255, 136, 136, 136));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
