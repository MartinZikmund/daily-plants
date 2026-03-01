namespace DailyPlants.Models;

/// <summary>
/// Defines an achievement that can be earned.
/// </summary>
public partial record Achievement
{
    /// <summary>
    /// Unique identifier for this achievement.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Localization key for the achievement name.
    /// </summary>
    public required string NameKey { get; init; }

    /// <summary>
    /// Localization key for the achievement description.
    /// </summary>
    public required string DescriptionKey { get; init; }

    /// <summary>
    /// The category of this achievement.
    /// </summary>
    public required AchievementType Type { get; init; }

    /// <summary>
    /// Icon glyph for the achievement (Segoe Fluent Icons).
    /// </summary>
    public required string IconGlyph { get; init; }

    /// <summary>
    /// Color for the achievement badge (hex color code).
    /// </summary>
    public required string BadgeColor { get; init; }

    /// <summary>
    /// The target value needed to earn this achievement.
    /// For streaks: number of consecutive days.
    /// For item-specific: number of times completed.
    /// For completion: number of perfect days.
    /// </summary>
    public int TargetValue { get; init; }

    /// <summary>
    /// Optional: The specific item ID this achievement is for (item-specific only).
    /// </summary>
    public string? ItemId { get; init; }
}
