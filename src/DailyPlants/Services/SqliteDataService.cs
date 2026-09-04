using System.Globalization;
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
    private readonly SQLiteAsyncConnection _connection;
    private readonly IAppPreferences _appPreferences;
    private bool _initialized;

    /// <summary>
    /// Held for the whole of <see cref="RunInTransactionAsync"/>. sqlite-net takes its
    /// connection lock per statement, not per transaction, so without this a write from
    /// elsewhere lands between the BEGIN and the COMMIT and is discarded by a rollback
    /// that has nothing to do with it.
    /// </summary>
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>Set on the flow running the transaction body, whose writes belong inside it.</summary>
    private readonly AsyncLocal<bool> _inTransaction = new();

    public SqliteDataService(IAppPreferences appPreferences)
        : this(appPreferences, GetDefaultDatabasePath())
    {
    }

    public SqliteDataService(IAppPreferences appPreferences, string databasePath)
    {
        _appPreferences = appPreferences;

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SQLiteAsyncConnection(databasePath);
    }

    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "DailyPlants", "dailyplants.db");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _connection.CreateTableAsync<DailyEntryEntity>();
        await _connection.CreateTableAsync<WeightEntryEntity>();
        await _connection.CreateTableAsync<EarnedAchievementEntity>();
        await _connection.CreateTableAsync<DailySettingsSnapshotEntity>();

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
            await MigrateAsync(1, () => Task.CompletedTask);
        }

        if (version < 2)
        {
            // v2: Removed Anti-Aging Eight checklist and corrected 21 Tweaks items.
            // Orphaned DailyEntries for removed items (sun_protection, fat_free_dressings,
            // more_legumes, more_greens, more_berries) are intentionally preserved.
            await MigrateAsync(2, () => Task.CompletedTask);
        }

        if (version < 3)
        {
            // v3: Per-date snapshots of the checklist requirements, so that changing
            // settings no longer rewrites past streaks and perfect days. Existing history
            // is backfilled with the current settings — the best available evidence of
            // what these days were tracked against, and it keeps today's numbers stable.
            await MigrateAsync(3, BackfillSettingsSnapshotsAsync);
        }

        if (version < 4)
        {
            // v4: Weight is now stored in kilograms and height in centimetres regardless of
            // the unit the user types in. Earlier versions stored whatever was typed, so an
            // imperial user's existing rows are pounds and their HeightCm is really inches.
            await MigrateAsync(4, ConvertStoredWeightsToKilogramsAsync);
        }

        // Future migrations go here:
        // if (version < 5) { await MigrateAsync(5, MigrateToV5Async); }

        // Not gated on the schema version: the preferences this converts are not in the
        // database and do not disappear with it.
        ConvertPreferencesToCanonicalUnits();
    }

    /// <summary>
    /// Applies one migration and its version bump as a single transaction. Run as separate
    /// statements they are separate autocommits, leaving a window where the work is durable
    /// but the version is not — and this runs during launch, before the window is shown,
    /// which is exactly where the OS kills apps. The next launch would then apply the same
    /// migration again: for v4 that divides every stored weight by 2.2 a second time.
    /// </summary>
    private async Task MigrateAsync(int version, Func<Task> migrate)
    {
        await _connection.ExecuteAsync("BEGIN TRANSACTION");
        try
        {
            await migrate();

            // user_version lives in the database header, so this bump rolls back with the rest.
            await _connection.ExecuteAsync($"PRAGMA user_version = {version}");
            await _connection.ExecuteAsync("COMMIT");
        }
        catch
        {
            await _connection.ExecuteAsync("ROLLBACK");
            throw;
        }
    }

    private async Task ConvertStoredWeightsToKilogramsAsync()
    {
        if (_appPreferences.UseMetricUnits)
        {
            // Already stored in kilograms.
            return;
        }

        await _connection.ExecuteAsync(
            "UPDATE WeightEntries SET Weight = Weight / ?", UnitConverter.PoundsPerKilogram);
    }

    /// <summary>
    /// Brings the height and goal weight into centimetres and kilograms, once ever. The
    /// database's user_version cannot gate this: preferences outlive the database file, so
    /// a database that is cleared, restored from a partial backup, or recreated after the
    /// failure dialog would convert an already-canonical height a second time — 177.8 cm
    /// read back as inches becomes 451.6.
    /// </summary>
    private void ConvertPreferencesToCanonicalUnits()
    {
        if (_appPreferences.UnitsAreCanonical) return;

        if (!_appPreferences.UseMetricUnits)
        {
            if (_appPreferences.HeightCm is { } heightInInches)
            {
                _appPreferences.HeightCm = UnitConverter.DisplayToCentimetres(heightInInches, useMetric: false);
            }

            if (_appPreferences.GoalWeight is { } goalInPounds)
            {
                _appPreferences.GoalWeight = UnitConverter.DisplayToKilograms(goalInPounds, useMetric: false);
            }
        }

        _appPreferences.UnitsAreCanonical = true;
    }

    public async Task RunInTransactionAsync(Func<Task> operation)
    {
        await EnsureInitializedAsync();

        await _writeGate.WaitAsync();
        _inTransaction.Value = true;
        try
        {
            await _connection.ExecuteAsync("BEGIN TRANSACTION");
            try
            {
                await operation();
                await _connection.ExecuteAsync("COMMIT");
            }
            catch
            {
                try
                {
                    await _connection.ExecuteAsync("ROLLBACK");
                }
                catch (SQLiteException)
                {
                    // SQLite may have already rolled back on its own (a full disk, for one).
                    // Reporting that instead of the original failure only hides the cause.
                }

                throw;
            }
        }
        finally
        {
            _inTransaction.Value = false;
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Waits for any in-flight transaction to finish, so this write is not swept into it.
    /// A write made by the transaction body itself belongs inside and passes straight through.
    /// </summary>
    private async Task<bool> EnterWriteAsync()
    {
        if (_inTransaction.Value) return false;

        await _writeGate.WaitAsync();
        return true;
    }

    private void ExitWrite(bool taken)
    {
        if (taken) _writeGate.Release();
    }

    private async Task BackfillSettingsSnapshotsAsync()
    {
        var requirements = SerializeRequirements(ChecklistDefinitions.GetRequiredServingsMap(_appPreferences));
        await _connection.ExecuteAsync(
            "INSERT OR IGNORE INTO DailySettingsSnapshots (Date, RequiredItems) SELECT DISTINCT Date, ? FROM DailyEntries",
            requirements);
    }

    /// <summary>
    /// Records the requirements in force for <paramref name="date"/> the first time anything
    /// is logged for it. Later writes on the same date deliberately do not overwrite it, so
    /// enabling a checklist in the evening cannot invalidate a day already completed.
    /// </summary>
    private async Task EnsureSettingsSnapshotAsync(DateOnly date)
    {
        var requirements = SerializeRequirements(ChecklistDefinitions.GetRequiredServingsMap(_appPreferences));
        await _connection.ExecuteAsync(
            "INSERT OR IGNORE INTO DailySettingsSnapshots (Date, RequiredItems) VALUES (?, ?)",
            IsoDate.ToStorage(date), requirements);
    }

    private async Task<Dictionary<DateOnly, Dictionary<string, int>>> LoadSettingsSnapshotsAsync()
    {
        var rows = await _connection.QueryAsync<DailySettingsSnapshotEntity>(
            "SELECT * FROM DailySettingsSnapshots");

        var result = new Dictionary<DateOnly, Dictionary<string, int>>();
        foreach (var row in rows)
        {
            if (IsoDate.TryParse(row.Date, out var date))
            {
                result[date] = DeserializeRequirements(row.RequiredItems);
            }
        }

        return result;
    }

    private static string SerializeRequirements(Dictionary<string, int> requirements) =>
        string.Join(',', requirements.Select(r => $"{r.Key}:{r.Value.ToString(CultureInfo.InvariantCulture)}"));

    private static Dictionary<string, int> DeserializeRequirements(string value)
    {
        var result = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(value)) return result;

        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.LastIndexOf(':');
            if (separator <= 0) continue;

            var itemId = pair[..separator];
            if (int.TryParse(pair[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var servings))
            {
                result[itemId] = servings;
            }
        }

        return result;
    }

    // ===== Daily Entries =====

    public async Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId)
    {
        await EnsureInitializedAsync();

        var dateStr = IsoDate.ToStorage(date);
        var entities = await _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr && e.ItemId == itemId)
            .ToListAsync();

        var entity = entities.FirstOrDefault();
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = IsoDate.ToStorage(date);
        var entities = await _connection.Table<DailyEntryEntity>()
            .Where(e => e.Date == dateStr)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();

        var startStr = IsoDate.ToStorage(startDate);
        var endStr = IsoDate.ToStorage(endDate);
        var entities = await _connection.QueryAsync<DailyEntryEntity>(
            "SELECT * FROM DailyEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        return entities.Select(ToModel).ToList();
    }

    public async Task SaveEntryAsync(DailyEntry entry)
    {
        await EnsureInitializedAsync();

        var gated = await EnterWriteAsync();
        try
        {
            await EnsureSettingsSnapshotAsync(entry.Date);

            var dateStr = IsoDate.ToStorage(entry.Date);
            await _connection.ExecuteAsync(
                "INSERT INTO DailyEntries (Date, ItemId, ServingsCompleted) VALUES (?, ?, ?) ON CONFLICT(Date, ItemId) DO UPDATE SET ServingsCompleted = ?",
                dateStr, entry.ItemId, entry.ServingsCompleted, entry.ServingsCompleted);
        }
        finally
        {
            ExitWrite(gated);
        }
    }

    public async Task DeleteEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var gated = await EnterWriteAsync();
        try
        {
            var dateStr = IsoDate.ToStorage(date);
            await _connection.ExecuteAsync("DELETE FROM DailyEntries WHERE Date = ?", dateStr);
        }
        finally
        {
            ExitWrite(gated);
        }
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

        var startStr = IsoDate.ToStorage(startDate);
        var endStr = IsoDate.ToStorage(endDate);
        var entities = await _connection.QueryAsync<WeightEntryEntity>(
            "SELECT * FROM WeightEntries WHERE Date BETWEEN ? AND ? ORDER BY Date",
            startStr, endStr);

        return entities.Select(ToModel).ToList();
    }

    public async Task<WeightEntry?> GetWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var dateStr = IsoDate.ToStorage(date);
        var entities = await _connection.Table<WeightEntryEntity>()
            .Where(e => e.Date == dateStr)
            .ToListAsync();

        var entity = entities.FirstOrDefault();
        return entity is null ? null : ToModel(entity);
    }

    public async Task SaveWeightEntryAsync(WeightEntry entry)
    {
        await EnsureInitializedAsync();

        var gated = await EnterWriteAsync();
        try
        {
            var dateStr = IsoDate.ToStorage(entry.Date);
            await _connection.ExecuteAsync(
                "INSERT INTO WeightEntries (Date, Weight, Notes) VALUES (?, ?, ?) ON CONFLICT(Date) DO UPDATE SET Weight = ?, Notes = ?",
                dateStr, entry.Weight, entry.Notes, entry.Weight, entry.Notes);
        }
        finally
        {
            ExitWrite(gated);
        }
    }

    public async Task DeleteWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();

        var gated = await EnterWriteAsync();
        try
        {
            var dateStr = IsoDate.ToStorage(date);
            await _connection.ExecuteAsync("DELETE FROM WeightEntries WHERE Date = ?", dateStr);
        }
        finally
        {
            ExitWrite(gated);
        }
    }

    // ===== Statistics =====

    public async Task<int> GetCurrentStreakAsync()
    {
        await EnsureInitializedAsync();

        var currentRequirements = ChecklistDefinitions.GetRequiredServingsMap(_appPreferences);
        var snapshots = await LoadSettingsSnapshotsAsync();

        // Load the full history: a windowed lookback cannot tell "no entry" from
        // "outside the window", which silently truncated streaks at the boundary.
        // GetLongestStreakAsync already scans the whole table, so this is no new cost class.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var allEntries = await GetEntriesInRangeAsync(DateOnly.MinValue, today);

        // Group entries by date
        var entriesByDate = allEntries.GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var streak = 0;
        var currentDate = today;

        while (true)
        {
            if (IsDateComplete(entriesByDate.GetValueOrDefault(currentDate), currentDate, snapshots, currentRequirements))
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

        var currentRequirements = ChecklistDefinitions.GetRequiredServingsMap(_appPreferences);
        var snapshots = await LoadSettingsSnapshotsAsync();

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
            if (IsDateComplete(entriesByDate[date], date, snapshots, currentRequirements))
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
        return dates.Select(IsoDate.Parse).ToList();
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    /// <summary>
    /// Judges a day against the requirements captured for that date, falling back to the
    /// current settings only for dates with no snapshot (history predating the v3 migration
    /// is backfilled, so this is a safety net rather than the normal path).
    /// </summary>
    private static bool IsDateComplete(
        IReadOnlyList<DailyEntry>? entries,
        DateOnly date,
        Dictionary<DateOnly, Dictionary<string, int>> snapshots,
        Dictionary<string, int> currentRequirements)
    {
        if (entries == null || entries.Count == 0) return false;

        var requiredServings = snapshots.GetValueOrDefault(date) ?? currentRequirements;
        if (requiredServings.Count == 0) return false;

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

        var gated = await EnterWriteAsync();
        try
        {
            await _connection.ExecuteAsync(
                "INSERT OR IGNORE INTO EarnedAchievements (AchievementId, EarnedAt, HasBeenSeen) VALUES (?, ?, ?)",
                achievement.AchievementId, IsoDate.TimestampToStorage(achievement.EarnedAt), achievement.HasBeenSeen ? 1 : 0);
        }
        finally
        {
            ExitWrite(gated);
        }
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

        var gated = await EnterWriteAsync();
        try
        {
            await _connection.ExecuteAsync("UPDATE EarnedAchievements SET HasBeenSeen = 1 WHERE HasBeenSeen = 0");
        }
        finally
        {
            ExitWrite(gated);
        }
    }

    public async Task<int> GetPerfectDaysCountAsync()
    {
        await EnsureInitializedAsync();

        var currentRequirements = ChecklistDefinitions.GetRequiredServingsMap(_appPreferences);
        var snapshots = await LoadSettingsSnapshotsAsync();

        // Load ALL entries in a single query
        var allEntries = await _connection.QueryAsync<DailyEntryEntity>(
            "SELECT * FROM DailyEntries ORDER BY Date");

        if (allEntries.Count == 0) return 0;

        // Group by date and count perfect days in memory
        var entriesByDate = allEntries.Select(ToModel)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());

        var perfectDays = 0;
        foreach (var (date, entries) in entriesByDate)
        {
            if (IsDateComplete(entries, date, snapshots, currentRequirements))
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
        Date = IsoDate.Parse(entity.Date),
        ItemId = entity.ItemId,
        ServingsCompleted = entity.ServingsCompleted
    };

    private static WeightEntry ToModel(WeightEntryEntity entity) => new()
    {
        Id = entity.Id,
        Date = IsoDate.Parse(entity.Date),
        Weight = entity.Weight,
        Notes = entity.Notes
    };

    private static EarnedAchievement ToModel(EarnedAchievementEntity entity) => new()
    {
        Id = entity.Id,
        AchievementId = entity.AchievementId,
        EarnedAt = IsoDate.ParseTimestamp(entity.EarnedAt),
        HasBeenSeen = entity.HasBeenSeen == 1
    };
}
