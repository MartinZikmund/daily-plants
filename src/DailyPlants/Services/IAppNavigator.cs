namespace DailyPlants.Services;

/// <summary>
/// A request to move the shell to a page, optionally with a parameter.
/// </summary>
/// <param name="PageTag">The NavigationViewItem Tag, e.g. "Latest".</param>
/// <param name="Parameter">Passed straight to Frame.Navigate; keep it to simple types (string).</param>
public sealed record AppNavigationRequest(string PageTag, object? Parameter);

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
