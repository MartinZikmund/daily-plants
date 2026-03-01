using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Service for managing achievements.
/// </summary>
public interface IAchievementService
{
    /// <summary>
    /// Event raised when a new achievement is earned.
    /// </summary>
    event EventHandler<Achievement>? AchievementEarned;

    /// <summary>
    /// Initializes the service.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Gets all achievement definitions.
    /// </summary>
    IReadOnlyList<Achievement> GetAllAchievements();

    /// <summary>
    /// Gets all achievements the user has earned.
    /// </summary>
    Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync();

    /// <summary>
    /// Checks if a specific achievement has been earned.
    /// </summary>
    Task<bool> IsAchievementEarnedAsync(string achievementId);

    /// <summary>
    /// Gets the count of unseen achievements.
    /// </summary>
    Task<int> GetUnseenCountAsync();

    /// <summary>
    /// Marks all achievements as seen.
    /// </summary>
    Task MarkAllAsSeenAsync();

    /// <summary>
    /// Checks and awards any newly earned achievements based on current data.
    /// Should be called after user actions that could trigger achievements.
    /// </summary>
    Task CheckAndAwardAchievementsAsync();

    /// <summary>
    /// Gets progress toward a specific achievement (0.0 to 1.0).
    /// </summary>
    Task<double> GetProgressAsync(string achievementId);

    /// <summary>
    /// Gets the current value for an achievement (e.g., current streak count).
    /// </summary>
    Task<int> GetCurrentValueAsync(string achievementId);
}
