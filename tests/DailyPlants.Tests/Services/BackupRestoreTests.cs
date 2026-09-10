using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Restoring a backup onto a second device has to reproduce what the first device
/// showed. These run against the real <see cref="SqliteDataService"/> rather than a
/// double, because what they are guarding is the per-date settings snapshot — and a
/// snapshot is written once and never rewritten, so getting it wrong is permanent.
/// </summary>
[TestClass]
public class BackupRestoreTests
{
    private readonly List<string> _databases = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in _databases)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // SQLite releases its handle asynchronously; a leftover temp file is harmless.
            }
        }
    }

    private async Task<SqliteDataService> NewDeviceAsync(IAppPreferences prefs)
    {
        var path = Path.Combine(Path.GetTempPath(), $"DailyPlants-Restore-{Guid.NewGuid():N}.db");
        _databases.Add(path);

        var service = new SqliteDataService(prefs, path);
        await service.InitializeAsync();
        return service;
    }

    private static async Task CompleteDayAsync(SqliteDataService service, IAppPreferences prefs, DateOnly date)
    {
        foreach (var item in ChecklistDefinitions.GetEnabledItems(prefs))
        {
            await service.SaveEntryAsync(new DailyEntry
            {
                Date = date,
                ItemId = item.Id,
                ServingsCompleted = item.RecommendedServings
            });
        }
    }

    [TestMethod]
    public async Task Restore_OntoStockDefaults_KeepsThePerfectDaysTheSourceDeviceHad()
    {
        // Source device: two items the user never eats are switched off.
        var sourcePrefs = new FakeAppPreferences { DisabledItemIds = "flaxseed,nuts" };
        var source = await NewDeviceAsync(sourcePrefs);
        await CompleteDayAsync(source, sourcePrefs, new DateOnly(2026, 4, 1));

        var expected = await source.GetPerfectDaysCountAsync();
        expected.Should().Be(1, "the source device counts the day as complete");

        var backup = await new ExportService(source, sourcePrefs).ExportToJsonAsync();

        // Fresh install: nothing disabled, so its requirements are stricter than the file's.
        var restoredPrefs = new FakeAppPreferences();
        var restored = await NewDeviceAsync(restoredPrefs);
        var result = await new ExportService(restored, restoredPrefs).ImportFromJsonAsync(backup);

        result.Success.Should().BeTrue();
        (await restored.GetPerfectDaysCountAsync()).Should().Be(
            expected,
            "the day was judged against the settings in the backup, not the importing device's");
    }

    [TestMethod]
    public async Task Restore_FromADeviceWithMoreChecklists_DoesNotInflateTheHistory()
    {
        // Source device tracked both checklists, so its bar was higher.
        var sourcePrefs = new FakeAppPreferences { TwentyOneTweaksEnabled = true };
        var source = await NewDeviceAsync(sourcePrefs);
        var day = new DateOnly(2026, 4, 1);

        // Only the Daily Dozen was actually completed - the tweaks were left undone.
        var dozenPrefs = new FakeAppPreferences();
        await CompleteDayAsync(source, dozenPrefs, day);

        var expected = await source.GetPerfectDaysCountAsync();
        expected.Should().Be(0, "the tweaks were never logged, so the day is not perfect");

        var backup = await new ExportService(source, sourcePrefs).ExportToJsonAsync();

        var restoredPrefs = new FakeAppPreferences();
        var restored = await NewDeviceAsync(restoredPrefs);
        var import = await new ExportService(restored, restoredPrefs).ImportFromJsonAsync(backup);
        import.Success.Should().BeTrue("a failed import would leave an empty database that also counts zero");

        (await restored.GetPerfectDaysCountAsync()).Should().Be(
            expected,
            "a restore must not hand the user perfect days the source device never gave them");
    }

    [TestMethod]
    public async Task Restore_ThatFails_LeavesTheUsersOwnSettingsInPlace()
    {
        var sourcePrefs = new FakeAppPreferences { TwentyOneTweaksEnabled = true, Language = "cs" };
        var source = await NewDeviceAsync(sourcePrefs);
        await CompleteDayAsync(source, sourcePrefs, new DateOnly(2026, 4, 1));
        var backup = await new ExportService(source, sourcePrefs).ExportToJsonAsync();

        var ownPrefs = new FakeAppPreferences { TwentyOneTweaksEnabled = false, Language = "en" };
        var target = await NewDeviceAsync(ownPrefs);
        var failing = new FailingDataService(target) { FailWrites = true };

        var result = await new ExportService(failing, ownPrefs).ImportFromJsonAsync(backup);

        result.Success.Should().BeFalse();
        ownPrefs.Language.Should().Be("en", "a failed import must not leave the file's settings behind");
        ownPrefs.TwentyOneTweaksEnabled.Should().BeFalse();
    }

    [TestMethod]
    public async Task Restore_WhereApplyingSettingsThrowsPartWay_PutsTheUsersOwnSettingsBack()
    {
        var sourcePrefs = new FakeAppPreferences { TwentyOneTweaksEnabled = true, ThemePreference = 2, Language = "cs" };
        var source = await NewDeviceAsync(sourcePrefs);
        var backup = await new ExportService(source, sourcePrefs).ExportToJsonAsync();

        var ownPrefs = new ThrowsOnceOnLanguage { TwentyOneTweaksEnabled = false, ThemePreference = 0 };
        var target = await NewDeviceAsync(ownPrefs);

        var result = await new ExportService(target, ownPrefs).ImportFromJsonAsync(backup);

        result.Success.Should().BeFalse();
        ownPrefs.TwentyOneTweaksEnabled.Should().BeFalse(
            "the settings written before the throw have to be put back");
        ownPrefs.ThemePreference.Should().Be(0);
    }

    /// <summary>
    /// Fails the first write to <see cref="Language"/> and behaves afterwards, standing in for a
    /// backing store that drops out mid-write. Import applies the file's settings before it opens
    /// the database transaction, so that half-applied state is only recoverable by hand.
    /// </summary>
    private sealed class ThrowsOnceOnLanguage : IAppPreferences
    {
        private bool _thrown;
        private string? _language;

        public bool DailyDozenEnabled { get; set; } = true;
        public bool TwentyOneTweaksEnabled { get; set; }
        public bool WeightTrackingEnabled { get; set; }
        public bool UseMetricUnits { get; set; } = true;
        public double? HeightCm { get; set; }
        public double? GoalWeight { get; set; }
        public int ThemePreference { get; set; }
        public string DisabledItemIds { get; set; } = string.Empty;

        public string? Language
        {
            get => _language;
            set
            {
                if (!_thrown)
                {
                    _thrown = true;
                    throw new InvalidOperationException("preferences backing store unavailable");
                }

                _language = value;
            }
        }
    }
}
