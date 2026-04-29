using System.Text.Json;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class ExportServiceTests
{
    private static (ExportService service, InMemoryDataService data, FakeAppPreferences prefs) NewService()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        var data = new InMemoryDataService(prefs);
        var export = new ExportService(data, prefs);
        return (export, data, prefs);
    }

    private static async Task SeedSampleAsync(InMemoryDataService data)
    {
        await data.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2026, 4, 1), ItemId = "beans", ServingsCompleted = 3 });
        await data.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2026, 4, 1), ItemId = "berries", ServingsCompleted = 1 });
        await data.SaveEntryAsync(new DailyEntry { Date = new DateOnly(2026, 4, 2), ItemId = "beans", ServingsCompleted = 2 });
        await data.SaveWeightEntryAsync(new WeightEntry { Date = new DateOnly(2026, 4, 1), Weight = 72.5, Notes = "after run" });
    }

    [TestMethod]
    public async Task ExportToJsonAsync_ProducesValidJsonWithExpectedShape()
    {
        var (service, data, _) = NewService();
        await SeedSampleAsync(data);

        var json = await service.ExportToJsonAsync();

        json.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("version").GetString().Should().Be("1.0");
        doc.RootElement.TryGetProperty("exportDate", out _).Should().BeTrue();
        doc.RootElement.GetProperty("dailyEntries").GetArrayLength().Should().Be(3);
        doc.RootElement.GetProperty("weightEntries").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("settings").GetProperty("dailyDozenEnabled").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task ExportImport_JsonRoundTripPreservesEntries()
    {
        var (sourceSvc, sourceData, _) = NewService();
        await SeedSampleAsync(sourceData);
        var json = await sourceSvc.ExportToJsonAsync();

        var (targetSvc, targetData, _) = NewService();
        var result = await targetSvc.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(3);
        result.WeightEntriesImported.Should().Be(1);

        var imported = await targetData.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        imported.Should().HaveCount(3);
        imported.Should().Contain(e => e.Date == new DateOnly(2026, 4, 1) && e.ItemId == "beans" && e.ServingsCompleted == 3);
        imported.Should().Contain(e => e.Date == new DateOnly(2026, 4, 1) && e.ItemId == "berries" && e.ServingsCompleted == 1);
        imported.Should().Contain(e => e.Date == new DateOnly(2026, 4, 2) && e.ItemId == "beans" && e.ServingsCompleted == 2);

        var weights = await targetData.GetAllWeightEntriesAsync();
        weights.Should().ContainSingle();
        weights[0].Weight.Should().Be(72.5);
        weights[0].Notes.Should().Be("after run");
    }

    [TestMethod]
    public async Task ImportFromJsonAsync_AppliesSettings()
    {
        var (sourceSvc, sourceData, sourcePrefs) = NewService();
        sourcePrefs.DailyDozenEnabled = false;
        sourcePrefs.TwentyOneTweaksEnabled = true;
        sourcePrefs.WeightTrackingEnabled = true;
        sourcePrefs.UseMetricUnits = false;
        sourcePrefs.HeightCm = 180.0;
        sourcePrefs.GoalWeight = 75.0;
        sourcePrefs.ThemePreference = 2;
        await SeedSampleAsync(sourceData);
        var json = await sourceSvc.ExportToJsonAsync();

        var (targetSvc, _, targetPrefs) = NewService();
        await targetSvc.ImportFromJsonAsync(json);

        targetPrefs.DailyDozenEnabled.Should().BeFalse();
        targetPrefs.TwentyOneTweaksEnabled.Should().BeTrue();
        targetPrefs.WeightTrackingEnabled.Should().BeTrue();
        targetPrefs.UseMetricUnits.Should().BeFalse();
        targetPrefs.HeightCm.Should().Be(180.0);
        targetPrefs.GoalWeight.Should().Be(75.0);
        targetPrefs.ThemePreference.Should().Be(2);
    }

    [TestMethod]
    public async Task ImportFromJsonAsync_MalformedJson_ReturnsFailureWithErrorMessage()
    {
        var (service, data, _) = NewService();

        var result = await service.ImportFromJsonAsync("{ this is not valid json");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        var entries = await data.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        entries.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ImportFromJsonAsync_EmptyDocument_ReturnsSuccessWithZeroEntries()
    {
        var (service, _, _) = NewService();

        var json = "{ \"version\": \"1.0\", \"exportDate\": \"2026-04-27T00:00:00Z\", \"dailyEntries\": [], \"weightEntries\": [] }";
        var result = await service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(0);
        result.WeightEntriesImported.Should().Be(0);
    }

    [TestMethod]
    public async Task ImportFromJsonAsync_SkipsEntriesWithInvalidDates()
    {
        var (service, data, _) = NewService();

        var json = "{ \"version\": \"1.0\", \"dailyEntries\": [" +
                   "{ \"date\": \"not-a-date\", \"itemId\": \"beans\", \"servingsCompleted\": 1 }," +
                   "{ \"date\": \"2026-04-27\", \"itemId\": \"beans\", \"servingsCompleted\": 2 }" +
                   "], \"weightEntries\": [] }";

        var result = await service.ImportFromJsonAsync(json);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(1);

        var entries = await data.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        entries.Should().ContainSingle();
        entries[0].Date.Should().Be(new DateOnly(2026, 4, 27));
    }

    [TestMethod]
    public async Task ExportToCsvAsync_ContainsHeaderAndAllEntries()
    {
        var (service, data, _) = NewService();
        await SeedSampleAsync(data);

        var csv = await service.ExportToCsvAsync();

        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(4); // header + 3 entries
        lines[0].Should().Be("Date,ItemId,ItemName,ServingsCompleted,RecommendedServings");
        lines[1].Should().Contain("2026-04-01,beans");
    }

    [TestMethod]
    public async Task ImportFromCsvAsync_ParsesEntries()
    {
        var (service, data, _) = NewService();

        var csv = "Date,ItemId,ItemName,ServingsCompleted,RecommendedServings\n" +
                  "2026-04-01,beans,Beans,3,3\n" +
                  "2026-04-01,berries,Berries,1,1\n";

        var result = await service.ImportFromCsvAsync(csv);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(2);

        var entries = await data.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        entries.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task ImportFromCsvAsync_HandlesQuotedFieldsWithCommas()
    {
        var (service, data, _) = NewService();

        // ItemName quoted with embedded comma — must not split incorrectly.
        var csv = "Date,ItemId,ItemName,ServingsCompleted,RecommendedServings\n" +
                  "2026-04-01,beans,\"Beans, lentils, peas\",3,3\n";

        var result = await service.ImportFromCsvAsync(csv);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(1);
        var entries = await data.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        entries.Should().ContainSingle().Which.ItemId.Should().Be("beans");
    }

    [TestMethod]
    public async Task ImportFromCsvAsync_EmptyInput_ReturnsFailure()
    {
        var (service, _, _) = NewService();

        var result = await service.ImportFromCsvAsync(string.Empty);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task ImportFromCsvAsync_HeaderOnly_ReturnsFailure()
    {
        var (service, _, _) = NewService();

        var result = await service.ImportFromCsvAsync("Date,ItemId,ItemName,ServingsCompleted,RecommendedServings\n");

        result.Success.Should().BeFalse();
    }

    [TestMethod]
    public async Task ImportFromCsvAsync_SkipsRowsWithInvalidServings()
    {
        var (service, data, _) = NewService();

        var csv = "Date,ItemId,ItemName,ServingsCompleted,RecommendedServings\n" +
                  "2026-04-01,beans,Beans,not_a_number,3\n" +
                  "2026-04-01,berries,Berries,1,1\n";

        var result = await service.ImportFromCsvAsync(csv);

        result.Success.Should().BeTrue();
        result.EntriesImported.Should().Be(1);

        var entries = await data.GetEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        entries.Should().ContainSingle().Which.ItemId.Should().Be("berries");
    }
}
