using DailyPlants.Infrastructure;
using Microsoft.UI.Xaml.Automation;

namespace DailyPlants.Controls;

/// <summary>
/// The "DEV" pill, with the worktree name when the build came from one. Collapsed in Prod.
/// </summary>
public sealed partial class DevChannelBadge : UserControl
{
    public DevChannelBadge()
    {
        InitializeComponent();

        if (!AppEnvironment.IsDevChannel)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        BadgeText.Text = AppEnvironment.ChannelBadgeLabel;
        AutomationProperties.SetName(this, AppEnvironment.ChannelBadgeLabel);
        Loaded += (_, _) => VisualStateManager.GoToState(this, AppEnvironment.IsCiBuild ? "Ci" : "Local", false);
    }
}
