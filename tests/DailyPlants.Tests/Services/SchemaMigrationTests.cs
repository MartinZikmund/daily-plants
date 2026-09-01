using DailyPlants.Tests.TestDoubles;
using SQLite;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Upgrade paths for databases created by already-shipped versions. A migration that
/// loses or rewrites a user's history is the worst failure this app can have, so each
/// version bump gets a test that starts from the older shape.
/// </summary>
[TestClass]
public class SchemaMigrationTests
{
    private string _dbPath = string.Empty;

    [TestInitialize]
    public void Initialize() =>
        _dbPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-Migration-{Guid.NewGuid():N}.db");

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

    /// <summary>
    /// Rewinds a fully migrated database to the v2 shape: no snapshot table, user_version 2.
    /// </summary>
    private async Task RewindToV2Async()
    {
        var raw = new SQLiteAsyncConnection(_dbPath);
        await raw.ExecuteAsync("DROP TABLE IF EXISTS DailySettingsSnapshots");
        await raw.ExecuteAsync("PRAGMA user_version = 2");
        await raw.CloseAsync();
    }

    private static async Task<int> ReadUserVersionAsync(string path)
    {
        var raw = new SQLiteAsyncConnection(path);
        var version = await raw.ExecuteScalarAsync<int>("PRAGMA user_version");
        await raw.CloseAsync();
        return version;
    }

    [TestMethod]
    public async Task Initialize_OnAV2Database_UpgradesToV3()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        await RewindToV2Async();

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        (await ReadUserVersionAsync(_dbPath)).Should().Be(3);
    }

    [TestMethod]
    public async Task Initialize_OnAV2Database_PreservesExistingEntries()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var date = new DateOnly(2026, 4, 1);
        await seed.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 3 });
        await seed.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 80 });
        await RewindToV2Async();

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        (await upgraded.GetEntryAsync(date, "beans"))!.ServingsCompleted.Should().Be(3);
        (await upgraded.GetWeightEntryAsync(date))!.Weight.Should().Be(80);
    }

    [TestMethod]
    public async Task Initialize_OnAV2Database_KeepsPerfectDaysStableAcrossTheUpgrade()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        prefs.DisabledItemIds = string.Join(',', ChecklistDefinitions.AllItems
            .Where(i => i.Id != "beans")
            .Select(i => i.Id));

        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < 3; i++)
        {
            await seed.SaveEntryAsync(new DailyEntry { Date = today.AddDays(-i), ItemId = "beans", ServingsCompleted = 3 });
        }
        var before = await seed.GetPerfectDaysCountAsync();
        await RewindToV2Async();

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        var after = await upgraded.GetPerfectDaysCountAsync();
        after.Should().Be(before, "the backfill must not change the numbers an existing user already sees");
        after.Should().Be(3);
    }

    [TestMethod]
    public async Task Initialize_OnAV2Database_BackfillsHistorySoLaterSettingsChangesDoNotRewriteIt()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true, TwentyOneTweaksEnabled = false };
        prefs.DisabledItemIds = string.Join(',', ChecklistDefinitions.AllItems
            .Where(i => i.Id != "beans" && i.Id != "green_tea")
            .Select(i => i.Id));

        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < 3; i++)
        {
            await seed.SaveEntryAsync(new DailyEntry { Date = today.AddDays(-i), ItemId = "beans", ServingsCompleted = 3 });
        }
        await RewindToV2Async();

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();
        prefs.TwentyOneTweaksEnabled = true;

        var perfectDays = await upgraded.GetPerfectDaysCountAsync();

        perfectDays.Should().Be(3, "pre-migration history is backfilled and then frozen against later changes");
    }
}
