using DailyDozen.Helpers;
using DailyDozen.Models;
using DailyDozen.Services;

namespace DailyDozen.ViewModels;

/// <summary>
/// ViewModel for the Achievements page.
/// </summary>
public partial class AchievementsViewModel : ObservableObject
{
    private readonly IAchievementService _achievementService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _earnedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _progressText = "0 / 0";

    public ObservableCollection<AchievementGroupViewModel> AchievementGroups { get; } = [];

    public AchievementsViewModel(IAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    public async Task LoadAchievementsAsync()
    {
        IsLoading = true;

        try
        {
            var allAchievements = _achievementService.GetAllAchievements();
            var earnedAchievements = await _achievementService.GetEarnedAchievementsAsync();
            var earnedIds = earnedAchievements.Select(e => e.AchievementId).ToHashSet();

            // Mark all as seen when page is loaded
            await _achievementService.MarkAllAsSeenAsync();

            TotalCount = allAchievements.Count;
            EarnedCount = earnedIds.Count;
            ProgressText = $"{EarnedCount} / {TotalCount}";

            AchievementGroups.Clear();

            // Group by type
            var groups = allAchievements
                .GroupBy(a => a.Type)
                .OrderBy(g => GetTypeOrder(g.Key));

            foreach (var group in groups)
            {
                var groupVm = new AchievementGroupViewModel
                {
                    Type = group.Key,
                    TypeName = GetTypeName(group.Key),
                    TypeIcon = GetTypeIcon(group.Key)
                };

                foreach (var achievement in group.OrderBy(a => a.TargetValue))
                {
                    var isEarned = earnedIds.Contains(achievement.Id);
                    var earnedAt = earnedAchievements.FirstOrDefault(e => e.AchievementId == achievement.Id)?.EarnedAt;
                    var progress = await _achievementService.GetProgressAsync(achievement.Id);
                    var currentValue = await _achievementService.GetCurrentValueAsync(achievement.Id);

                    groupVm.Achievements.Add(new AchievementViewModel
                    {
                        Achievement = achievement,
                        Name = Localizer.GetString(achievement.NameKey),
                        Description = Localizer.GetString(achievement.DescriptionKey),
                        IsEarned = isEarned,
                        EarnedAt = earnedAt,
                        EarnedAtText = earnedAt.HasValue ? earnedAt.Value.ToLocalTime().ToString("d") : null,
                        Progress = progress,
                        ProgressText = $"{currentValue} / {achievement.TargetValue}",
                        IconGlyph = achievement.IconGlyph,
                        BadgeColor = achievement.BadgeColor
                    });
                }

                AchievementGroups.Add(groupVm);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int GetTypeOrder(AchievementType type) => type switch
    {
        AchievementType.Milestone => 0,
        AchievementType.Streak => 1,
        AchievementType.Completion => 2,
        AchievementType.ItemSpecific => 3,
        _ => 99
    };

    private static string GetTypeName(AchievementType type) => type switch
    {
        AchievementType.Milestone => Localizer.GetString("Achievement_Type_Milestone"),
        AchievementType.Streak => Localizer.GetString("Achievement_Type_Streak"),
        AchievementType.Completion => Localizer.GetString("Achievement_Type_Completion"),
        AchievementType.ItemSpecific => Localizer.GetString("Achievement_Type_ItemSpecific"),
        _ => type.ToString()
    };

    private static string GetTypeIcon(AchievementType type) => type switch
    {
        AchievementType.Milestone => "\uE8E1",  // Star
        AchievementType.Streak => "\uE7C1",     // Fire
        AchievementType.Completion => "\uE73E", // Checkmark
        AchievementType.ItemSpecific => "\uE707", // Leaf
        _ => "\uE8E1"
    };
}

/// <summary>
/// ViewModel for a group of achievements by type.
/// </summary>
public class AchievementGroupViewModel
{
    public AchievementType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = string.Empty;
    public ObservableCollection<AchievementViewModel> Achievements { get; } = [];
}

/// <summary>
/// ViewModel for a single achievement.
/// </summary>
public class AchievementViewModel
{
    public required Achievement Achievement { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEarned { get; set; }
    public DateTime? EarnedAt { get; set; }
    public string? EarnedAtText { get; set; }
    public double Progress { get; set; }
    public string ProgressText { get; set; } = "0 / 0";
    public string IconGlyph { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "#888888";

    public double Opacity => IsEarned ? 1.0 : 0.5;
    public bool ShowProgress => !IsEarned;
}
