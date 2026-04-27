namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// In-memory implementation of <see cref="IDataService"/> for VM and service tests where
/// Moq's verbose setup would obscure intent. Mirrors the persistence semantics of
/// <see cref="SqliteDataService"/>: upsert by (Date, ItemId) for daily entries and by
/// Date for weight entries; achievements are insert-or-ignore by AchievementId.
/// </summary>
internal sealed class InMemoryDataService : IDataService
{
    private readonly IAppPreferences _preferences;
    private readonly List<DailyEntry> _dailyEntries = new();
    private readonly List<WeightEntry> _weightEntries = new();
    private readonly List<EarnedAchievement> _earnedAchievements = new();
    private int _nextDailyId = 1;
    private int _nextWeightId = 1;
    private int _nextEarnedId = 1;

    public InMemoryDataService(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // ===== Daily Entries =====

    public Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId) =>
        Task.FromResult<DailyEntry?>(_dailyEntries.FirstOrDefault(e => e.Date == date && e.ItemId == itemId));

    public Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date) =>
        Task.FromResult<IReadOnlyList<DailyEntry>>(_dailyEntries.Where(e => e.Date == date).ToList());

    public Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate) =>
        Task.FromResult<IReadOnlyList<DailyEntry>>(_dailyEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToList());

    public Task SaveEntryAsync(DailyEntry entry)
    {
        var existing = _dailyEntries.FirstOrDefault(e => e.Date == entry.Date && e.ItemId == entry.ItemId);
        if (existing is null)
        {
            _dailyEntries.Add(new DailyEntry
            {
                Id = _nextDailyId++,
                Date = entry.Date,
                ItemId = entry.ItemId,
                ServingsCompleted = entry.ServingsCompleted
            });
        }
        else
        {
            existing.ServingsCompleted = entry.ServingsCompleted;
        }
        return Task.CompletedTask;
    }

    public Task DeleteEntriesForDateAsync(DateOnly date)
    {
        _dailyEntries.RemoveAll(e => e.Date == date);
        return Task.CompletedTask;
    }

    // ===== Weight Entries =====

    public Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync() =>
        Task.FromResult<IReadOnlyList<WeightEntry>>(_weightEntries.OrderBy(e => e.Date).ToList());

    public Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate) =>
        Task.FromResult<IReadOnlyList<WeightEntry>>(_weightEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToList());

    public Task<WeightEntry?> GetWeightEntryAsync(DateOnly date) =>
        Task.FromResult<WeightEntry?>(_weightEntries.FirstOrDefault(e => e.Date == date));

    public Task SaveWeightEntryAsync(WeightEntry entry)
    {
        var existing = _weightEntries.FirstOrDefault(e => e.Date == entry.Date);
        if (existing is null)
        {
            _weightEntries.Add(new WeightEntry
            {
                Id = _nextWeightId++,
                Date = entry.Date,
                Weight = entry.Weight,
                Notes = entry.Notes
            });
        }
        else
        {
            existing.Weight = entry.Weight;
            existing.Notes = entry.Notes;
        }
        return Task.CompletedTask;
    }

    public Task DeleteWeightEntryAsync(DateOnly date)
    {
        _weightEntries.RemoveAll(e => e.Date == date);
        return Task.CompletedTask;
    }

    // ===== Statistics =====

    public Task<int> GetCurrentStreakAsync()
    {
        var requiredServings = ChecklistDefinitions.GetRequiredServingsMap(_preferences);
        if (requiredServings.Count == 0) return Task.FromResult(0);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var entriesByDate = _dailyEntries
            .Where(e => e.Date >= today.AddDays(-365) && e.Date <= today)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());

        var streak = 0;
        var currentDate = today;

        while (true)
        {
            if (IsDateComplete(entriesByDate.GetValueOrDefault(currentDate), requiredServings))
            {
                streak++;
                currentDate = currentDate.AddDays(-1);
            }
            else if (currentDate == today)
            {
                currentDate = currentDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return Task.FromResult(streak);
    }

    public Task<int> GetLongestStreakAsync()
    {
        var requiredServings = ChecklistDefinitions.GetRequiredServingsMap(_preferences);
        if (requiredServings.Count == 0) return Task.FromResult(0);
        if (_dailyEntries.Count == 0) return Task.FromResult(0);

        var entriesByDate = _dailyEntries
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());

        var dates = entriesByDate.Keys.OrderBy(d => d).ToList();

        var longestStreak = 0;
        var currentStreak = 0;
        DateOnly? previousDate = null;

        foreach (var date in dates)
        {
            if (IsDateComplete(entriesByDate[date], requiredServings))
            {
                if (previousDate.HasValue && date.DayNumber - previousDate.Value.DayNumber == 1)
                {
                    currentStreak++;
                }
                else
                {
                    currentStreak = 1;
                }

                longestStreak = Math.Max(longestStreak, currentStreak);
                previousDate = date;
            }
            else
            {
                currentStreak = 0;
                previousDate = null;
            }
        }

        return Task.FromResult(longestStreak);
    }

    public Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync() =>
        Task.FromResult<IReadOnlyList<DateOnly>>(
            _dailyEntries.Select(e => e.Date).Distinct().OrderBy(d => d).ToList());

    // ===== Achievements =====

    public Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync() =>
        Task.FromResult<IReadOnlyList<EarnedAchievement>>(
            _earnedAchievements.OrderByDescending(e => e.EarnedAt).ToList());

    public Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        if (_earnedAchievements.Any(e => e.AchievementId == achievement.AchievementId))
        {
            return Task.CompletedTask;
        }
        _earnedAchievements.Add(new EarnedAchievement
        {
            Id = _nextEarnedId++,
            AchievementId = achievement.AchievementId,
            EarnedAt = achievement.EarnedAt,
            HasBeenSeen = achievement.HasBeenSeen
        });
        return Task.CompletedTask;
    }

    public Task<bool> IsAchievementEarnedAsync(string achievementId) =>
        Task.FromResult(_earnedAchievements.Any(e => e.AchievementId == achievementId));

    public Task<int> GetUnseenAchievementCountAsync() =>
        Task.FromResult(_earnedAchievements.Count(e => !e.HasBeenSeen));

    public Task MarkAllAchievementsAsSeenAsync()
    {
        foreach (var achievement in _earnedAchievements)
        {
            achievement.HasBeenSeen = true;
        }
        return Task.CompletedTask;
    }

    public Task<int> GetPerfectDaysCountAsync()
    {
        var requiredServings = ChecklistDefinitions.GetRequiredServingsMap(_preferences);
        if (requiredServings.Count == 0) return Task.FromResult(0);

        var entriesByDate = _dailyEntries
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());

        return Task.FromResult(entriesByDate.Count(kvp => IsDateComplete(kvp.Value, requiredServings)));
    }

    public Task<int> GetItemCompletionCountAsync(string itemId)
    {
        var item = ChecklistDefinitions.GetItemById(itemId);
        if (item == null) return Task.FromResult(0);
        return Task.FromResult(_dailyEntries.Count(e => e.ItemId == itemId && e.ServingsCompleted >= item.RecommendedServings));
    }

    public Task<int> GetTotalDaysTrackedAsync() =>
        Task.FromResult(_dailyEntries.Select(e => e.Date).Distinct().Count());

    private static bool IsDateComplete(IReadOnlyList<DailyEntry>? entries, Dictionary<string, int> requiredServings)
    {
        if (entries == null || entries.Count == 0) return false;

        foreach (var (itemId, required) in requiredServings)
        {
            var entry = entries.FirstOrDefault(e => e.ItemId == itemId);
            if (entry == null || entry.ServingsCompleted < required)
            {
                return false;
            }
        }

        return true;
    }
}
