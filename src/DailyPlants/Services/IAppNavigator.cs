namespace DailyPlants.Services;

/// <summary>
/// A request to move the shell to a page, optionally with a parameter.
/// </summary>
/// <param name="PageTag">The NavigationViewItem Tag, e.g. "Resources".</param>
/// <param name="Parameter">
/// Passed straight to Frame.Navigate; keep it to simple types - a string, or one of the small
/// parameter records declared alongside this one.
/// </param>
public sealed record AppNavigationRequest(string PageTag, object? Parameter);

/// <summary>
/// The parameter that opens the Resources page in topic mode - on one nutritionfacts.org topic
/// rather than on a tab. Carried by the same <see cref="AppNavigationRequest"/> the tab deep link
/// uses, so there is still only the one way into the page.
/// </summary>
/// <param name="Slug">The topic slug, as in <c>ChecklistItem.TopicSlug</c> (e.g. "flax-seeds").</param>
/// <param name="Title">The item name the header reads back, e.g. "Flaxseeds".</param>
public sealed record ResourcesTopicRequest(string Slug, string Title);

/// <summary>
/// Lets a view request shell navigation without holding a reference to ShellView.
/// </summary>
public interface IAppNavigator
{
    /// <summary>
    /// Raised when a view asks the shell to navigate. ShellView is the only subscriber.
    /// </summary>
    event EventHandler<AppNavigationRequest>? NavigationRequested;

    /// <summary>
    /// Asks the shell to navigate to <paramref name="pageTag"/>.
    /// </summary>
    void RequestNavigation(string pageTag, object? parameter = null);
}

/// <summary>
/// The default <see cref="IAppNavigator"/>: a singleton whose event ShellView subscribes to,
/// mirroring <see cref="IAchievementService.AchievementEarned"/>.
/// </summary>
public sealed class AppNavigator : IAppNavigator
{
    /// <inheritdoc />
    public event EventHandler<AppNavigationRequest>? NavigationRequested;

    /// <inheritdoc />
    public void RequestNavigation(string pageTag, object? parameter = null)
        => NavigationRequested?.Invoke(this, new AppNavigationRequest(pageTag, parameter));
}
