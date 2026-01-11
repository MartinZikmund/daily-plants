using DailyDozen.Models;

namespace DailyDozen.Services;

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

    // ===== User Settings =====

    /// <summary>
    /// Gets the user settings.
    /// </summary>
    Task<UserSettings> GetSettingsAsync();

    /// <summary>
    /// Saves the user settings.
    /// </summary>
    Task SaveSettingsAsync(UserSettings settings);

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
}
