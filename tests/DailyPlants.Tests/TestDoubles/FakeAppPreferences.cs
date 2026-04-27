namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Direct in-memory implementation of <see cref="IAppPreferences"/> for VM and service tests
/// that don't need to exercise <see cref="AppPreferences"/> itself.
/// </summary>
internal sealed class FakeAppPreferences : IAppPreferences
{
    public bool DailyDozenEnabled { get; set; } = true;
    public bool TwentyOneTweaksEnabled { get; set; }
    public bool WeightTrackingEnabled { get; set; }
    public bool UseMetricUnits { get; set; } = true;
    public double? HeightCm { get; set; }
    public double? GoalWeight { get; set; }
    public int ThemePreference { get; set; }
    public string? Language { get; set; }
    public string DisabledItemIds { get; set; } = string.Empty;
}
