using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Data written by earlier versions: B12 used to be required for a complete day, and Stay
/// Hydrated used to be folded into Beverages, with the sixth glass stored as stay_hydrated.
/// </summary>
[TestClass]
public class UpgradeCompatibilityTests
{
    private string _dbPath = string.Empty;
    private FakeAppPreferences _prefs = null!;
    private SqliteDataService _service = null!;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Upgrade-{Guid.NewGuid():N}.db");
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true, TwentyOneTweaksEnabled = false };
        _service = new SqliteDataService(_prefs, _dbPath);
        await _service.InitializeAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // SQLite may still be releasing its handle.
        }
    }

    private async Task SaveDayAsync(DateOnly date, bool withB12)
    {
        foreach (var item in ChecklistDefinitions.GetEnabledItems(_prefs))
        {
            if (item.Id != "vitamin_b12" || withB12)
            {
                await _service.SaveEntryAsync(new DailyEntry { Date = date, ItemId = item.Id, ServingsCompleted = item.RecommendedServings });
            }
        }
    }

    [TestMethod]
    public async Task DayCompletedWithB12_StaysComplete()
    {
        await SaveDayAsync(Today, withB12: true);

        (await _service.GetPerfectDaysCountAsync()).Should().Be(1);
        (await _service.GetCurrentStreakAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task DayMissingOnlyB12_NowCompletesAndJoinsTheStreak()
    {
        await SaveDayAsync(Today.AddDays(-2), withB12: true);
        await SaveDayAsync(Today.AddDays(-1), withB12: false);
        await SaveDayAsync(Today, withB12: true);

        (await _service.GetCurrentStreakAsync()).Should().Be(3);
        (await _service.GetLongestStreakAsync()).Should().Be(3);
        (await _service.GetPerfectDaysCountAsync()).Should().Be(3);
    }

    [TestMethod]
    public async Task StoredB12Rows_AreKept()
    {
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "vitamin_b12", ServingsCompleted = 1 });

        (await _service.GetEntryAsync(Today, "vitamin_b12"))!.ServingsCompleted.Should().Be(1);
    }

    [TestMethod]
    public async Task B12DisabledByTheUser_StillBehavesTheSame()
    {
        _prefs.SetItemDisabled("vitamin_b12", true);

        await SaveDayAsync(Today, withB12: false);

        (await _service.GetCurrentStreakAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task MergeEraDay_WithSixthGlass_StaysComplete()
    {
        _prefs.TwentyOneTweaksEnabled = true;
        await SaveDayAsync(Today, withB12: true);

        (await _service.GetEntryAsync(Today, "beverages"))!.ServingsCompleted.Should().Be(5);
        (await _service.GetEntryAsync(Today, "stay_hydrated"))!.ServingsCompleted.Should().Be(1);
        (await _service.GetPerfectDaysCountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task MergeEraDay_WithFiveGlasses_StaysIncomplete()
    {
        _prefs.TwentyOneTweaksEnabled = true;
        await SaveDayAsync(Today, withB12: true);
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "stay_hydrated", ServingsCompleted = 0 });

        (await _service.GetPerfectDaysCountAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task MergeEraRows_ShowOnTheirOwnRows()
    {
        _prefs.TwentyOneTweaksEnabled = true;
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "beverages", ServingsCompleted = 5 });
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "stay_hydrated", ServingsCompleted = 1 });

        DiaryViewModel vm = new(_service, _prefs);
        await vm.LoadDataAsync();

        var beverages = vm.Items.Single(i => i.Item.Id == "beverages");
        beverages.ServingsCompleted.Should().Be(5);
        beverages.IsComplete.Should().BeTrue();

        var hydrated = vm.Items.Single(i => i.Item.Id == "stay_hydrated");
        hydrated.ServingsCompleted.Should().Be(1);
        hydrated.IsComplete.Should().BeTrue();
    }

    [TestMethod]
    public async Task StoredCountAboveTheTarget_StaysCompleteAndCanBeLowered()
    {
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "beverages", ServingsCompleted = 6 });

        DiaryViewModel vm = new(_service, _prefs);
        await vm.LoadDataAsync();

        var beverages = vm.Items.Single(i => i.Item.Id == "beverages");
        beverages.IsComplete.Should().BeTrue();

        beverages.ServingsCompleted = 4;
        await Task.Delay(50);

        (await _service.GetEntryAsync(Today, "beverages"))!.ServingsCompleted.Should().Be(4);
    }

    [TestMethod]
    public async Task OrphanedRowsFromRemovedItems_AreKeptAndIgnored()
    {
        await SaveDayAsync(Today, withB12: true);
        await _service.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = "more_legumes", ServingsCompleted = 1 });

        DiaryViewModel vm = new(_service, _prefs);
        await vm.LoadDataAsync();

        vm.Items.Should().NotContain(i => i.Item.Id == "more_legumes");
        (await _service.GetEntryAsync(Today, "more_legumes")).Should().NotBeNull();
        (await _service.GetPerfectDaysCountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task EarnedAchievements_AreNotRevoked()
    {
        await _service.SaveEarnedAchievementAsync(new EarnedAchievement
        {
            AchievementId = "streak_7",
            EarnedAt = DateTime.UtcNow,
            HasBeenSeen = true
        });

        AchievementService achievements = new(_service);
        await achievements.CheckAndAwardAchievementsAsync();

        (await _service.IsAchievementEarnedAsync("streak_7")).Should().BeTrue();
    }

    [TestMethod]
    public async Task ExistingDatabase_ReopensWithItsData()
    {
        await SaveDayAsync(Today, withB12: true);

        SqliteDataService reopened = new(_prefs, _dbPath);
        await reopened.InitializeAsync();

        (await reopened.GetPerfectDaysCountAsync()).Should().Be(1);
    }
}
