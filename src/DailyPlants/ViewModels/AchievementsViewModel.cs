using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Achievements page.
/// </summary>
public partial class AchievementsViewModel : ObservableObject
{
    private readonly IAchievementService _achievementService;
    private readonly IAppPreferences _appPreferences;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _earnedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _tallyText = "0 of 0";

    public ObservableCollection<AchievementGroupViewModel> AchievementGroups { get; } = [];

    public AchievementsViewModel(IAchievementService achievementService, IAppPreferences appPreferences)
    {
        _achievementService = achievementService;
        _appPreferences = appPreferences;
    }

    public async Task LoadAchievementsAsync()
    {
        IsLoading = true;

        try
        {
            var enabledItemIds = ChecklistDefinitions.GetEnabledItemIds(_appPreferences).ToHashSet();
            var allAchievements = _achievementService.GetAllAchievements()
                .Where(a => a.Type != AchievementType.ItemSpecific
                    || (a.ItemId != null && enabledItemIds.Contains(a.ItemId)))
                .ToList();
            var earnedAchievements = await _achievementService.GetEarnedAchievementsAsync();
            var earnedIds = earnedAchievements.Select(e => e.AchievementId).ToHashSet();

            // Mark all as seen when page is loaded
            await _achievementService.MarkAllAsSeenAsync();

            TotalCount = allAchievements.Count;
            EarnedCount = earnedIds.Count;
            TallyText = $"{EarnedCount} of {TotalCount}";

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
                    TypeName = GetTypeName(group.Key)
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
                        EarnedDateText = earnedAt?.ToLocalTime().ToString("d MMMM"),
                        Progress = progress,
                        ProgressText = $"{currentValue} of {achievement.TargetValue}",
                        IconUri = !string.IsNullOrEmpty(achievement.IconPath) ? new Uri(achievement.IconPath) : null,
                        BadgeColor = achievement.BadgeColor
                    });
                }

                var earnedInGroup = groupVm.Achievements.Count(a => a.IsEarned);
                groupVm.TallyText = $"{earnedInGroup} of {groupVm.Achievements.Count}";

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
}

/// <summary>
/// ViewModel for a group of achievements by type.
/// </summary>
public class AchievementGroupViewModel
{
    public AchievementType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string TallyText { get; set; } = "0 of 0";
    public ObservableCollection<AchievementViewModel> Achievements { get; } = [];
}

/// <summary>
/// ViewModel for a single achievement tile.
/// </summary>
public class AchievementViewModel
{
    public required Achievement Achievement { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEarned { get; set; }
    public bool IsLocked => !IsEarned;
    public DateTime? EarnedAt { get; set; }
    public string? EarnedDateText { get; set; }
    public double Progress { get; set; }
    public string ProgressText { get; set; } = "0 of 0";
    public Uri? IconUri { get; set; }
    public string BadgeColor { get; set; } = "#888888";
    public string BadgeBackground => IsEarned ? BadgeColor : "#9E9E9E";
    public bool ShowAsMonochrome => !IsEarned;
    public double BadgeOpacity => IsEarned ? 1.0 : 0.45;

    // Not localized: this ViewModel doesn't own the .resw files.
    // Reuses EarnedDateText so the announcement matches what is on screen -- formatting
    // EarnedAt again risks a different format, a UTC/local mismatch, or "earned " with
    // nothing after it when the date is missing.
    public string AutomationName => IsEarned
        ? string.IsNullOrWhiteSpace(EarnedDateText) ? Name : $"{Name}, earned {EarnedDateText}"
        : $"{Name}, locked, {ProgressText}";
}
