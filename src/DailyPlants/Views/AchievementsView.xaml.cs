using DailyPlants.Services;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DailyPlants.Views;

public sealed partial class AchievementsView : Page
{
    public AchievementsViewModel ViewModel { get; }

    public AchievementsView()
    {
        var achievementService = App.Current.Services!.GetRequiredService<IAchievementService>();
        ViewModel = new AchievementsViewModel(achievementService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += AchievementsView_Loaded;
    }

    private async void AchievementsView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAchievementsAsync();
    }
}
