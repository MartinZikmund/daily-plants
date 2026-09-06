namespace DailyPlants.Services.Settings;

public interface IAppPreferences
{
    bool DailyDozenEnabled { get; set; }
    bool TwentyOneTweaksEnabled { get; set; }
    bool WeightTrackingEnabled { get; set; }
    bool UseMetricUnits { get; set; }
    double? HeightCm { get; set; }
    double? GoalWeight { get; set; }
    int ThemePreference { get; set; }
    string? Language { get; set; }
    string DisabledItemIds { get; set; }

    /// <summary>
    /// Whether <see cref="HeightCm"/> and <see cref="GoalWeight"/> are already stored in
    /// centimetres and kilograms. Preferences live outside the database, so they cannot be
    /// gated on its schema version - a database that is reset or replaced would convert
    /// them a second time.
    /// </summary>
    bool UnitsAreCanonical { get; set; }

    /// <summary>
    /// Whether the user has been told that changing which items are tracked also changes
    /// the streaks and perfect days already on record. Shown once, then never again.
    /// </summary>
    bool HasSeenChecklistImpactWarning { get; set; }
}
