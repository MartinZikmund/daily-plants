using MZikmund.Toolkit.WinUI.Services;

namespace DailyPlants.Services.Settings;

public class AppPreferences : IAppPreferences
{
    private const string DailyDozenEnabledKey = "DailyDozenEnabled";
    private const string TwentyOneTweaksEnabledKey = "TwentyOneTweaksEnabled";
    private const string AntiAgingEightEnabledKey = "AntiAgingEightEnabled";
    private const string WeightTrackingEnabledKey = "WeightTrackingEnabled";
    private const string UseMetricUnitsKey = "UseMetricUnits";
    private const string HeightCmKey = "HeightCm";
    private const string GoalWeightKey = "GoalWeight";
    private const string ThemePreferenceKey = "ThemePreference";
    private const string LanguageKey = "Language";

    private readonly IPreferences _preferences;

    public AppPreferences(IPreferences preferences)
    {
        _preferences = preferences;
    }

    public bool DailyDozenEnabled
    {
        get => _preferences.Get(DailyDozenEnabledKey, true);
        set => _preferences.Set(DailyDozenEnabledKey, value);
    }

    public bool TwentyOneTweaksEnabled
    {
        get => _preferences.Get(TwentyOneTweaksEnabledKey, false);
        set => _preferences.Set(TwentyOneTweaksEnabledKey, value);
    }

    public bool AntiAgingEightEnabled
    {
        get => _preferences.Get(AntiAgingEightEnabledKey, false);
        set => _preferences.Set(AntiAgingEightEnabledKey, value);
    }

    public bool WeightTrackingEnabled
    {
        get => _preferences.Get(WeightTrackingEnabledKey, false);
        set => _preferences.Set(WeightTrackingEnabledKey, value);
    }

    public bool UseMetricUnits
    {
        get => _preferences.Get(UseMetricUnitsKey, true);
        set => _preferences.Set(UseMetricUnitsKey, value);
    }

    public double? HeightCm
    {
        get
        {
            var value = _preferences.Get(HeightCmKey, double.NaN);
            return double.IsNaN(value) ? null : value;
        }
        set => _preferences.Set(HeightCmKey, value ?? double.NaN);
    }

    public double? GoalWeight
    {
        get
        {
            var value = _preferences.Get(GoalWeightKey, double.NaN);
            return double.IsNaN(value) ? null : value;
        }
        set => _preferences.Set(GoalWeightKey, value ?? double.NaN);
    }

    public int ThemePreference
    {
        get => _preferences.Get(ThemePreferenceKey, 0);
        set => _preferences.Set(ThemePreferenceKey, value);
    }

    public string? Language
    {
        get
        {
            var value = _preferences.Get(LanguageKey, string.Empty);
            return string.IsNullOrEmpty(value) ? null : value;
        }
        set => _preferences.Set(LanguageKey, value ?? string.Empty);
    }
}
