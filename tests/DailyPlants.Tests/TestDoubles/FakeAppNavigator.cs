namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Recording <see cref="IAppNavigator"/> for ViewModel tests. Mirrors <see cref="AppNavigator"/>:
/// it still raises <see cref="NavigationRequested"/>, and additionally remembers the last request
/// so a test can assert on it without subscribing.
/// </summary>
internal sealed class FakeAppNavigator : IAppNavigator
{
    public event EventHandler<AppNavigationRequest>? NavigationRequested;

    public string? LastPageTag { get; private set; }

    public object? LastParameter { get; private set; }

    public int RequestCount { get; private set; }

    public void RequestNavigation(string pageTag, object? parameter = null)
    {
        LastPageTag = pageTag;
        LastParameter = parameter;
        RequestCount++;
        NavigationRequested?.Invoke(this, new AppNavigationRequest(pageTag, parameter));
    }
}
