using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Export is the app's only backup, so a restore has to bring back everything the
/// user would notice losing — and an import must not damage the database it is
/// writing into.
/// </summary>
[TestClass]
public class ExportRoundTripTests
{
    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _data = null!;
    private ExportService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences();
        _data = new InMemoryDataService(_prefs);
        _service = new ExportService(_data, _prefs);
    }

    private static DateOnly Date(int day) => new(2026, 4, day);

    // ===== Completeness =====

    [TestMethod]
    public async Task RoundTrip_RestoresEarnedAchievements()
    {
        await _data.SaveEarnedAchievementAsync(new EarnedAchievement
        {
            AchievementId = "milestone_first_day",
            EarnedAt = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            HasBeenSeen = true
        });
        var json = await _service.ExportToJsonAsync();

        var restoredData = new InMemoryDataService(_prefs);
        var restored = new ExportService(restoredData, _prefs);
        var result = await restored.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        var achievements = await restoredData.GetEarnedAchievementsAsync();
        achievements.Should().ContainSingle().Which.AchievementId.Should().Be("milestone_first_day");
    }

    [TestMethod]
    public async Task RoundTrip_RestoresDisabledItems()
    {
        _prefs.DisabledItemIds = "beans,berries";
        var json = await _service.ExportToJsonAsync();

        var restoredPrefs = new FakeAppPreferences();
        var restored = new ExportService(new InMemoryDataService(restoredPrefs), restoredPrefs);
        await restored.ImportFromJsonAsync(json);

        restoredPrefs.GetDisabledItemIdSet().Should().BeEquivalentTo(["beans", "berries"]);
    }

    [TestMethod]
    public async Task RoundTrip_RestoresLanguage()
    {
        _prefs.Language = "cs";
        var json = await _service.ExportToJsonAsync();

        var restoredPrefs = new FakeAppPreferences();
        var restored = new ExportService(new InMemoryDataService(restoredPrefs), restoredPrefs);
        await restored.ImportFromJsonAsync(json);

        restoredPrefs.Language.Should().Be("cs");
    }

    // ===== Validation =====

    [TestMethod]
    public async Task Import_WithUnknownItemId_SkipsTheRowAndReportsIt()
    {
        var json = """
        {
          "version": "1.1",
          "dailyEntries": [
            { "date": "2026-04-01", "itemId": "beans", "servingsCompleted": 3 },
            { "date": "2026-04-01", "itemId": "definitely_not_an_item", "servingsCompleted": 3 }
          ],
          "weightEntries": []
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(1);
        result.EntriesSkipped.Should().Be(1, "the user must be told something was dropped");
        (await _data.GetEntryAsync(Date(1), "definitely_not_an_item")).Should().BeNull();
    }

    [TestMethod]
    public async Task Import_WithNegativeServings_SkipsTheRow()
    {
        var json = """
        {
          "version": "1.1",
          "dailyEntries": [ { "date": "2026-04-01", "itemId": "beans", "servingsCompleted": -5 } ],
          "weightEntries": []
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.EntriesImported.Should().Be(0);
        result.EntriesSkipped.Should().Be(1);
    }

    [TestMethod]
    public async Task Import_WithUnparseableDate_SkipsTheRow()
    {
        var json = """
        {
          "version": "1.1",
          "dailyEntries": [ { "date": "not-a-date", "itemId": "beans", "servingsCompleted": 3 } ],
          "weightEntries": []
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.EntriesImported.Should().Be(0);
        result.EntriesSkipped.Should().Be(1);
    }

    [TestMethod]
    public async Task Import_WithAFutureFormatVersion_IsRejected()
    {
        var json = """
        {
          "version": "9.9",
          "dailyEntries": [ { "date": "2026-04-01", "itemId": "beans", "servingsCompleted": 3 } ],
          "weightEntries": []
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.Success.Should().BeFalse("a newer file may mean things this version would misread");
        (await _data.GetEntryAsync(Date(1), "beans")).Should().BeNull("nothing may be written when the file is rejected");
    }

    // ===== Units across format versions =====

    [TestMethod]
    public async Task Import_OfA10FileFromAnImperialUser_ConvertsWeightsToKilograms()
    {
        // 1.0 files stored whatever unit the exporting user had selected.
        var json = """
        {
          "version": "1.0",
          "dailyEntries": [],
          "weightEntries": [ { "date": "2026-04-01", "weight": 176.4 } ],
          "settings": { "useMetricUnits": false, "heightCm": 70, "goalWeight": 176.4 }
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        var entry = await _data.GetWeightEntryAsync(Date(1));
        entry!.Weight.Should().BeApproximately(80, 0.05);
        _prefs.HeightCm.Should().BeApproximately(177.8, 0.05);
        _prefs.GoalWeight.Should().BeApproximately(80, 0.05);
    }

    [TestMethod]
    public async Task Import_OfA11File_TakesWeightsAsKilograms()
    {
        var json = """
        {
          "version": "1.1",
          "dailyEntries": [],
          "weightEntries": [ { "date": "2026-04-01", "weight": 80 } ],
          "settings": { "useMetricUnits": false, "heightCm": 177.8, "goalWeight": 80 }
        }
        """;

        await _service.ImportFromJsonAsync(json);

        (await _data.GetWeightEntryAsync(Date(1)))!.Weight.Should().Be(80);
        _prefs.HeightCm.Should().Be(177.8);
    }

    [TestMethod]
    public async Task Import_OfAFileWithNoVersion_TakesItAsTheLegacyFormat()
    {
        // Nothing this app has shipped writes a file without a version, so one that turns
        // up predates the field - which means its numbers are in the exporter's own units.
        var json = """
        {
          "dailyEntries": [],
          "weightEntries": [ { "date": "2026-04-01", "weight": 176.4 } ],
          "settings": { "useMetricUnits": false, "heightCm": 70, "goalWeight": 176.4 }
        }
        """;

        await _service.ImportFromJsonAsync(json);

        (await _data.GetWeightEntryAsync(Date(1)))!.Weight.Should().BeApproximately(
            80, 0.05, "176.4 lb is 80 kg - reading it as kilograms would be wrong by 2.2x");
        _prefs.HeightCm.Should().BeApproximately(177.8, 0.05);
    }

    [TestMethod]
    public async Task Import_OfAFileWithANullVersion_TakesItAsTheLegacyFormat()
    {
        var json = """
        {
          "version": null,
          "dailyEntries": [],
          "weightEntries": [ { "date": "2026-04-01", "weight": 176.4 } ],
          "settings": { "useMetricUnits": false }
        }
        """;

        var result = await _service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        (await _data.GetWeightEntryAsync(Date(1)))!.Weight.Should().BeApproximately(80, 0.05);
    }

    [TestMethod]
    public async Task Export_DeclaresTheCurrentFormatVersion()
    {
        var json = await _service.ExportToJsonAsync();

        json.Should().Contain("\"version\": \"1.1\"");
    }
}
