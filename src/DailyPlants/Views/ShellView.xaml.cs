using DailyPlants.Models;
using DailyPlants.Services;
using Microsoft.UI.Xaml.Controls;

namespace DailyPlants.Views;

public sealed partial class ShellView : Page
{
    private IAchievementService? _achievementService;

    public ShellView()
    {
        this.InitializeComponent();
        this.Loaded += ShellView_Loaded;
    }

    private async void ShellView_Loaded(object sender, RoutedEventArgs e)
    {
        // Select the first item (Today) by default
        NavView.SelectedItem = NavView.MenuItems[0];

        // Initialize achievement service and subscribe to events
        _achievementService = App.Current.Services?.GetService<IAchievementService>();
        if (_achievementService != null)
        {
            _achievementService.AchievementEarned += OnAchievementEarned;
            await UpdateAchievementBadgeAsync();
        }
    }

    private void OnAchievementEarned(object? sender, Achievement achievement)
    {
        // Show notification popup
        DispatcherQueue.TryEnqueue(() =>
        {
            AchievementNotification.ShowAchievement(achievement);
            _ = UpdateAchievementBadgeAsync();
        });
    }

    private async Task UpdateAchievementBadgeAsync()
    {
        if (_achievementService == null) return;

        var unseenCount = await _achievementService.GetUnseenCountAsync();

        DispatcherQueue.TryEnqueue(() =>
        {
            if (unseenCount > 0)
            {
                AchievementsBadge.Value = unseenCount;
                AchievementsBadge.Visibility = Visibility.Visible;
            }
            else
            {
                AchievementsBadge.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            NavigateToPage(tag);

            // Clear badge when navigating to achievements
            if (tag == "Achievements")
            {
                AchievementsBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void NavigateToPage(string? tag)
    {
        Type? pageType = tag switch
        {
            "Today" => typeof(TodayView),
            "Statistics" => typeof(StatisticsView),
            "Achievements" => typeof(AchievementsView),
            "Settings" => typeof(SettingsView),
            "About" => typeof(AboutView),
            _ => null
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
