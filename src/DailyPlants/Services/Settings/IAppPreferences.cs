namespace DailyPlants.Services.Settings;

public interface IAppPreferences
{
    bool DailyDozenEnabled { get; set; }
    bool TwentyOneTweaksEnabled { get; set; }
    bool AntiAgingEightEnabled { get; set; }
    bool WeightTrackingEnabled { get; set; }
    bool UseMetricUnits { get; set; }
    double? HeightCm { get; set; }
    double? GoalWeight { get; set; }
    int ThemePreference { get; set; }
    string? Language { get; set; }
}
