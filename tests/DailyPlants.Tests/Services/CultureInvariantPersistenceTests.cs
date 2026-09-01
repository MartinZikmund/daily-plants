using System.Globalization;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// The app switches <see cref="CultureInfo.CurrentCulture"/> at runtime (LocalizationService)
/// and ships locales whose default calendar is not Gregorian (fa). Every date that round-trips
/// through storage or export must therefore use the invariant culture.
/// </summary>
[TestClass]
public class CultureInvariantPersistenceTests
{
    private string _dbPath = string.Empty;
    private CultureInfo _originalCulture = null!;

    [TestInitialize]
    public void Initialize()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Culture-{Guid.NewGuid():N}.db");
        _originalCulture = CultureInfo.CurrentCulture;
    }

    [TestCleanup]
    public void Cleanup()
    {
        CultureInfo.CurrentCulture = _originalCulture;

        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // SQLite handle may still be released asynchronously; ignore.
        }
    }

    [TestMethod]
    public async Task DailyEntry_WrittenUnderPersianCulture_IsReadableUnderInvariantCulture()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        var date = new DateOnly(2026, 4, 1);

        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var writer = new SqliteDataService(prefs, _dbPath);
        await writer.InitializeAsync();
        await writer.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 2 });

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var reader = new SqliteDataService(prefs, _dbPath);
        var entry = await reader.GetEntryAsync(date, "beans");

        entry.Should().NotBeNull("switching app language must not orphan existing entries");
        entry!.Date.Should().Be(date);
    }

    [TestMethod]
    public async Task GetEntriesInRangeAsync_SpanningACultureSwitch_ReturnsBothEntries()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };

        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var writer = new SqliteDataService(prefs, _dbPath);
        await writer.InitializeAsync();
        await writer.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2026, 4, 1), ItemId = "beans", ServingsCompleted = 2 });

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var reader = new SqliteDataService(prefs, _dbPath);
        await reader.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2026, 4, 2), ItemId = "beans", ServingsCompleted = 3 });

        var entries = await reader.GetEntriesInRangeAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 1));

        entries.Should().HaveCount(2, "entries written before and after a language change share one date range");
    }

    [TestMethod]
    public async Task WeightEntry_WrittenUnderPersianCulture_IsReadableUnderInvariantCulture()
    {
        var prefs = new FakeAppPreferences { WeightTrackingEnabled = true };
        var date = new DateOnly(2026, 4, 1);

        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var writer = new SqliteDataService(prefs, _dbPath);
        await writer.InitializeAsync();
        await writer.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 80 });

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var reader = new SqliteDataService(prefs, _dbPath);
        var entry = await reader.GetWeightEntryAsync(date);

        entry.Should().NotBeNull("a date written under one culture must be readable under another");
        entry!.Weight.Should().Be(80);
    }

    [TestMethod]
    public async Task GetEarnedAchievementsAsync_UnderPersianCalendarCulture_ParsesTimestamp()
    {
        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var prefs = new FakeAppPreferences();
        var service = new SqliteDataService(prefs, _dbPath);
        await service.InitializeAsync();
        var earnedAt = new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc);

        await service.SaveEarnedAchievementAsync(new EarnedAchievement
        {
            AchievementId = "milestone_first_day",
            EarnedAt = earnedAt,
            HasBeenSeen = false
        });
        var earned = await service.GetEarnedAchievementsAsync();

        earned.Should().ContainSingle();
        earned[0].EarnedAt.Should().BeCloseTo(earnedAt, TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task ImportFromJsonAsync_UnderPersianCalendarCulture_ImportsIsoDates()
    {
        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        var prefs = new FakeAppPreferences();
        var data = new InMemoryDataService(prefs);
        var service = new ExportService(data, prefs);
        var json = """
        {
          "version": "1.0",
          "dailyEntries": [ { "date": "2026-04-01", "itemId": "beans", "servingsCompleted": 3 } ],
          "weightEntries": []
        }
        """;

        var result = await service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(1);
        var entry = await data.GetEntryAsync(new DateOnly(2026, 4, 1), "beans");
        entry.Should().NotBeNull();
        entry!.ServingsCompleted.Should().Be(3);
    }
}
