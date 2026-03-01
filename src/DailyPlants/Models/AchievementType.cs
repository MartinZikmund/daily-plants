namespace DailyPlants.Models;

/// <summary>
/// Categories of achievements.
/// </summary>
public enum AchievementType
{
    /// <summary>
    /// Achievements based on consecutive days of 100% completion.
    /// </summary>
    Streak,

    /// <summary>
    /// First-time accomplishments (first day, first week, etc.).
    /// </summary>
    Milestone,

    /// <summary>
    /// Achievements for completing specific items multiple times.
    /// </summary>
    ItemSpecific,

    /// <summary>
    /// Achievements for completing days with 100% completion.
    /// </summary>
    Completion
}
