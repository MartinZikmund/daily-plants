namespace DailyPlants.Models;

/// <summary>
/// Records when a user earned a specific achievement.
/// </summary>
public class EarnedAchievement
{
    /// <summary>
    /// Database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The achievement ID that was earned.
    /// </summary>
    public required string AchievementId { get; set; }

    /// <summary>
    /// When the achievement was earned.
    /// </summary>
    public required DateTime EarnedAt { get; set; }

    /// <summary>
    /// Whether the user has seen/acknowledged this achievement.
    /// Used for showing the notification badge.
    /// </summary>
    public bool HasBeenSeen { get; set; }
}
