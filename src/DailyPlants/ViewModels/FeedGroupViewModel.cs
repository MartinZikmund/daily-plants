using DailyPlants.Models;

namespace DailyPlants.ViewModels;

/// <summary>
/// One section of the Latest overview: a feed's newest few items under a tappable heading that
/// takes you to that feed's own tab.
/// </summary>
public sealed class FeedGroupViewModel
{
    public FeedGroupViewModel(FeedGroup group, IAsyncRelayCommand<FeedKind> selectSectionCommand)
    {
        Kind = group.Kind;
        Title = FeedKindLabel.For(group.Kind);
        Items = group.Items.Select(item => new FeedItemViewModel(item)).ToList();
        SelectSectionCommand = selectSectionCommand;
    }

    public FeedKind Kind { get; }

    /// <summary>The section heading - the same label the feed's tab carries.</summary>
    public string Title { get; }

    /// <summary>AutomationProperties.AutomationId for the heading, e.g. "ResourcesOverviewHeadingBlog".</summary>
    public string HeadingAutomationId => $"ResourcesOverviewHeading{Kind}";

    public IReadOnlyList<FeedItemViewModel> Items { get; }

    /// <summary>Selects this section's tab. Pass <see cref="Kind"/> as the parameter.</summary>
    public IAsyncRelayCommand<FeedKind> SelectSectionCommand { get; }
}
