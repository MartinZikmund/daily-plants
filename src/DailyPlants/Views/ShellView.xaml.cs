using DailyPlants.Models;
using DailyPlants.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Metadata;
using Windows.UI;

namespace DailyPlants.Views;

public sealed partial class ShellView : Page
{
    private IAchievementService? _achievementService;
    private IAppNavigator? _appNavigator;
    private object? _pendingNavigationParameter;
    private readonly Window _associatedWindow;

    public ShellView(Window associatedWindow)
    {
        this.InitializeComponent();
        _associatedWindow = associatedWindow;
        this.Loaded += ShellView_Loaded;
        CustomizeWindow();
    }

    public bool HasCustomTitleBar { get; private set; }

    private void CustomizeWindow()
    {
        SetMinWindowSizing();
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _associatedWindow.ExtendsContentIntoTitleBar = true;
            _associatedWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _associatedWindow.SetTitleBar(DraggableTitleBar);
            HasCustomTitleBar = true;
        }
        if (MicaController.IsSupported())
        {
            _associatedWindow.SystemBackdrop = new MicaBackdrop();
            Background = null;
        }
    }

    private void SetMinWindowSizing()
    {
        if (_associatedWindow.AppWindow.Presenter is OverlappedPresenter overlappedPresenter && XamlRoot is not null)
        {
            overlappedPresenter.PreferredMinimumWidth = (int)(500 * XamlRoot.RasterizationScale);
            overlappedPresenter.PreferredMinimumHeight = (int)(400 * XamlRoot.RasterizationScale);
        }

#if !HAS_UNO
        if (_associatedWindow.ExtendsContentIntoTitleBar)
        {
            DraggableTitleBar.Margin = new Thickness(DraggableTitleBar.Margin.Left, 0, _associatedWindow.AppWindow.TitleBar.RightInset / XamlRoot.RasterizationScale, 0);
        }
#endif
    }

    /// <summary>
    /// The caption buttons are drawn by the system and do not follow the app's
    /// ElementTheme, so they stay light after a switch to light theme unless their
    /// colours are pushed across explicitly.
    /// </summary>
    private void UpdateTitleBarColors()
    {
        if (!HasCustomTitleBar)
        {
            return;
        }

        var titleBar = _associatedWindow.AppWindow.TitleBar;
        var ink = (ThemeProbe.Background as SolidColorBrush)?.Color;
        var muted = (ThemeProbe.BorderBrush as SolidColorBrush)?.Color;

        if (ink is not { } foreground)
        {
            return;
        }

        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = muted ?? foreground;

        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

        // A wash of the foreground reads correctly in both themes without a second token.
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(28, foreground.R, foreground.G, foreground.B);
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(56, foreground.R, foreground.G, foreground.B);
    }

    private void ShellView_ActualThemeChanged(FrameworkElement sender, object args) => UpdateTitleBarColors();

    private async void ShellView_Loaded(object sender, RoutedEventArgs e)
    {
        XamlRoot.Changed += XamlRoot_Changed;
        SetMinWindowSizing();

        this.ActualThemeChanged += ShellView_ActualThemeChanged;
        UpdateTitleBarColors();

        // Select the first item (Diary) by default
        NavView.SelectedItem = NavView.MenuItems[0];

        // Initialize achievement service and subscribe to events
        _achievementService = App.Current.Services?.GetService<IAchievementService>();
        if (_achievementService != null)
        {
            _achievementService.AchievementEarned += OnAchievementEarned;
            await UpdateAchievementBadgeAsync();
        }

        _appNavigator = App.Current.Services?.GetService<IAppNavigator>();
        if (_appNavigator != null)
        {
            _appNavigator.NavigationRequested += OnNavigationRequested;
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        SetMinWindowSizing();
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
            var parameter = _pendingNavigationParameter;
            _pendingNavigationParameter = null;
            NavigateToPage(tag, parameter);

            // Clear badge when navigating to achievements
            if (tag == "Achievements")
            {
                AchievementsBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void OnNavigationRequested(object? sender, AppNavigationRequest request)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var item = NavView.MenuItems.Concat(NavView.FooterMenuItems)
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => (string?)i.Tag == request.PageTag);
            if (item is null)
            {
                return;
            }

            if (ReferenceEquals(NavView.SelectedItem, item))
            {
                // Already selected, so SelectionChanged will not fire - navigate directly.
                NavigateToPage(request.PageTag, request.Parameter);
                return;
            }

            _pendingNavigationParameter = request.Parameter;
            NavView.SelectedItem = item;
        });
    }

    private void NavigateToPage(string? tag, object? parameter = null)
    {
        Type? pageType = tag switch
        {
            "Diary" => typeof(DiaryView),
            "Resources" => typeof(ResourcesView),
            "Statistics" => typeof(StatisticsView),
            "Achievements" => typeof(AchievementsView),
            "Settings" => typeof(SettingsView),
            "About" => typeof(AboutView),
            _ => null
        };

        // A deep link re-navigates even when the page is already showing, so the target tab changes.
        if (pageType != null && (ContentFrame.CurrentSourcePageType != pageType || parameter is not null))
        {
            ContentFrame.Navigate(pageType, parameter);
        }
    }
}
