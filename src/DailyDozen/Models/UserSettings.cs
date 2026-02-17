namespace DailyDozen.Models;

/// <summary>
/// User preferences and settings.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Whether the Daily Dozen checklist is enabled.
    /// </summary>
    public bool DailyDozenEnabled { get; set; } = true;

    /// <summary>
    /// Whether the Twenty-One Tweaks checklist is enabled.
    /// </summary>
    public bool TwentyOneTweaksEnabled { get; set; } = false;

    /// <summary>
    /// Whether the Anti-Aging Eight checklist is enabled.
    /// </summary>
    public bool AntiAgingEightEnabled { get; set; } = false;

    /// <summary>
    /// Whether weight tracking is enabled.
    /// </summary>
    public bool WeightTrackingEnabled { get; set; } = false;

    /// <summary>
    /// Whether to use metric units (kg, cm) or imperial (lb, in).
    /// </summary>
    public bool UseMetricUnits { get; set; } = true;

    /// <summary>
    /// User's height in centimeters (for BMI calculation).
    /// </summary>
    public double? HeightCm { get; set; }

    /// <summary>
    /// User's goal weight in their preferred unit.
    /// </summary>
    public double? GoalWeight { get; set; }

    /// <summary>
    /// Theme preference: 0 = System, 1 = Light, 2 = Dark.
    /// </summary>
    public int ThemePreference { get; set; } = 0;

    /// <summary>
    /// Language code (e.g., "en", "cs"). Null means system default.
    /// </summary>
    public string? Language { get; set; }
}
