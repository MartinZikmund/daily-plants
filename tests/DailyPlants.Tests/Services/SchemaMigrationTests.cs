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
    /// Rewinds a fully migrated database to the v2 shape by putting user_version back.
    /// </summary>
    private async Task RewindToV2Async()
    {
        var raw = new SQLiteAsyncConnection(_dbPath);
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
    public async Task Initialize_OnAV2Database_UpgradesToTheLatestVersion()
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
        after.Should().Be(before, "the upgrade must not change the numbers an existing user already sees");
        after.Should().Be(3);
    }

    private async Task SetUserVersionAsync(int version)
    {
        var raw = new SQLiteAsyncConnection(_dbPath);
        await raw.ExecuteAsync($"PRAGMA user_version = {version}");
        await raw.CloseAsync();
    }

    // ===== v3: canonical units =====
    //
    // Before v3, weights and heights were stored in whatever unit the user had selected.
    // The upgrade reinterprets an imperial user's existing values as pounds and inches
    // and rewrites them as kilograms and centimetres.

    [TestMethod]
    public async Task Initialize_ForAnImperialUser_ConvertsStoredWeightsToKilograms()
    {
        var prefs = new FakeAppPreferences { UseMetricUnits = false, WeightTrackingEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var date = new DateOnly(2026, 4, 1);
        // A pre-v3 database holds the raw pounds the user typed.
        await seed.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 176.4 });
        await SetUserVersionAsync(2);

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        var entry = await upgraded.GetWeightEntryAsync(date);
        entry!.Weight.Should().BeApproximately(80, 0.05, "176.4 lb is 80 kg");
    }

    [TestMethod]
    public async Task Initialize_ForAnImperialUser_ConvertsHeightAndGoalWeightPreferences()
    {
        var prefs = new FakeAppPreferences { UseMetricUnits = false, WeightTrackingEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        await SetUserVersionAsync(2);
        // Pre-v4 preferences hold the raw numbers the user typed.
        prefs.HeightCm = 70;      // inches
        prefs.GoalWeight = 176.4; // pounds

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        prefs.HeightCm.Should().BeApproximately(177.8, 0.05);
        prefs.GoalWeight.Should().BeApproximately(80, 0.05);
    }

    [TestMethod]
    public async Task Initialize_ForAMetricUser_LeavesStoredValuesAlone()
    {
        var prefs = new FakeAppPreferences
        {
            UseMetricUnits = true,
            WeightTrackingEnabled = true,
            HeightCm = 178,
            GoalWeight = 80
        };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var date = new DateOnly(2026, 4, 1);
        await seed.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 80 });
        await SetUserVersionAsync(2);

        var upgraded = new SqliteDataService(prefs, _dbPath);
        await upgraded.InitializeAsync();

        (await upgraded.GetWeightEntryAsync(date))!.Weight.Should().Be(80);
        prefs.HeightCm.Should().Be(178);
        prefs.GoalWeight.Should().Be(80);
    }

    [TestMethod]
    public async Task Initialize_RunTwice_DoesNotConvertTwice()
    {
        var prefs = new FakeAppPreferences { UseMetricUnits = false, WeightTrackingEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var date = new DateOnly(2026, 4, 1);
        await seed.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 176.4 });
        await SetUserVersionAsync(2);
        prefs.HeightCm = 70; // inches

        await new SqliteDataService(prefs, _dbPath).InitializeAsync();
        await new SqliteDataService(prefs, _dbPath).InitializeAsync();

        var entry = await new SqliteDataService(prefs, _dbPath).GetWeightEntryAsync(date);
        entry!.Weight.Should().BeApproximately(80, 0.05, "the migration is gated on user_version and must be idempotent");
        prefs.HeightCm.Should().BeApproximately(177.8, 0.05);
    }

    // ===== Crash safety =====

    /// <summary>
    /// Preferences that blow up part way through the v4 conversion, standing in for the
    /// process being killed after the weight UPDATE but before the version bump.
    /// </summary>
    private sealed class ThrowingPreferences : IAppPreferences
    {
        public bool DailyDozenEnabled { get; set; } = true;
        public bool TwentyOneTweaksEnabled { get; set; }
        public bool WeightTrackingEnabled { get; set; } = true;
        public bool UseMetricUnits { get; set; }
        public double? GoalWeight { get; set; }
        public int ThemePreference { get; set; }
        public string? Language { get; set; }
        public string DisabledItemIds { get; set; } = string.Empty;

        public double? HeightCm
        {
            get => throw new InvalidOperationException("preference store went away mid-migration");
            set => throw new InvalidOperationException("preference store went away mid-migration");
        }
    }

    [TestMethod]
    public async Task Initialize_WhenAMigrationFailsPartWayThrough_RollsBackBothTheWorkAndTheVersion()
    {
        var prefs = new FakeAppPreferences { UseMetricUnits = false, WeightTrackingEnabled = true };
        var seed = new SqliteDataService(prefs, _dbPath);
        await seed.InitializeAsync();
        var date = new DateOnly(2026, 4, 1);
        await seed.SaveWeightEntryAsync(new WeightEntry { Date = date, Weight = 176.4 });
        await SetUserVersionAsync(2);

        // v3 divides the weights, then reads HeightCm - which throws.
        var failing = new SqliteDataService(new ThrowingPreferences(), _dbPath);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(failing.InitializeAsync);

        (await ReadUserVersionAsync(_dbPath)).Should().Be(2, "the failed migration must not claim to have run");
        var entry = await new SqliteDataService(prefs, _dbPath).GetWeightEntryAsync(date);
        entry!.Weight.Should().BeApproximately(
            80,
            0.05,
            "the retry converts the untouched pounds exactly once, rather than halving an already-converted value");
    }
}
