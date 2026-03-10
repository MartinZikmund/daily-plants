using DailyPlants.Models;
using DailyPlants.Services.Entities;
using DailyPlants.Services.Settings;
using SQLite;

namespace DailyPlants.Services;

/// <summary>
/// SQLite implementation of IDataService using sqlite-net ORM with async connection.
/// </summary>
public class SqliteDataService : IDataService
{
    private const int CurrentSchemaVersion = 1;

    private readonly SQLiteAsyncConnection _connection;
    private readonly IAppPreferences _appPreferences;
    private bool _initialized;

    public SqliteDataService(IAppPreferences appPreferences)
    {
        _appPreferences = appPreferences;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "DailyPlants", "dailyplants.db");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _connection.CreateTableAsync<DailyEntryEntity>();
        await _connection.CreateTableAsync<WeightEntryEntity>();
        await _connection.CreateTableAsync<EarnedAchievementEntity>();

        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_daily_entries_date_item ON DailyEntries (Date, ItemId)");

        await RunMigrationsAsync();

        _initialized = true;
    }

    private async Task RunMigrationsAsync()
    {
        var version = await _connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        // Run migrations sequentially from current version to latest
        if (version < 1)
        {
            // v1: Initial schema - tables already created above via CreateTableAsync
            await _connection.ExecuteAsync("PRAGMA user_version = 1");
        }

        // Future migrations go here:
        // if (version < 2) { /* migration to v2 */ await _connection.ExecuteAsync("PRAGMA user_version = 2"); }
    }

    // ===== Daily Entries =====

    public async Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId)
    {
        await EnsureInitializedAsync();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entities = await _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr && e.ItemId == itemId)
            .ToListAsync();

        var entity = entities.FirstOrDefault();
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entities = await _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();

        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");
        var entities = await _connection.QueryAsync<DailyEntryEntity>(
            "SELECT * FROM DailyEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        return entities.Select(ToModel).ToList();
    }

    public async Task SaveEntryAsync(DailyEntry entry)
    {
        await EnsureInitializedAsync();

        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        await _connection.ExecuteAsync(
            "INSERT INTO DailyEntries (Date, ItemId, ServingsCompleted) VALUES (?, ?, ?) ON CONFLICT(Date, ItemId) DO UPDATE SET ServingsCompleted = ?",
            dateStr, entry.ItemId, entry.ServingsCompleted, entry.ServingsCompleted);
    }

    public async Task DeleteEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = date.ToString("yyyy-MM-dd");
        await _connection.ExecuteAsync("DELETE FROM DailyEntries WHERE Date = ?", dateStr);
    }

    // ===== Weight Entries =====

    public async Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync()
    {
        await EnsureInitializedAsync();

        var entities = await _connection.Table<WeightEntryEntity>()
            .OrderBy(e => e.Date)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();

        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");
        var entities = await _connection.QueryAsync<WeightEntryEntity>(
            "SELECT * FROM WeightEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        return entities.Select(ToModel).ToList();
    }

    public async Task<WeightEntry?> GetWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entities = await _connection.Table<WeightEntryEntity>()
            .Where(e => e.Date == dateStr)
            .ToListAsync();

        var entity = entities.FirstOrDefault();
        return entity is null ? null : ToModel(entity);
    }

    public async Task SaveWeightEntryAsync(WeightEntry entry)
    {
        await EnsureInitializedAsync();

        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        await _connection.ExecuteAsync(
            "INSERT INTO WeightEntries (Date, Weight, Notes) VALUES (?, ?, ?) ON CONFLICT(Date) DO UPDATE SET Weight = ?, Notes = ?",
            dateStr, entry.Weight, entry.Notes, entry.Weight, entry.Notes);
    }

    public async Task DeleteWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = date.ToString("yyyy-MM-dd");
        await _connection.ExecuteAsync("DELETE FROM WeightEntries WHERE Date = ?", dateStr);
    }

    // ===== Statistics =====

    public async Task<int> GetCurrentStreakAsync()
    {
        await EnsureInitializedAsync();

        var enabledItems = GetEnabledItemIds();
        if (enabledItems.Count == 0) return 0;

        // Build the required servings map for enabled items
        var requiredServings = new Dictionary<string, int>();
        foreach (var itemId in enabledItems)
        {
            var item = ChecklistDefinitions.GetItemById(itemId);
            if (item != null)
            {
                requiredServings[itemId] = item.RecommendedServings;
            }
        }

        if (requiredServings.Count == 0) return 0;

        // Load all entries from recent history (enough for reasonable streak)
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lookbackStart = today.AddDays(-365);
        var allEntries = await GetEntriesInRangeAsync(lookbackStart, today);

        // Group entries by date
        var entriesByDate = allEntries.GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

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
                // Today not complete yet, check yesterday
                currentDate = currentDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    public async Task<int> GetLongestStreakAsync()
    {
        await EnsureInitializedAsync();

        var enabledItems = GetEnabledItemIds();
        if (enabledItems.Count == 0) return 0;

        var requiredServings = new Dictionary<string, int>();
        foreach (var itemId in enabledItems)
        {
            var item = ChecklistDefinitions.GetItemById(itemId);
            if (item != null)
            {
                requiredServings[itemId] = item.RecommendedServings;
            }
        }

        if (requiredServings.Count == 0) return 0;

        // Load ALL entries in a single query
        var allEntries = await _connection.QueryAsync<DailyEntryEntity>(
            "SELECT * FROM DailyEntries ORDER BY Date");

        if (allEntries.Count == 0) return 0;

        // Group by date
        var entriesByDate = allEntries.Select(ToModel)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

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

        return longestStreak;
    }

    public async Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync()
    {
        await EnsureInitializedAsync();

        var dates = await _connection.QueryScalarsAsync<string>(
            "SELECT DISTINCT Date FROM DailyEntries ORDER BY Date");
        return dates.Select(d => DateOnly.Parse(d)).ToList();
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private List<string> GetEnabledItemIds()
    {
        var itemIds = new List<string>();

        if (_appPreferences.DailyDozenEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen).Select(i => i.Id));
        }

        if (_appPreferences.TwentyOneTweaksEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks).Select(i => i.Id));
        }

        if (_appPreferences.AntiAgingEightEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.AntiAgingEight).Select(i => i.Id));
        }

        return itemIds.Distinct().ToList();
    }

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

    // ===== Achievements =====

    public async Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync()
    {
        await EnsureInitializedAsync();

        var entities = await _connection.Table<EarnedAchievementEntity>()
            .OrderByDescending(e => e.EarnedAt)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        await EnsureInitializedAsync();

        await _connection.ExecuteAsync(
            "INSERT OR IGNORE INTO EarnedAchievements (AchievementId, EarnedAt, HasBeenSeen) VALUES (?, ?, ?)",
            achievement.AchievementId, achievement.EarnedAt.ToString("O"), achievement.HasBeenSeen ? 1 : 0);
    }

    public async Task<bool> IsAchievementEarnedAsync(string achievementId)
    {
        await EnsureInitializedAsync();

        var count = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EarnedAchievements WHERE AchievementId = ?", achievementId);

        return count > 0;
    }

    public async Task<int> GetUnseenAchievementCountAsync()
    {
        await EnsureInitializedAsync();

        return await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EarnedAchievements WHERE HasBeenSeen = 0");
    }

    public async Task MarkAllAchievementsAsSeenAsync()
    {
        await EnsureInitializedAsync();

        await _connection.ExecuteAsync("UPDATE EarnedAchievements SET HasBeenSeen = 1 WHERE HasBeenSeen = 0");
    }

    public async Task<int> GetPerfectDaysCountAsync()
    {
        await EnsureInitializedAsync();

        var enabledItems = GetEnabledItemIds();
        if (enabledItems.Count == 0) return 0;

        var requiredServings = new Dictionary<string, int>();
        foreach (var itemId in enabledItems)
        {
            var item = ChecklistDefinitions.GetItemById(itemId);
            if (item != null)
            {
                requiredServings[itemId] = item.RecommendedServings;
            }
        }

        if (requiredServings.Count == 0) return 0;

        // Load ALL entries in a single query
        var allEntries = await _connection.QueryAsync<DailyEntryEntity>(
            "SELECT * FROM DailyEntries ORDER BY Date");

        if (allEntries.Count == 0) return 0;

        // Group by date and count perfect days in memory
        var entriesByDate = allEntries.Select(ToModel)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());

        var perfectDays = 0;
        foreach (var (_, entries) in entriesByDate)
        {
            if (IsDateComplete(entries, requiredServings))
            {
                perfectDays++;
            }
        }

        return perfectDays;
    }

    public async Task<int> GetItemCompletionCountAsync(string itemId)
    {
        await EnsureInitializedAsync();

        var item = ChecklistDefinitions.GetItemById(itemId);
        if (item == null) return 0;

        return await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM DailyEntries WHERE ItemId = ? AND ServingsCompleted >= ?",
            itemId, item.RecommendedServings);
    }

    public async Task<int> GetTotalDaysTrackedAsync()
    {
        await EnsureInitializedAsync();

        return await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT Date) FROM DailyEntries");
    }

    // ===== Mapping helpers =====

    private static DailyEntry ToModel(DailyEntryEntity entity) => new()
    {
        Id = entity.Id,
        Date = DateOnly.Parse(entity.Date),
        ItemId = entity.ItemId,
        ServingsCompleted = entity.ServingsCompleted
    };

    private static WeightEntry ToModel(WeightEntryEntity entity) => new()
    {
        Id = entity.Id,
        Date = DateOnly.Parse(entity.Date),
        Weight = entity.Weight,
        Notes = entity.Notes
    };

    private static EarnedAchievement ToModel(EarnedAchievementEntity entity) => new()
    {
        Id = entity.Id,
        AchievementId = entity.AchievementId,
        EarnedAt = DateTime.Parse(entity.EarnedAt),
        HasBeenSeen = entity.HasBeenSeen == 1
    };
}
