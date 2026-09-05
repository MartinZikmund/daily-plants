using DailyPlants.Models;

namespace DailyPlants.ViewModels;

/// <summary>
/// One tab on the Resources page. Implemented by <see cref="FeedListViewModel"/> (a feed) and
/// <see cref="LatestOverviewViewModel"/> (the overview), so the strip and the narrow-window
/// picker can render all seven from a single list.
/// </summary>
public interface IResourceTab
{
    /// <summary>The localized label, shown on the tab and - on the overview - above its section.</summary>
    string Title { get; }

    /// <summary>
    /// AutomationProperties.AutomationId for the tab's button, e.g. "ResourcesTabBlogButton".
    /// UI automation drives the page by these, so they are part of the contract.
    /// </summary>
    string TabAutomationId { get; }

    /// <summary>The feed this tab shows, or null for the overview.</summary>
    FeedKind? Kind { get; }

    /// <summary>True while this is the selected tab. Maintained by <see cref="ResourcesViewModel"/>.</summary>
    bool IsSelected { get; }
}
