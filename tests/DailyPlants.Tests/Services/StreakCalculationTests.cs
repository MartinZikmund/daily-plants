using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Streak and perfect-day correctness. These numbers are the app's core promise,
/// so they must not depend on how long the user has been tracking or on settings
/// they changed after the fact.
/// </summary>
[TestClass]
public class StreakCalculationTests
{
    private string _dbPath = string.Empty;
    private FakeAppPreferences _prefs = null!;
    private SqliteDataService _service = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Streak-{Guid.NewGuid():N}.db");
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _service = new SqliteDataService(_prefs, _dbPath);
        await _service.InitializeAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // SQLite handle may still be released asynchronously; ignore.
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Reduces the Daily Dozen to a single required item so streak tests stay fast
    /// and assert on streak logic rather than on bulk insert throughput.
    /// </summary>
    private void RequireOnlyBeans()
    {
        var disabled = ChecklistDefinitions.AllItems
            .Where(i => i.Id != "beans")
            .Select(i => i.Id);
        _prefs.DisabledItemIds = string.Join(',', disabled);
    }

    private async Task CompleteBeansForDaysAsync(int days, DateOnly endingOn)
    {
        for (var i = 0; i < days; i++)
        {
            await _service.SaveEntryAsync(new DailyEntry
            {
                Date = endingOn.AddDays(-i),
                ItemId = "beans",
                ServingsCompleted = 3
            });
        }
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_WithMoreThanAYearOfCompleteDays_IsNotTruncated()
    {
        RequireOnlyBeans();
        await CompleteBeansForDaysAsync(400, Today());

        var streak = await _service.GetCurrentStreakAsync();

        streak.Should().Be(400, "a streak longer than the lookback window must still be reported in full");
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_AtTheOldLookbackBoundary_CountsEveryDay()
    {
        RequireOnlyBeans();
        await CompleteBeansForDaysAsync(367, Today());

        var streak = await _service.GetCurrentStreakAsync();

        streak.Should().Be(367);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_WithAGapBeforeTheWindow_StopsAtTheGap()
    {
        RequireOnlyBeans();
        var today = Today();
        await CompleteBeansForDaysAsync(10, today);
        // Gap at today-10, then more complete days further back.
        await CompleteBeansForDaysAsync(5, today.AddDays(-11));

        var streak = await _service.GetCurrentStreakAsync();

        streak.Should().Be(10, "the streak ends at the first incomplete day");
    }
}
