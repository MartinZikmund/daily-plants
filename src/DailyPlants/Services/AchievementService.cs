using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Service for managing achievements.
/// </summary>
public class AchievementService : IAchievementService
{
    private readonly IDataService _dataService;
    private HashSet<string> _earnedAchievementIds = new();
    private bool _initialized;

    public event EventHandler<Achievement>? AchievementEarned;

    public AchievementService(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var earned = await _dataService.GetEarnedAchievementsAsync();
        _earnedAchievementIds = earned.Select(e => e.AchievementId).ToHashSet();
        _initialized = true;
    }

    public IReadOnlyList<Achievement> GetAllAchievements() => AchievementDefinitions.All;

    public async Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync()
    {
        await EnsureInitializedAsync();
        return await _dataService.GetEarnedAchievementsAsync();
    }

    public async Task<bool> IsAchievementEarnedAsync(string achievementId)
    {
        await EnsureInitializedAsync();
        return _earnedAchievementIds.Contains(achievementId);
    }

    public async Task<int> GetUnseenCountAsync()
    {
        await EnsureInitializedAsync();
        return await _dataService.GetUnseenAchievementCountAsync();
    }

    public async Task MarkAllAsSeenAsync()
    {
        await EnsureInitializedAsync();
        await _dataService.MarkAllAchievementsAsSeenAsync();
    }

    public async Task CheckAndAwardAchievementsAsync()
    {
        await EnsureInitializedAsync();

        var newlyEarned = new List<Achievement>();

        // Check streak achievements
        var currentStreak = await _dataService.GetCurrentStreakAsync();
        var longestStreak = await _dataService.GetLongestStreakAsync();
        var bestStreak = Math.Max(currentStreak, longestStreak);

        foreach (var achievement in AchievementDefinitions.GetByType(AchievementType.Streak))
        {
            if (!_earnedAchievementIds.Contains(achievement.Id) && bestStreak >= achievement.TargetValue)
            {
                await AwardAchievementAsync(achievement);
                newlyEarned.Add(achievement);
            }
        }

        // Check milestone achievements
        var totalDays = await _dataService.GetTotalDaysTrackedAsync();
        var perfectDays = await _dataService.GetPerfectDaysCountAsync();

        foreach (var achievement in AchievementDefinitions.GetByType(AchievementType.Milestone))
        {
            if (_earnedAchievementIds.Contains(achievement.Id)) continue;

            var shouldAward = achievement.Id switch
            {
                "milestone_first_day" => totalDays >= 1,
                "milestone_first_perfect" => perfectDays >= 1,
                "milestone_first_week" => totalDays >= 7,
                "milestone_first_month" => totalDays >= 30,
                _ => false
            };

            if (shouldAward)
            {
                await AwardAchievementAsync(achievement);
                newlyEarned.Add(achievement);
            }
        }

        // Check completion achievements
        foreach (var achievement in AchievementDefinitions.GetByType(AchievementType.Completion))
        {
            if (!_earnedAchievementIds.Contains(achievement.Id) && perfectDays >= achievement.TargetValue)
            {
                await AwardAchievementAsync(achievement);
                newlyEarned.Add(achievement);
            }
        }

        // Check item-specific achievements
        foreach (var achievement in AchievementDefinitions.GetByType(AchievementType.ItemSpecific))
        {
            if (_earnedAchievementIds.Contains(achievement.Id) || string.IsNullOrEmpty(achievement.ItemId))
                continue;

            var completionCount = await _dataService.GetItemCompletionCountAsync(achievement.ItemId);
            if (completionCount >= achievement.TargetValue)
            {
                await AwardAchievementAsync(achievement);
                newlyEarned.Add(achievement);
            }
        }

        // Raise events for newly earned achievements
        foreach (var achievement in newlyEarned)
        {
            AchievementEarned?.Invoke(this, achievement);
        }
    }

    public async Task<double> GetProgressAsync(string achievementId)
    {
        await EnsureInitializedAsync();

        var achievement = AchievementDefinitions.GetById(achievementId);
        if (achievement == null || achievement.TargetValue == 0) return 0;

        if (_earnedAchievementIds.Contains(achievementId)) return 1.0;

        var currentValue = await GetCurrentValueAsync(achievementId);
        return Math.Min(1.0, (double)currentValue / achievement.TargetValue);
    }

    public async Task<int> GetCurrentValueAsync(string achievementId)
    {
        await EnsureInitializedAsync();

        var achievement = AchievementDefinitions.GetById(achievementId);
        if (achievement == null) return 0;

        return achievement.Type switch
        {
            AchievementType.Streak => Math.Max(
                await _dataService.GetCurrentStreakAsync(),
                await _dataService.GetLongestStreakAsync()),
            AchievementType.Completion => await _dataService.GetPerfectDaysCountAsync(),
            AchievementType.Milestone => achievement.Id switch
            {
                "milestone_first_day" or "milestone_first_week" or "milestone_first_month"
                    => await _dataService.GetTotalDaysTrackedAsync(),
                "milestone_first_perfect" => await _dataService.GetPerfectDaysCountAsync(),
                _ => 0
            },
            AchievementType.ItemSpecific when !string.IsNullOrEmpty(achievement.ItemId)
                => await _dataService.GetItemCompletionCountAsync(achievement.ItemId),
            _ => 0
        };
    }

    private async Task AwardAchievementAsync(Achievement achievement)
    {
        var earned = new EarnedAchievement
        {
            AchievementId = achievement.Id,
            EarnedAt = DateTime.UtcNow,
            HasBeenSeen = false
        };

        await _dataService.SaveEarnedAchievementAsync(earned);
        _earnedAchievementIds.Add(achievement.Id);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }
}
