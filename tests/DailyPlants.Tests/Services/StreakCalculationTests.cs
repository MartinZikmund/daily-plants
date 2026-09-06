using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Streak and perfect-day correctness. These numbers are the app's core promise,
/// so they must not depend on how long the user has been tracking.
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

    // ===== Settings changes =====
    //
    // Completion is judged against the settings in force right now, for every day. That
    // is a deliberate trade: the user is free to change what they track, the numbers stay
    // explainable, and Settings warns once that history moves with them. Achievements are
    // a separate matter - they are only ever awarded, never taken back.

    /// <summary>Enables only beans (Daily Dozen) and green tea (21 Tweaks).</summary>
    private void RequireOnlyBeansAndGreenTea()
    {
        var disabled = ChecklistDefinitions.AllItems
            .Where(i => i.Id != "beans" && i.Id != "green_tea")
            .Select(i => i.Id);
        _prefs.DisabledItemIds = string.Join(',', disabled);
    }

    [TestMethod]
    public async Task GetLongestStreakAsync_AfterEnablingAnotherChecklist_RejudgesPastDays()
    {
        RequireOnlyBeansAndGreenTea();
        _prefs.TwentyOneTweaksEnabled = false;
        await CompleteBeansForDaysAsync(10, Today());
        (await _service.GetLongestStreakAsync()).Should().Be(10, "sanity: the streak exists before the change");

        _prefs.TwentyOneTweaksEnabled = true;

        var longest = await _service.GetLongestStreakAsync();

        longest.Should().Be(0, "green tea is now required and was never logged on those days");
    }

    [TestMethod]
    public async Task GetPerfectDaysCountAsync_AfterDisablingARequiredItem_CountsThoseDays()
    {
        RequireOnlyBeansAndGreenTea();
        _prefs.TwentyOneTweaksEnabled = true;
        // Only beans logged, so green tea keeps these days incomplete.
        await CompleteBeansForDaysAsync(5, Today());
        (await _service.GetPerfectDaysCountAsync()).Should().Be(0, "sanity: green tea was required and never logged");

        _prefs.DisabledItemIds = string.Join(',', ChecklistDefinitions.AllItems
            .Where(i => i.Id != "beans")
            .Select(i => i.Id));

        var perfectDays = await _service.GetPerfectDaysCountAsync();

        perfectDays.Should().Be(5, "beans is all that is tracked now, and beans was logged every day");
    }

    [TestMethod]
    public async Task GetPerfectDaysCountAsync_AfterASettingsChange_JudgesEveryDayTheSameWay()
    {
        RequireOnlyBeansAndGreenTea();
        _prefs.TwentyOneTweaksEnabled = true;
        var today = Today();

        // Logged while green tea was still required, so incomplete at the time.
        await _service.SaveEntryAsync(new DailyEntry { Date = today.AddDays(-1), ItemId = "beans", ServingsCompleted = 3 });

        // Green tea switched off, then a fresh day logged under the looser settings.
        _prefs.TwentyOneTweaksEnabled = false;
        await _service.SaveEntryAsync(new DailyEntry { Date = today, ItemId = "beans", ServingsCompleted = 3 });

        var perfectDays = await _service.GetPerfectDaysCountAsync();

        perfectDays.Should().Be(2, "one bar applies to the whole history, not one bar per day");
    }
}
