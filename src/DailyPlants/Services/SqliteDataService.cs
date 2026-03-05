using System.Text.Json;
using DailyPlants.Models;
using DailyPlants.Services.Entities;
using SQLite;

namespace DailyPlants.Services;

/// <summary>
/// SQLite implementation of IDataService using sqlite-net ORM.
/// </summary>
public class SqliteDataService : IDataService, IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _initialized;
    private bool _disposed;

    public SqliteDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "DailyPlants", "dailyplants.db");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SQLiteConnection(dbPath);
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;

        _connection.CreateTable<DailyEntryEntity>();
        _connection.CreateTable<WeightEntryEntity>();
        _connection.CreateTable<UserSettingsEntity>();
        _connection.CreateTable<EarnedAchievementEntity>();

        _connection.CreateIndex("idx_daily_entries_date_item", "DailyEntries", ["Date", "ItemId"], unique: true);

        _initialized = true;
        return Task.CompletedTask;
    }

    // ===== Daily Entries =====

    public Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId)
    {
        EnsureInitialized();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entity = _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr && e.ItemId == itemId)
            .FirstOrDefault();

        return Task.FromResult(entity is null ? null : ToModel(entity));
    }

    public Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        EnsureInitialized();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entities = _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr)
            .ToList();

        IReadOnlyList<DailyEntry> result = entities.Select(ToModel).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        EnsureInitialized();

        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");
        var entities = _connection.Query<DailyEntryEntity>(
            "SELECT * FROM DailyEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        IReadOnlyList<DailyEntry> result = entities.Select(ToModel).ToList();
        return Task.FromResult(result);
    }

    public Task SaveEntryAsync(DailyEntry entry)
    {
        EnsureInitialized();

        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        _connection.Execute(
            "INSERT INTO DailyEntries (Date, ItemId, ServingsCompleted) VALUES (?, ?, ?) ON CONFLICT(Date, ItemId) DO UPDATE SET ServingsCompleted = ?",
            dateStr, entry.ItemId, entry.ServingsCompleted, entry.ServingsCompleted);

        return Task.CompletedTask;
    }

    public Task DeleteEntriesForDateAsync(DateOnly date)
    {
        EnsureInitialized();

        var dateStr = date.ToString("yyyy-MM-dd");
        _connection.Execute("DELETE FROM DailyEntries WHERE Date = ?", dateStr);

        return Task.CompletedTask;
    }

    // ===== Weight Entries =====

    public Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync()
    {
        EnsureInitialized();

        var entities = _connection.Table<WeightEntryEntity>()
            .OrderBy(e => e.Date)
            .ToList();

        IReadOnlyList<WeightEntry> result = entities.Select(ToModel).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        EnsureInitialized();

        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");
        var entities = _connection.Query<WeightEntryEntity>(
            "SELECT * FROM WeightEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        IReadOnlyList<WeightEntry> result = entities.Select(ToModel).ToList();
        return Task.FromResult(result);
    }

    public Task<WeightEntry?> GetWeightEntryAsync(DateOnly date)
    {
        EnsureInitialized();

        var dateStr = date.ToString("yyyy-MM-dd");
        var entity = _connection.Table<WeightEntryEntity>()
            .Where(e => e.Date == dateStr)
            .FirstOrDefault();

        return Task.FromResult(entity is null ? null : ToModel(entity));
    }

    public Task SaveWeightEntryAsync(WeightEntry entry)
    {
        EnsureInitialized();

        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        _connection.Execute(
            "INSERT INTO WeightEntries (Date, Weight, Notes) VALUES (?, ?, ?) ON CONFLICT(Date) DO UPDATE SET Weight = ?, Notes = ?",
            dateStr, entry.Weight, entry.Notes, entry.Weight, entry.Notes);

        return Task.CompletedTask;
    }

    public Task DeleteWeightEntryAsync(DateOnly date)
    {
        EnsureInitialized();

        var dateStr = date.ToString("yyyy-MM-dd");
        _connection.Execute("DELETE FROM WeightEntries WHERE Date = ?", dateStr);

        return Task.CompletedTask;
    }

    // ===== User Settings =====

    public Task<UserSettings> GetSettingsAsync()
    {
        EnsureInitialized();

        var entity = _connection.Find<UserSettingsEntity>(1);
        if (entity is not null)
        {
            return Task.FromResult(JsonSerializer.Deserialize<UserSettings>(entity.SettingsJson) ?? new UserSettings());
        }

        return Task.FromResult(new UserSettings());
    }

    public Task SaveSettingsAsync(UserSettings settings)
    {
        EnsureInitialized();

        var json = JsonSerializer.Serialize(settings);
        _connection.Execute(
            "INSERT INTO UserSettings (Id, SettingsJson) VALUES (1, ?) ON CONFLICT(Id) DO UPDATE SET SettingsJson = ?",
            json, json);

        return Task.CompletedTask;
    }

    // ===== Statistics =====

    public async Task<int> GetCurrentStreakAsync()
    {
        EnsureInitialized();

        var settings = await GetSettingsAsync();
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var streak = 0;
        var currentDate = today;

        while (true)
        {
            var entries = await GetEntriesForDateAsync(currentDate);
            if (IsDateComplete(entries, enabledItems))
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
        EnsureInitialized();

        var settings = await GetSettingsAsync();
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var datesWithEntries = await GetDatesWithEntriesAsync();
        if (datesWithEntries.Count == 0) return 0;

        var longestStreak = 0;
        var currentStreak = 0;
        DateOnly? previousDate = null;

        foreach (var date in datesWithEntries.OrderBy(d => d))
        {
            var entries = await GetEntriesForDateAsync(date);
            if (IsDateComplete(entries, enabledItems))
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

    public Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync()
    {
        EnsureInitialized();

        var dates = _connection.QueryScalars<string>("SELECT DISTINCT Date FROM DailyEntries ORDER BY Date");
        IReadOnlyList<DateOnly> result = dates.Select(d => DateOnly.Parse(d)).ToList();
        return Task.FromResult(result);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        _connection.CreateTable<DailyEntryEntity>();
        _connection.CreateTable<WeightEntryEntity>();
        _connection.CreateTable<UserSettingsEntity>();
        _connection.CreateTable<EarnedAchievementEntity>();

        _connection.CreateIndex("idx_daily_entries_date_item", "DailyEntries", ["Date", "ItemId"], unique: true);

        _initialized = true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }

    private static List<string> GetEnabledItemIds(UserSettings settings) =>
        ChecklistDefinitions.GetEnabledItemIds(settings);

    private static bool IsDateComplete(IReadOnlyList<DailyEntry> entries, List<string> enabledItems)
    {
        foreach (var itemId in enabledItems)
        {
            var item = ChecklistDefinitions.GetItemById(itemId);
            if (item == null) continue;

            var entry = entries.FirstOrDefault(e => e.ItemId == itemId);
            if (entry == null || entry.ServingsCompleted < item.RecommendedServings)
            {
                return false;
            }
        }

        return true;
    }

    // ===== Achievements =====

    public Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync()
    {
        EnsureInitialized();

        var entities = _connection.Table<EarnedAchievementEntity>()
            .OrderByDescending(e => e.EarnedAt)
            .ToList();

        IReadOnlyList<EarnedAchievement> result = entities.Select(ToModel).ToList();
        return Task.FromResult(result);
    }

    public Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        EnsureInitialized();

        _connection.Execute(
            "INSERT OR IGNORE INTO EarnedAchievements (AchievementId, EarnedAt, HasBeenSeen) VALUES (?, ?, ?)",
            achievement.AchievementId, achievement.EarnedAt.ToString("O"), achievement.HasBeenSeen ? 1 : 0);

        return Task.CompletedTask;
    }

    public Task<bool> IsAchievementEarnedAsync(string achievementId)
    {
        EnsureInitialized();

        var count = _connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM EarnedAchievements WHERE AchievementId = ?", achievementId);

        return Task.FromResult(count > 0);
    }

    public Task<int> GetUnseenAchievementCountAsync()
    {
        EnsureInitialized();

        var count = _connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM EarnedAchievements WHERE HasBeenSeen = 0");

        return Task.FromResult(count);
    }

    public Task MarkAllAchievementsAsSeenAsync()
    {
        EnsureInitialized();

        _connection.Execute("UPDATE EarnedAchievements SET HasBeenSeen = 1 WHERE HasBeenSeen = 0");

        return Task.CompletedTask;
    }

    public async Task<int> GetPerfectDaysCountAsync()
    {
        EnsureInitialized();

        var settings = await GetSettingsAsync();
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var datesWithEntries = await GetDatesWithEntriesAsync();
        var perfectDays = 0;

        foreach (var date in datesWithEntries)
        {
            var entries = await GetEntriesForDateAsync(date);
            if (IsDateComplete(entries, enabledItems))
            {
                perfectDays++;
            }
        }

        return perfectDays;
    }

    public Task<int> GetItemCompletionCountAsync(string itemId)
    {
        EnsureInitialized();

        var item = ChecklistDefinitions.GetItemById(itemId);
        if (item == null) return Task.FromResult(0);

        var count = _connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM DailyEntries WHERE ItemId = ? AND ServingsCompleted >= ?",
            itemId, item.RecommendedServings);

        return Task.FromResult(count);
    }

    public Task<int> GetTotalDaysTrackedAsync()
    {
        EnsureInitialized();

        var count = _connection.ExecuteScalar<int>("SELECT COUNT(DISTINCT Date) FROM DailyEntries");

        return Task.FromResult(count);
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
