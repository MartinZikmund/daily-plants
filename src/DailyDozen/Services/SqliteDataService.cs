using System.Text.Json;
using DailyDozen.Models;
using Microsoft.Data.Sqlite;

namespace DailyDozen.Services;

/// <summary>
/// SQLite implementation of IDataService.
/// </summary>
public class SqliteDataService : IDataService
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "DailyDozen", "dailydozen.db");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DailyEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                ItemId TEXT NOT NULL,
                ServingsCompleted INTEGER NOT NULL DEFAULT 0,
                UNIQUE(Date, ItemId)
            );

            CREATE TABLE IF NOT EXISTS WeightEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL UNIQUE,
                Weight REAL NOT NULL,
                Notes TEXT
            );

            CREATE TABLE IF NOT EXISTS UserSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                SettingsJson TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_daily_entries_date ON DailyEntries(Date);
            CREATE INDEX IF NOT EXISTS idx_weight_entries_date ON WeightEntries(Date);

            CREATE TABLE IF NOT EXISTS EarnedAchievements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AchievementId TEXT NOT NULL UNIQUE,
                EarnedAt TEXT NOT NULL,
                HasBeenSeen INTEGER NOT NULL DEFAULT 0
            );
            """;

        await command.ExecuteNonQueryAsync();
        _initialized = true;
    }

    // ===== Daily Entries =====

    public async Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, ItemId, ServingsCompleted FROM DailyEntries WHERE Date = @date AND ItemId = @itemId";
        command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@itemId", itemId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DailyEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                ItemId = reader.GetString(2),
                ServingsCompleted = reader.GetInt32(3)
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, ItemId, ServingsCompleted FROM DailyEntries WHERE Date = @date";
        command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        var entries = new List<DailyEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DailyEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                ItemId = reader.GetString(2),
                ServingsCompleted = reader.GetInt32(3)
            });
        }

        return entries;
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, ItemId, ServingsCompleted FROM DailyEntries WHERE Date >= @startDate AND Date <= @endDate ORDER BY Date";
        command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

        var entries = new List<DailyEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DailyEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                ItemId = reader.GetString(2),
                ServingsCompleted = reader.GetInt32(3)
            });
        }

        return entries;
    }

    public async Task SaveEntryAsync(DailyEntry entry)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DailyEntries (Date, ItemId, ServingsCompleted)
            VALUES (@date, @itemId, @servings)
            ON CONFLICT(Date, ItemId) DO UPDATE SET ServingsCompleted = @servings
            """;
        command.Parameters.AddWithValue("@date", entry.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@itemId", entry.ItemId);
        command.Parameters.AddWithValue("@servings", entry.ServingsCompleted);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DailyEntries WHERE Date = @date";
        command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        await command.ExecuteNonQueryAsync();
    }

    // ===== Weight Entries =====

    public async Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, Weight, Notes FROM WeightEntries ORDER BY Date";

        var entries = new List<WeightEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new WeightEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                Weight = reader.GetDouble(2),
                Notes = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return entries;
    }

    public async Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, Weight, Notes FROM WeightEntries WHERE Date >= @startDate AND Date <= @endDate ORDER BY Date";
        command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

        var entries = new List<WeightEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new WeightEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                Weight = reader.GetDouble(2),
                Notes = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return entries;
    }

    public async Task<WeightEntry?> GetWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Date, Weight, Notes FROM WeightEntries WHERE Date = @date";
        command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new WeightEntry
            {
                Id = reader.GetInt32(0),
                Date = DateOnly.Parse(reader.GetString(1)),
                Weight = reader.GetDouble(2),
                Notes = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }

        return null;
    }

    public async Task SaveWeightEntryAsync(WeightEntry entry)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WeightEntries (Date, Weight, Notes)
            VALUES (@date, @weight, @notes)
            ON CONFLICT(Date) DO UPDATE SET Weight = @weight, Notes = @notes
            """;
        command.Parameters.AddWithValue("@date", entry.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@weight", entry.Weight);
        command.Parameters.AddWithValue("@notes", entry.Notes ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM WeightEntries WHERE Date = @date";
        command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        await command.ExecuteNonQueryAsync();
    }

    // ===== User Settings =====

    public async Task<UserSettings> GetSettingsAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT SettingsJson FROM UserSettings WHERE Id = 1";

        var result = await command.ExecuteScalarAsync();
        if (result is string json)
        {
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }

        return new UserSettings();
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var json = JsonSerializer.Serialize(settings);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO UserSettings (Id, SettingsJson)
            VALUES (1, @json)
            ON CONFLICT(Id) DO UPDATE SET SettingsJson = @json
            """;
        command.Parameters.AddWithValue("@json", json);

        await command.ExecuteNonQueryAsync();
    }

    // ===== Statistics =====

    public async Task<int> GetCurrentStreakAsync()
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();

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

    public async Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Date FROM DailyEntries ORDER BY Date";

        var dates = new List<DateOnly>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dates.Add(DateOnly.Parse(reader.GetString(0)));
        }

        return dates;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private static List<string> GetEnabledItemIds(UserSettings settings)
    {
        var itemIds = new List<string>();

        if (settings.DailyDozenEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen).Select(i => i.Id));
        }

        if (settings.TwentyOneTweaksEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks).Select(i => i.Id));
        }

        if (settings.AntiAgingEightEnabled)
        {
            itemIds.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.AntiAgingEight).Select(i => i.Id));
        }

        return itemIds.Distinct().ToList();
    }

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

    public async Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, AchievementId, EarnedAt, HasBeenSeen FROM EarnedAchievements ORDER BY EarnedAt DESC";

        var achievements = new List<EarnedAchievement>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            achievements.Add(new EarnedAchievement
            {
                Id = reader.GetInt32(0),
                AchievementId = reader.GetString(1),
                EarnedAt = DateTime.Parse(reader.GetString(2)),
                HasBeenSeen = reader.GetInt32(3) == 1
            });
        }

        return achievements;
    }

    public async Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO EarnedAchievements (AchievementId, EarnedAt, HasBeenSeen)
            VALUES (@achievementId, @earnedAt, @hasBeenSeen)
            """;
        command.Parameters.AddWithValue("@achievementId", achievement.AchievementId);
        command.Parameters.AddWithValue("@earnedAt", achievement.EarnedAt.ToString("O"));
        command.Parameters.AddWithValue("@hasBeenSeen", achievement.HasBeenSeen ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsAchievementEarnedAsync(string achievementId)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM EarnedAchievements WHERE AchievementId = @achievementId";
        command.Parameters.AddWithValue("@achievementId", achievementId);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<int> GetUnseenAchievementCountAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM EarnedAchievements WHERE HasBeenSeen = 0";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task MarkAllAchievementsAsSeenAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE EarnedAchievements SET HasBeenSeen = 1 WHERE HasBeenSeen = 0";

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetPerfectDaysCountAsync()
    {
        await EnsureInitializedAsync();

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

    public async Task<int> GetItemCompletionCountAsync(string itemId)
    {
        await EnsureInitializedAsync();

        var item = ChecklistDefinitions.GetItemById(itemId);
        if (item == null) return 0;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM DailyEntries WHERE ItemId = @itemId AND ServingsCompleted >= @target";
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@target", item.RecommendedServings);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> GetTotalDaysTrackedAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(DISTINCT Date) FROM DailyEntries";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
