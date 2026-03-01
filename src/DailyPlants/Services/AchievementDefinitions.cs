using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Contains all achievement definitions.
/// </summary>
public static class AchievementDefinitions
{
    // Colors for different achievement types
    private const string StreakColor = "#FFD700";      // Gold
    private const string MilestoneColor = "#4CAF50";   // Green
    private const string ItemColor = "#2196F3";        // Blue
    private const string CompletionColor = "#9C27B0";  // Purple

    /// <summary>
    /// All available achievements.
    /// </summary>
    public static IReadOnlyList<Achievement> All { get; } = CreateAchievements();

    /// <summary>
    /// Gets an achievement by ID.
    /// </summary>
    public static Achievement? GetById(string id) =>
        All.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Gets all achievements of a specific type.
    /// </summary>
    public static IReadOnlyList<Achievement> GetByType(AchievementType type) =>
        All.Where(a => a.Type == type).ToList();

    private static List<Achievement> CreateAchievements()
    {
        var achievements = new List<Achievement>();

        // ===== Streak Achievements =====
        achievements.AddRange(CreateStreakAchievements());

        // ===== Milestone Achievements =====
        achievements.AddRange(CreateMilestoneAchievements());

        // ===== Completion Achievements =====
        achievements.AddRange(CreateCompletionAchievements());

        // ===== Item-Specific Achievements =====
        achievements.AddRange(CreateItemAchievements());

        return achievements;
    }

    private static IEnumerable<Achievement> CreateStreakAchievements()
    {
        return new[]
        {
            new Achievement
            {
                Id = "streak_7",
                NameKey = "Achievement_Streak7_Name",
                DescriptionKey = "Achievement_Streak7_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1", // Fire
                BadgeColor = StreakColor,
                TargetValue = 7
            },
            new Achievement
            {
                Id = "streak_14",
                NameKey = "Achievement_Streak14_Name",
                DescriptionKey = "Achievement_Streak14_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1",
                BadgeColor = StreakColor,
                TargetValue = 14
            },
            new Achievement
            {
                Id = "streak_30",
                NameKey = "Achievement_Streak30_Name",
                DescriptionKey = "Achievement_Streak30_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1",
                BadgeColor = StreakColor,
                TargetValue = 30
            },
            new Achievement
            {
                Id = "streak_60",
                NameKey = "Achievement_Streak60_Name",
                DescriptionKey = "Achievement_Streak60_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1",
                BadgeColor = StreakColor,
                TargetValue = 60
            },
            new Achievement
            {
                Id = "streak_100",
                NameKey = "Achievement_Streak100_Name",
                DescriptionKey = "Achievement_Streak100_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1",
                BadgeColor = StreakColor,
                TargetValue = 100
            },
            new Achievement
            {
                Id = "streak_365",
                NameKey = "Achievement_Streak365_Name",
                DescriptionKey = "Achievement_Streak365_Desc",
                Type = AchievementType.Streak,
                IconGlyph = "\uE7C1",
                BadgeColor = StreakColor,
                TargetValue = 365
            }
        };
    }

    private static IEnumerable<Achievement> CreateMilestoneAchievements()
    {
        return new[]
        {
            new Achievement
            {
                Id = "milestone_first_day",
                NameKey = "Achievement_FirstDay_Name",
                DescriptionKey = "Achievement_FirstDay_Desc",
                Type = AchievementType.Milestone,
                IconGlyph = "\uE8E1", // Star
                BadgeColor = MilestoneColor,
                TargetValue = 1
            },
            new Achievement
            {
                Id = "milestone_first_perfect",
                NameKey = "Achievement_FirstPerfect_Name",
                DescriptionKey = "Achievement_FirstPerfect_Desc",
                Type = AchievementType.Milestone,
                IconGlyph = "\uE735", // Trophy
                BadgeColor = MilestoneColor,
                TargetValue = 1
            },
            new Achievement
            {
                Id = "milestone_first_week",
                NameKey = "Achievement_FirstWeek_Name",
                DescriptionKey = "Achievement_FirstWeek_Desc",
                Type = AchievementType.Milestone,
                IconGlyph = "\uE787", // Calendar
                BadgeColor = MilestoneColor,
                TargetValue = 7
            },
            new Achievement
            {
                Id = "milestone_first_month",
                NameKey = "Achievement_FirstMonth_Name",
                DescriptionKey = "Achievement_FirstMonth_Desc",
                Type = AchievementType.Milestone,
                IconGlyph = "\uE787",
                BadgeColor = MilestoneColor,
                TargetValue = 30
            }
        };
    }

    private static IEnumerable<Achievement> CreateCompletionAchievements()
    {
        return new[]
        {
            new Achievement
            {
                Id = "completion_10",
                NameKey = "Achievement_Completion10_Name",
                DescriptionKey = "Achievement_Completion10_Desc",
                Type = AchievementType.Completion,
                IconGlyph = "\uE73E", // Checkmark
                BadgeColor = CompletionColor,
                TargetValue = 10
            },
            new Achievement
            {
                Id = "completion_25",
                NameKey = "Achievement_Completion25_Name",
                DescriptionKey = "Achievement_Completion25_Desc",
                Type = AchievementType.Completion,
                IconGlyph = "\uE73E",
                BadgeColor = CompletionColor,
                TargetValue = 25
            },
            new Achievement
            {
                Id = "completion_50",
                NameKey = "Achievement_Completion50_Name",
                DescriptionKey = "Achievement_Completion50_Desc",
                Type = AchievementType.Completion,
                IconGlyph = "\uE73E",
                BadgeColor = CompletionColor,
                TargetValue = 50
            },
            new Achievement
            {
                Id = "completion_100",
                NameKey = "Achievement_Completion100_Name",
                DescriptionKey = "Achievement_Completion100_Desc",
                Type = AchievementType.Completion,
                IconGlyph = "\uE73E",
                BadgeColor = CompletionColor,
                TargetValue = 100
            },
            new Achievement
            {
                Id = "completion_365",
                NameKey = "Achievement_Completion365_Name",
                DescriptionKey = "Achievement_Completion365_Desc",
                Type = AchievementType.Completion,
                IconGlyph = "\uE73E",
                BadgeColor = CompletionColor,
                TargetValue = 365
            }
        };
    }

    private static IEnumerable<Achievement> CreateItemAchievements()
    {
        // Create achievements for key Daily Dozen items
        var itemsWithAchievements = new[]
        {
            ("beans", "\uE707", "Beans"),           // Leaf icon
            ("berries", "\uE707", "Berries"),
            ("greens", "\uE707", "Greens"),
            ("cruciferous", "\uE707", "Cruciferous"),
            ("whole_grains", "\uE707", "WholeGrains"),
            ("exercise", "\uE823", "Exercise"),     // Running icon
            ("flaxseed", "\uE707", "Flaxseed"),
            ("nuts", "\uE707", "Nuts"),
        };

        var achievements = new List<Achievement>();

        foreach (var (itemId, glyph, nameBase) in itemsWithAchievements)
        {
            // 50 times achievement
            achievements.Add(new Achievement
            {
                Id = $"item_{itemId}_50",
                NameKey = $"Achievement_{nameBase}50_Name",
                DescriptionKey = $"Achievement_{nameBase}50_Desc",
                Type = AchievementType.ItemSpecific,
                IconGlyph = glyph,
                BadgeColor = ItemColor,
                TargetValue = 50,
                ItemId = itemId
            });

            // 100 times achievement
            achievements.Add(new Achievement
            {
                Id = $"item_{itemId}_100",
                NameKey = $"Achievement_{nameBase}100_Name",
                DescriptionKey = $"Achievement_{nameBase}100_Desc",
                Type = AchievementType.ItemSpecific,
                IconGlyph = glyph,
                BadgeColor = ItemColor,
                TargetValue = 100,
                ItemId = itemId
            });
        }

        return achievements;
    }
}
