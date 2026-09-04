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

    private async Task<SqliteDataService> NewDeviceAsync(FakeAppPreferences prefs)
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
        await new ExportService(restored, restoredPrefs).ImportFromJsonAsync(backup);

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
}
