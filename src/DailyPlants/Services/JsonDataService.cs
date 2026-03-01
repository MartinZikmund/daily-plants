using System.Text.Json;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// JSON file-based implementation of IDataService.
/// Stores data in JSON files in the app's local data folder.
/// </summary>
public class JsonDataService : IDataService
{
    private readonly string _dataFolderPath;
    private readonly string _entriesFilePath;
    private readonly string _weightFilePath;
    private readonly string _settingsFilePath;
    private readonly string _achievementsFilePath;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private DataStore? _dataStore;
    private bool _initialized;

    public JsonDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dataFolderPath = Path.Combine(appDataPath, "DailyPlants");
        _entriesFilePath = Path.Combine(_dataFolderPath, "daily_entries.json");
        _weightFilePath = Path.Combine(_dataFolderPath, "weight_entries.json");
        _settingsFilePath = Path.Combine(_dataFolderPath, "settings.json");
        _achievementsFilePath = Path.Combine(_dataFolderPath, "achievements.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Ensure directory exists
        if (!Directory.Exists(_dataFolderPath))
        {
            Directory.CreateDirectory(_dataFolderPath);
        }

        _dataStore = await LoadDataStoreAsync();
        _initialized = true;
    }

    // ===== Daily Entries =====

    public async Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId)
    {
        await EnsureInitializedAsync();
        return _dataStore!.DailyEntries.FirstOrDefault(e => e.Date == date && e.ItemId == itemId);
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();
        return _dataStore!.DailyEntries.Where(e => e.Date == date).ToList();
    }

    public async Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();
        return _dataStore!.DailyEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToList();
    }

    public async Task SaveEntryAsync(DailyEntry entry)
    {
        await EnsureInitializedAsync();

        var existing = _dataStore!.DailyEntries.FirstOrDefault(e => e.Date == entry.Date && e.ItemId == entry.ItemId);
        if (existing != null)
        {
            existing.ServingsCompleted = entry.ServingsCompleted;
        }
        else
        {
            entry.Id = _dataStore.DailyEntries.Any() ? _dataStore.DailyEntries.Max(e => e.Id) + 1 : 1;
            _dataStore.DailyEntries.Add(entry);
        }

        await SaveDailyEntriesAsync();
    }

    public async Task DeleteEntriesForDateAsync(DateOnly date)
    {
        await EnsureInitializedAsync();
        _dataStore!.DailyEntries.RemoveAll(e => e.Date == date);
        await SaveDailyEntriesAsync();
    }

    // ===== Weight Entries =====

    public async Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync()
    {
        await EnsureInitializedAsync();
        return _dataStore!.WeightEntries.OrderBy(e => e.Date).ToList();
    }

    public async Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        await EnsureInitializedAsync();
        return _dataStore!.WeightEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToList();
    }

    public async Task<WeightEntry?> GetWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();
        return _dataStore!.WeightEntries.FirstOrDefault(e => e.Date == date);
    }

    public async Task SaveWeightEntryAsync(WeightEntry entry)
    {
        await EnsureInitializedAsync();

        var existing = _dataStore!.WeightEntries.FirstOrDefault(e => e.Date == entry.Date);
        if (existing != null)
        {
            existing.Weight = entry.Weight;
            existing.Notes = entry.Notes;
        }
        else
        {
            entry.Id = _dataStore.WeightEntries.Any() ? _dataStore.WeightEntries.Max(e => e.Id) + 1 : 1;
            _dataStore.WeightEntries.Add(entry);
        }

        await SaveWeightEntriesAsync();
    }

    public async Task DeleteWeightEntryAsync(DateOnly date)
    {
        await EnsureInitializedAsync();
        _dataStore!.WeightEntries.RemoveAll(e => e.Date == date);
        await SaveWeightEntriesAsync();
    }

    // ===== User Settings =====

    public async Task<UserSettings> GetSettingsAsync()
    {
        await EnsureInitializedAsync();
        return _dataStore!.Settings;
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        await EnsureInitializedAsync();
        _dataStore!.Settings = settings;
        await SaveSettingsAsync();
    }

    // ===== Statistics =====

    public async Task<int> GetCurrentStreakAsync()
    {
        await EnsureInitializedAsync();

        var settings = _dataStore!.Settings;
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var streak = 0;
        var currentDate = today;

        while (true)
        {
            var entries = _dataStore.DailyEntries.Where(e => e.Date == currentDate).ToList();
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

        var settings = _dataStore!.Settings;
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var datesWithEntries = _dataStore.DailyEntries.Select(e => e.Date).Distinct().OrderBy(d => d).ToList();
        if (datesWithEntries.Count == 0) return 0;

        var longestStreak = 0;
        var currentStreak = 0;
        DateOnly? previousDate = null;

        foreach (var date in datesWithEntries)
        {
            var entries = _dataStore.DailyEntries.Where(e => e.Date == date).ToList();
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
        return _dataStore!.DailyEntries.Select(e => e.Date).Distinct().OrderBy(d => d).ToList();
    }

    // ===== Private Helpers =====

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private async Task<DataStore> LoadDataStoreAsync()
    {
        var dataStore = new DataStore();

        // Load daily entries
        if (File.Exists(_entriesFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_entriesFilePath);
                var entries = JsonSerializer.Deserialize<List<DailyEntryDto>>(json, _jsonOptions);
                if (entries != null)
                {
                    dataStore.DailyEntries = entries.Select(dto => new DailyEntry
                    {
                        Id = dto.Id,
                        Date = DateOnly.Parse(dto.Date),
                        ItemId = dto.ItemId,
                        ServingsCompleted = dto.ServingsCompleted
                    }).ToList();
                }
            }
            catch
            {
                // If file is corrupted, start fresh
            }
        }

        // Load weight entries
        if (File.Exists(_weightFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_weightFilePath);
                var entries = JsonSerializer.Deserialize<List<WeightEntryDto>>(json, _jsonOptions);
                if (entries != null)
                {
                    dataStore.WeightEntries = entries.Select(dto => new WeightEntry
                    {
                        Id = dto.Id,
                        Date = DateOnly.Parse(dto.Date),
                        Weight = dto.Weight,
                        Notes = dto.Notes
                    }).ToList();
                }
            }
            catch
            {
                // If file is corrupted, start fresh
            }
        }

        // Load settings
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);
                if (settings != null)
                {
                    dataStore.Settings = settings;
                }
            }
            catch
            {
                // If file is corrupted, use defaults
            }
        }

        // Load achievements
        if (File.Exists(_achievementsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_achievementsFilePath);
                var achievements = JsonSerializer.Deserialize<List<EarnedAchievementDto>>(json, _jsonOptions);
                if (achievements != null)
                {
                    dataStore.EarnedAchievements = achievements.Select(dto => new EarnedAchievement
                    {
                        Id = dto.Id,
                        AchievementId = dto.AchievementId,
                        EarnedAt = DateTime.Parse(dto.EarnedAt),
                        HasBeenSeen = dto.HasBeenSeen
                    }).ToList();
                }
            }
            catch
            {
                // If file is corrupted, start fresh
            }
        }

        return dataStore;
    }

    private async Task SaveDailyEntriesAsync()
    {
        var dtos = _dataStore!.DailyEntries.Select(e => new DailyEntryDto
        {
            Id = e.Id,
            Date = e.Date.ToString("yyyy-MM-dd"),
            ItemId = e.ItemId,
            ServingsCompleted = e.ServingsCompleted
        }).ToList();

        var json = JsonSerializer.Serialize(dtos, _jsonOptions);
        await File.WriteAllTextAsync(_entriesFilePath, json);
    }

    private async Task SaveWeightEntriesAsync()
    {
        var dtos = _dataStore!.WeightEntries.Select(e => new WeightEntryDto
        {
            Id = e.Id,
            Date = e.Date.ToString("yyyy-MM-dd"),
            Weight = e.Weight,
            Notes = e.Notes
        }).ToList();

        var json = JsonSerializer.Serialize(dtos, _jsonOptions);
        await File.WriteAllTextAsync(_weightFilePath, json);
    }

    private async Task SaveSettingsAsync()
    {
        var json = JsonSerializer.Serialize(_dataStore!.Settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
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

    // ===== Achievement Methods =====

    public async Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync()
    {
        await EnsureInitializedAsync();
        return _dataStore!.EarnedAchievements.ToList();
    }

    public async Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        await EnsureInitializedAsync();

        var existing = _dataStore!.EarnedAchievements.FirstOrDefault(a => a.AchievementId == achievement.AchievementId);
        if (existing == null)
        {
            achievement.Id = _dataStore.EarnedAchievements.Any() ? _dataStore.EarnedAchievements.Max(a => a.Id) + 1 : 1;
            _dataStore.EarnedAchievements.Add(achievement);
            await SaveAchievementsAsync();
        }
    }

    public async Task<bool> IsAchievementEarnedAsync(string achievementId)
    {
        await EnsureInitializedAsync();
        return _dataStore!.EarnedAchievements.Any(a => a.AchievementId == achievementId);
    }

    public async Task<int> GetUnseenAchievementCountAsync()
    {
        await EnsureInitializedAsync();
        return _dataStore!.EarnedAchievements.Count(a => !a.HasBeenSeen);
    }

    public async Task MarkAllAchievementsAsSeenAsync()
    {
        await EnsureInitializedAsync();
        foreach (var achievement in _dataStore!.EarnedAchievements)
        {
            achievement.HasBeenSeen = true;
        }
        await SaveAchievementsAsync();
    }

    public async Task<int> GetPerfectDaysCountAsync()
    {
        await EnsureInitializedAsync();

        var settings = _dataStore!.Settings;
        var enabledItems = GetEnabledItemIds(settings);
        if (enabledItems.Count == 0) return 0;

        var datesWithEntries = _dataStore.DailyEntries.Select(e => e.Date).Distinct().ToList();
        var perfectDays = 0;

        foreach (var date in datesWithEntries)
        {
            var entries = _dataStore.DailyEntries.Where(e => e.Date == date).ToList();
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

        return _dataStore!.DailyEntries
            .Where(e => e.ItemId == itemId && e.ServingsCompleted >= item.RecommendedServings)
            .Select(e => e.Date)
            .Distinct()
            .Count();
    }

    public async Task<int> GetTotalDaysTrackedAsync()
    {
        await EnsureInitializedAsync();
        return _dataStore!.DailyEntries.Select(e => e.Date).Distinct().Count();
    }

    private async Task SaveAchievementsAsync()
    {
        var dtos = _dataStore!.EarnedAchievements.Select(a => new EarnedAchievementDto
        {
            Id = a.Id,
            AchievementId = a.AchievementId,
            EarnedAt = a.EarnedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            HasBeenSeen = a.HasBeenSeen
        }).ToList();

        var json = JsonSerializer.Serialize(dtos, _jsonOptions);
        await File.WriteAllTextAsync(_achievementsFilePath, json);
    }

    // ===== DTOs for JSON serialization (DateOnly doesn't serialize well by default) =====

    private class DataStore
    {
        public List<DailyEntry> DailyEntries { get; set; } = [];
        public List<WeightEntry> WeightEntries { get; set; } = [];
        public List<EarnedAchievement> EarnedAchievements { get; set; } = [];
        public UserSettings Settings { get; set; } = new();
    }

    private class DailyEntryDto
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public int ServingsCompleted { get; set; }
    }

    private class WeightEntryDto
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string? Notes { get; set; }
    }

    private class EarnedAchievementDto
    {
        public int Id { get; set; }
        public string AchievementId { get; set; } = string.Empty;
        public string EarnedAt { get; set; } = string.Empty;
        public bool HasBeenSeen { get; set; }
    }
}
