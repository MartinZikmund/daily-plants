namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Wraps a working data service and fails writes on demand, standing in for the transient
/// SQLite failures the app has to survive: a locked database, a full disk, or the OS
/// reclaiming resources while the app is being backgrounded.
/// </summary>
internal sealed class FailingDataService : IDataService
{
    private readonly IDataService _inner;

    public bool FailWrites { get; set; }

    /// <summary>Fails the date-range queries, standing in for a read that hits a busy database.</summary>
    public bool FailDateQueries { get; set; }

    /// <summary>Lets the first N writes through, then fails, for testing partial writes.</summary>
    public int? FailAfterWrites { get; set; }

    private int _writes;

    public FailingDataService(IDataService inner) => _inner = inner;

    private void ThrowIfFailing()
    {
        if (FailAfterWrites is { } allowed && _writes++ >= allowed)
        {
            throw new InvalidOperationException("database is locked");
        }

        if (FailWrites)
        {
            throw new InvalidOperationException("database is locked");
        }
    }

    public Task InitializeAsync() => _inner.InitializeAsync();

    public Task RunInTransactionAsync(Func<Task> operation) => _inner.RunInTransactionAsync(operation);

    public Task<DailyEntry?> GetEntryAsync(DateOnly date, string itemId) => _inner.GetEntryAsync(date, itemId);

    public Task<IReadOnlyList<DailyEntry>> GetEntriesForDateAsync(DateOnly date) => _inner.GetEntriesForDateAsync(date);

    public Task<IReadOnlyList<DailyEntry>> GetEntriesInRangeAsync(DateOnly startDate, DateOnly endDate) =>
        _inner.GetEntriesInRangeAsync(startDate, endDate);

    public Task SaveEntryAsync(DailyEntry entry)
    {
        ThrowIfFailing();
        return _inner.SaveEntryAsync(entry);
    }

    public Task DeleteEntriesForDateAsync(DateOnly date)
    {
        ThrowIfFailing();
        return _inner.DeleteEntriesForDateAsync(date);
    }

    public Task<IReadOnlyList<WeightEntry>> GetAllWeightEntriesAsync() => _inner.GetAllWeightEntriesAsync();

    public Task<IReadOnlyList<WeightEntry>> GetWeightEntriesInRangeAsync(DateOnly startDate, DateOnly endDate) =>
        _inner.GetWeightEntriesInRangeAsync(startDate, endDate);

    public Task<WeightEntry?> GetWeightEntryAsync(DateOnly date) => _inner.GetWeightEntryAsync(date);

    public Task SaveWeightEntryAsync(WeightEntry entry)
    {
        ThrowIfFailing();
        return _inner.SaveWeightEntryAsync(entry);
    }

    public Task DeleteWeightEntryAsync(DateOnly date)
    {
        ThrowIfFailing();
        return _inner.DeleteWeightEntryAsync(date);
    }

    public Task<int> GetCurrentStreakAsync() => _inner.GetCurrentStreakAsync();

    public Task<int> GetLongestStreakAsync() => _inner.GetLongestStreakAsync();

    public Task<IReadOnlyList<DateOnly>> GetDatesWithEntriesAsync()
    {
        if (FailDateQueries)
        {
            throw new InvalidOperationException("database is locked");
        }

        return _inner.GetDatesWithEntriesAsync();
    }

    public Task<IReadOnlyList<EarnedAchievement>> GetEarnedAchievementsAsync() => _inner.GetEarnedAchievementsAsync();

    public Task SaveEarnedAchievementAsync(EarnedAchievement achievement)
    {
        ThrowIfFailing();
        return _inner.SaveEarnedAchievementAsync(achievement);
    }

    public Task<bool> IsAchievementEarnedAsync(string achievementId) => _inner.IsAchievementEarnedAsync(achievementId);

    public Task<int> GetUnseenAchievementCountAsync() => _inner.GetUnseenAchievementCountAsync();

    public Task MarkAllAchievementsAsSeenAsync() => _inner.MarkAllAchievementsAsSeenAsync();

    public Task<int> GetPerfectDaysCountAsync() => _inner.GetPerfectDaysCountAsync();

    public Task<int> GetItemCompletionCountAsync(string itemId) => _inner.GetItemCompletionCountAsync(itemId);

    public Task<int> GetTotalDaysTrackedAsync() => _inner.GetTotalDaysTrackedAsync();
}
