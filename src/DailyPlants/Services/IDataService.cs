using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Interface for data persistence operations.
/// </summary>
public interface IDataService
{
    /// <summary>
    /// Initializes the database, creating tables if needed.
    /// </summary>
    Task InitializeAsync();

    // ===== Daily Entries =====

    /// <summary>
    /// Gets the entry for a specific item on a specific date.
    /// </summary>
    Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId);

    /// <summary>
    /// Gets all entries for a specific date.
    /// </summary>
    Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date);

    /// <summary>
    /// Gets all entries within a date range.
    /// </summary>
    Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Saves or updates a daily entry.
    /// </summary>
    Task SaveEntryAsync(DailyEntry entry);

    /// <summary>
    /// Deletes all entries for a specific date.
    /// </summary>
    Task DeleteEntriesForDateAsync(DateOnly date);

    // ===== Weight Entries =====

    /// <summary>
    /// Gets all weight entries.
    /// </summary>
    Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync();

    /// <summary>
    /// Gets weight entries within a date range.
    /// </summary>
    Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Gets the weight entry for a specific date.
    /// </summary>
    Task<WeightEntry?> GetWeightEntryAsync(DateOnly date);

    /// <summary>
    /// Saves or updates a weight entry.
    /// </summary>
    Task SaveWeightEntryAsync(WeightEntry entry);

    /// <summary>
    /// Deletes a weight entry.
    /// </summary>
    Task DeleteWeightEntryAsync(DateOnly date);

    // ===== Statistics =====

    /// <summary>
    /// Gets the current streak (consecutive days with 100% completion).
    /// </summary>
    Task<int> GetCurrentStreakAsync();

    /// <summary>
    /// Gets the longest streak ever achieved.
    /// </summary>
    Task<int> GetLongestStreakAsync();

    /// <summary>
    /// Gets dates that have any entries (for calendar highlighting).
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync();

    // ===== Achievements =====

    /// <summary>
    /// Gets all earned achievements.
    /// </summary>
    Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync();

    /// <summary>
    /// Saves a newly earned achievement.
    /// </summary>
    Task SaveEarnedAchievementAsync(EarnedAchievement achievement);

    /// <summary>
    /// Checks if an achievement has been earned.
    /// </summary>
    Task<bool> IsAchievementEarnedAsync(string achievementId);

    /// <summary>
    /// Gets the count of unseen achievements.
    /// </summary>
    Task<int> GetUnseenAchievementCountAsync();

    /// <summary>
    /// Marks all achievements as seen.
    /// </summary>
    Task MarkAllAchievementsAsSeenAsync();

    /// <summary>
    /// Gets the total count of perfect days (100% completion).
    /// </summary>
    Task<int> GetPerfectDaysCountAsync();

    /// <summary>
    /// Gets the total count of days where a specific item was completed.
    /// </summary>
    Task<int> GetItemCompletionCountAsync(string itemId);

    /// <summary>
    /// Gets the total number of days with any entries.
    /// </summary>
    Task<int> GetTotalDaysTrackedAsync();

    // ===== Custom Items (definitions) =====

    /// <summary>
    /// Gets all custom item definitions, ordered by SortOrder.
    /// </summary>
    Task<IReadOnlyList<CustomItem>> GetCustomItemsAsync();

    /// <summary>
    /// Gets a single custom item definition by Id, or null if missing.
    /// </summary>
    Task<CustomItem?> GetCustomItemByIdAsync(string id);

    /// <summary>
    /// Inserts or updates a custom item definition (upsert by Id).
    /// </summary>
    Task SaveCustomItemAsync(CustomItem item);

    /// <summary>
    /// Deletes a custom item definition. When cascadeEntries is true, also
    /// deletes all CustomItemEntries rows for this Id in a single transaction.
    /// When false, entries remain as orphans (FR-004 keep-history).
    /// </summary>
    Task DeleteCustomItemAsync(string id, bool cascadeEntries);

    // ===== Custom Item Entries =====

    /// <summary>
    /// Gets all custom item entries (including orphans) for a specific date.
    /// </summary>
    Task<IReadOnlyList<CustomItemEntry>> GetCustomItemEntriesForDateAsync(DateOnly date);

    /// <summary>
    /// Gets all custom item entries within a date range.
    /// </summary>
    Task<IReadOnlyList<CustomItemEntry>> GetCustomItemEntriesInRangeAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Gets every custom item entry in the database (used by export).
    /// Includes orphans whose parent CustomItem has been deleted.
    /// </summary>
    Task<IReadOnlyList<CustomItemEntry>> GetAllCustomItemEntriesAsync();

    /// <summary>
    /// Inserts or updates a custom item entry (upsert by Date + CustomItemId).
    /// </summary>
    Task SaveCustomItemEntryAsync(CustomItemEntry entry);
}
