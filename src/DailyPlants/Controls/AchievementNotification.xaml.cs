using DailyPlants.Helpers;
using DailyPlants.Models;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace DailyPlants.Controls;

public sealed partial class AchievementNotification : UserControl
{
    private readonly Queue<Achievement> _pendingNotifications = new();
    private bool _isShowing;
    private DispatcherTimer? _autoHideTimer;

    public AchievementNotification()
    {
        this.InitializeComponent();
    }

    public void ShowAchievement(Achievement achievement)
    {
        _pendingNotifications.Enqueue(achievement);

        if (!_isShowing)
        {
            ShowNextNotification();
        }
    }

    private void ShowNextNotification()
    {
        if (_pendingNotifications.Count == 0)
        {
            _isShowing = false;
            return;
        }

        _isShowing = true;
        var achievement = _pendingNotifications.Dequeue();

        // Update UI
        AchievementName.Text = Localizer.GetString(achievement.NameKey);
        AchievementDescription.Text = Localizer.GetString(achievement.DescriptionKey);
        AchievementIcon.Glyph = achievement.IconGlyph;

        // Set badge color
        try
        {
            var hex = achievement.BadgeColor.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                IconBorder.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
        }
        catch
        {
            IconBorder.Background = new SolidColorBrush(Color.FromArgb(255, 136, 136, 136));
        }

        // Show with animation
        RootGrid.Visibility = Visibility.Visible;
        AnimateIn();

        // Auto-hide after 5 seconds
        _autoHideTimer?.Stop();
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoHideTimer.Tick += (s, e) =>
        {
            _autoHideTimer.Stop();
            Hide();
        };
        _autoHideTimer.Start();
    }

    private void AnimateIn()
    {
        var storyboard = new Storyboard();

        var slideAnimation = new DoubleAnimation
        {
            From = -100,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideAnimation, SlideTransform);
        Storyboard.SetTargetProperty(slideAnimation, "Y");
        storyboard.Children.Add(slideAnimation);

        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(300))
        };
        Storyboard.SetTarget(fadeAnimation, RootGrid);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        storyboard.Children.Add(fadeAnimation);

        storyboard.Begin();
    }

    private void AnimateOut(Action? onCompleted = null)
    {
        var storyboard = new Storyboard();

        var slideAnimation = new DoubleAnimation
        {
            From = 0,
            To = -100,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slideAnimation, SlideTransform);
        Storyboard.SetTargetProperty(slideAnimation, "Y");
        storyboard.Children.Add(slideAnimation);

        var fadeAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };
        Storyboard.SetTarget(fadeAnimation, RootGrid);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        storyboard.Children.Add(fadeAnimation);

        storyboard.Completed += (s, e) =>
        {
            RootGrid.Visibility = Visibility.Collapsed;
            onCompleted?.Invoke();
        };

        storyboard.Begin();
    }

    private void Hide()
    {
        AnimateOut(() =>
        {
            // Show next notification if there are more
            if (_pendingNotifications.Count > 0)
            {
                ShowNextNotification();
            }
            else
            {
                _isShowing = false;
            }
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _autoHideTimer?.Stop();
        Hide();
    }
}

/// <summary>
/// Converts a hex color string to a Color.
/// </summary>
public class HexToColorConverter : IValueConverter
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
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                // Fall through to default
            }
        }
        return Color.FromArgb(255, 136, 136, 136);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
