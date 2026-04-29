using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class SqliteDataServiceTests
{
    private string _dbPath = string.Empty;
    private FakeAppPreferences _prefs = null!;
    private SqliteDataService _service = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Tests-{Guid.NewGuid():N}.db");
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

    // ===== Daily Entry CRUD =====

    [TestMethod]
    public async Task SaveEntryAsync_ThenGetEntryAsync_ReturnsSaved()
    {
        var date = new DateOnly(2026, 4, 1);
        await _service.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 2 });

        var entry = await _service.GetEntryAsync(date, "beans");

        entry.Should().NotBeNull();
        entry!.ServingsCompleted.Should().Be(2);
    }

    [TestMethod]
    public async Task GetEntryAsync_NonExistent_ReturnsNull()
    {
        var entry = await _service.GetEntryAsync(new DateOnly(2026, 1, 1), "beans");

        entry.Should().BeNull();
    }

    [TestMethod]
    public async Task SaveEntryAsync_TwiceSameDateAndItem_Upserts()
    {
        var date = new DateOnly(2026, 4, 1);
        await _service.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 3 });

        var entries = await _service.GetEntriesForDateAsync(date);

        entries.Should().ContainSingle();
        entries[0].ServingsCompleted.Should().Be(3);
    }

    [TestMethod]
    public async Task GetEntriesForDateAsync_ReturnsOnlyMatchingDate()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 2);
        await _service.SaveEntryAsync(new DailyEntry { Date = d1, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d1, ItemId = "berries", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d2, ItemId = "beans", ServingsCompleted = 1 });

        var entries = await _service.GetEntriesForDateAsync(d1);

        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.Date == d1);
    }

    [TestMethod]
    public async Task GetEntriesInRangeAsync_InclusiveAndOrdered()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 2);
        var d3 = new DateOnly(2026, 4, 3);
        var d4 = new DateOnly(2026, 4, 4);
        await _service.SaveEntryAsync(new DailyEntry { Date = d3, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d1, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d2, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d4, ItemId = "beans", ServingsCompleted = 1 });

        var inRange = await _service.GetEntriesInRangeAsync(d2, d3);

        inRange.Should().HaveCount(2);
        inRange.Select(e => e.Date).Should().Equal(d2, d3);
    }

    [TestMethod]
    public async Task DeleteEntriesForDateAsync_RemovesOnlyThatDate()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 2);
        await _service.SaveEntryAsync(new DailyEntry { Date = d1, ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = d2, ItemId = "beans", ServingsCompleted = 1 });

        await _service.DeleteEntriesForDateAsync(d1);

        (await _service.GetEntriesForDateAsync(d1)).Should().BeEmpty();
        (await _service.GetEntriesForDateAsync(d2)).Should().ContainSingle();
    }

    // ===== Weight CRUD =====

    [TestMethod]
    public async Task SaveWeightEntryAsync_ThenGet_ReturnsSaved()
    {
        var date = new DateOnly(2026, 4, 1);
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 72.5, Notes = "after run" });

        var entry = await _service.GetWeightEntryAsync(date);

        entry.Should().NotBeNull();
        entry!.Weight.Should().Be(72.5);
        entry.Notes.Should().Be("after run");
    }

    [TestMethod]
    public async Task SaveWeightEntryAsync_TwiceSameDate_Upserts()
    {
        var date = new DateOnly(2026, 4, 1);
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 72.5 });
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 73.0, Notes = "later" });

        var all = await _service.GetAllWeightEntriesAsync();

        all.Should().ContainSingle();
        all[0].Weight.Should().Be(73.0);
        all[0].Notes.Should().Be("later");
    }

    [TestMethod]
    public async Task DeleteWeightEntryAsync_RemovesEntry()
    {
        var date = new DateOnly(2026, 4, 1);
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 72.5 });

        await _service.DeleteWeightEntryAsync(date);

        (await _service.GetWeightEntryAsync(date)).Should().BeNull();
    }

    [TestMethod]
    public async Task GetWeightEntriesInRangeAsync_FiltersByRange()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 5);
        var d3 = new DateOnly(2026, 4, 10);
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = d1, Weight = 70 });
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = d2, Weight = 71 });
        await _service.SaveWeightEntryAsync(new WeightEntry { Date = d3, Weight = 72 });

        var inRange = await _service.GetWeightEntriesInRangeAsync(d2, d3);

        inRange.Should().HaveCount(2);
        inRange.Select(e => e.Date).Should().Equal(d2, d3);
    }

    // ===== Streak / Stats =====

    private async Task SaveCompleteDayAsync(DateOnly date)
    {
        // Saves enough servings for every enabled item so the day is "complete".
        var required = ChecklistDefinitions.GetRequiredServingsMap(_prefs);
        foreach (var (itemId, servings) in required)
        {
            await _service.SaveEntryAsync(new DailyEntry { Date = date, ItemId = itemId, ServingsCompleted = servings });
        }
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_EmptyDb_ReturnsZero()
    {
        (await _service.GetCurrentStreakAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_TodayCompleteOnly_ReturnsOne()
    {
        await SaveCompleteDayAsync(Today());

        (await _service.GetCurrentStreakAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_TodayIncompleteYesterdayComplete_ReturnsOne()
    {
        // Today has only partial entry (not complete).
        await _service.SaveEntryAsync(new DailyEntry { Date = Today(), ItemId = "beans", ServingsCompleted = 1 });
        await SaveCompleteDayAsync(Today().AddDays(-1));

        (await _service.GetCurrentStreakAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_FiveConsecutiveCompleteDays_ReturnsFive()
    {
        var today = Today();
        for (var i = 0; i < 5; i++)
        {
            await SaveCompleteDayAsync(today.AddDays(-i));
        }

        (await _service.GetCurrentStreakAsync()).Should().Be(5);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_GapBreaksStreak()
    {
        var today = Today();
        await SaveCompleteDayAsync(today);
        await SaveCompleteDayAsync(today.AddDays(-1));
        // Skip day -2.
        await SaveCompleteDayAsync(today.AddDays(-3));

        (await _service.GetCurrentStreakAsync()).Should().Be(2);
    }

    [TestMethod]
    public async Task GetCurrentStreakAsync_BothChecklistsDisabled_ReturnsZero()
    {
        _prefs.DailyDozenEnabled = false;
        _prefs.TwentyOneTweaksEnabled = false;
        await SaveCompleteDayAsync(Today());

        (await _service.GetCurrentStreakAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task GetLongestStreakAsync_EmptyDb_ReturnsZero()
    {
        (await _service.GetLongestStreakAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task GetLongestStreakAsync_SingleCompleteDay_ReturnsOne()
    {
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 1));

        (await _service.GetLongestStreakAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task GetLongestStreakAsync_DisjointStreaks_ReturnsMax()
    {
        // Streak A: 3 days
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 1));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 2));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 3));
        // Gap
        // Streak B: 5 days
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 10));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 11));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 12));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 13));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 14));

        (await _service.GetLongestStreakAsync()).Should().Be(5);
    }

    [TestMethod]
    public async Task GetLongestStreakAsync_IncompleteDayResetsRunningStreak()
    {
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 1));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 2));
        // Day 3 incomplete (only one item entered, not enough)
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 3), ItemId = "beans", ServingsCompleted = 1 });
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 4));

        (await _service.GetLongestStreakAsync()).Should().Be(2);
    }

    [TestMethod]
    public async Task GetPerfectDaysCountAsync_CountsCompleteDaysOnly()
    {
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 1));
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 2));
        // Incomplete day:
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 3), ItemId = "beans", ServingsCompleted = 1 });

        (await _service.GetPerfectDaysCountAsync()).Should().Be(2);
    }

    [TestMethod]
    public async Task GetPerfectDaysCountAsync_BothChecklistsDisabled_ReturnsZero()
    {
        _prefs.DailyDozenEnabled = false;
        await SaveCompleteDayAsync(new DateOnly(2024, 1, 1));

        (await _service.GetPerfectDaysCountAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task GetItemCompletionCountAsync_CountsAtOrAboveRecommended()
    {
        var beans = ChecklistDefinitions.GetItemById("beans")!;
        var recommended = beans.RecommendedServings;

        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "beans", ServingsCompleted = recommended - 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 2), ItemId = "beans", ServingsCompleted = recommended });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 3), ItemId = "beans", ServingsCompleted = recommended + 1 });

        (await _service.GetItemCompletionCountAsync("beans")).Should().Be(2);
    }

    [TestMethod]
    public async Task GetItemCompletionCountAsync_UnknownItem_ReturnsZero()
    {
        (await _service.GetItemCompletionCountAsync("not_a_real_item")).Should().Be(0);
    }

    [TestMethod]
    public async Task GetTotalDaysTrackedAsync_ReturnsDistinctDateCount()
    {
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "berries", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 2), ItemId = "beans", ServingsCompleted = 1 });

        (await _service.GetTotalDaysTrackedAsync()).Should().Be(2);
    }

    [TestMethod]
    public async Task GetDatesWithEntriesAsync_DistinctAndOrdered()
    {
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 3), ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "beans", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "berries", ServingsCompleted = 1 });
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 2), ItemId = "beans", ServingsCompleted = 1 });

        var dates = await _service.GetDatesWithEntriesAsync();

        dates.Should().Equal(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 3));
    }

    // ===== Achievements =====

    [TestMethod]
    public async Task SaveEarnedAchievementAsync_ThenIsEarned_ReturnsTrue()
    {
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement
        {
            AchievementId = "streak_7",
            EarnedAt = DateTime.UtcNow,
        });

        (await _service.IsAchievementEarnedAsync("streak_7")).Should().BeTrue();
    }

    [TestMethod]
    public async Task SaveEarnedAchievementAsync_DuplicateIgnored()
    {
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow.AddHours(1) });

        var earned = await _service.GetEarnedAchievementsAsync();

        earned.Should().ContainSingle();
    }

    [TestMethod]
    public async Task GetEarnedAchievementsAsync_OrderedByEarnedAtDescending()
    {
        var first = DateTime.UtcNow.AddDays(-2);
        var second = DateTime.UtcNow.AddDays(-1);
        var third = DateTime.UtcNow;

        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_7", EarnedAt = first });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_14", EarnedAt = third });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_30", EarnedAt = second });

        var ordered = await _service.GetEarnedAchievementsAsync();

        ordered.Select(e => e.AchievementId).Should().Equal("streak_14", "streak_30", "streak_7");
    }

    [TestMethod]
    public async Task GetUnseenAchievementCountAsync_ReturnsUnseenOnly()
    {
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow, HasBeenSeen = false });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_14", EarnedAt = DateTime.UtcNow, HasBeenSeen = true });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_30", EarnedAt = DateTime.UtcNow, HasBeenSeen = false });

        (await _service.GetUnseenAchievementCountAsync()).Should().Be(2);
    }

    [TestMethod]
    public async Task MarkAllAchievementsAsSeenAsync_ZeroesUnseenCount()
    {
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow, HasBeenSeen = false });
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement { AchievementId = "streak_14", EarnedAt = DateTime.UtcNow, HasBeenSeen = false });

        await _service.MarkAllAchievementsAsSeenAsync();

        (await _service.GetUnseenAchievementCountAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task IsAchievementEarnedAsync_NotSaved_ReturnsFalse()
    {
        (await _service.IsAchievementEarnedAsync("streak_7")).Should().BeFalse();
    }

    // ===== Initialization / re-use =====

    [TestMethod]
    public async Task InitializeAsync_IsIdempotent_AllowsMultipleCalls()
    {
        await _service.InitializeAsync();
        await _service.InitializeAsync();

        // Should not throw and basic ops still work.
        await _service.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2024, 1, 1), ItemId = "beans", ServingsCompleted = 1 });
        var entries = await _service.GetEntriesForDateAsync(new DateOnly(2024, 1, 1));
        entries.Should().ContainSingle();
    }
}
