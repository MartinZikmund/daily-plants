using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// Streaks and perfect days are judged against the settings in force, so changing which
/// items are tracked also re-judges days already logged. The user is told that once,
/// the first time they change something, rather than watching the numbers move on their own.
/// </summary>
[TestClass]
public class ChecklistChangeWarningTests
{
    private FakeAppPreferences _prefs = null!;

    [TestInitialize]
    public void Initialize() => _prefs = new FakeAppPreferences { DailyDozenEnabled = true };

    private SettingsViewModel CreateSettings()
    {
        var localization = new Mock<ILocalizationService>();
        localization.SetupGet(l => l.SupportedLanguages).Returns([new LanguageOption { Code = "en" }]);
        localization.SetupGet(l => l.CurrentLanguage).Returns("en");
        return new SettingsViewModel(_prefs, Mock.Of<IExportService>(), localization.Object);
    }

    /// <summary>Counts the warnings a view would have shown.</summary>
    private static int CountWarnings(SettingsViewModel vm, Action change)
    {
        var warnings = 0;
        void OnWarning(object? sender, EventArgs e) => warnings++;

        vm.ChecklistImpactWarningRequested += OnWarning;
        try
        {
            change();
        }
        finally
        {
            vm.ChecklistImpactWarningRequested -= OnWarning;
        }

        return warnings;
    }

    [TestMethod]
    public async Task OpeningSettings_DoesNotWarn()
    {
        var vm = CreateSettings();

        var warnings = 0;
        vm.ChecklistImpactWarningRequested += (_, _) => warnings++;
        await vm.LoadSettingsAsync();

        warnings.Should().Be(0, "reading the saved settings back is not the user changing them");
        _prefs.HasSeenChecklistImpactWarning.Should().BeFalse();
    }

    [TestMethod]
    public async Task EnablingAChecklist_TheFirstTime_Warns()
    {
        var vm = CreateSettings();
        await vm.LoadSettingsAsync();

        var warnings = CountWarnings(vm, () => vm.TwentyOneTweaksEnabled = true);

        warnings.Should().Be(1);
        _prefs.HasSeenChecklistImpactWarning.Should().BeTrue("the warning is shown once, not every time");
    }

    [TestMethod]
    public async Task TogglingASingleItem_TheFirstTime_Warns()
    {
        var vm = CreateSettings();
        await vm.LoadSettingsAsync();

        var warnings = CountWarnings(vm, () => vm.DailyDozenItems[0].IsEnabled = false);

        warnings.Should().Be(1, "switching one item off moves the bar for every past day just as much");
    }

    [TestMethod]
    public async Task ChangingChecklists_AfterTheWarningWasSeen_DoesNotWarnAgain()
    {
        var vm = CreateSettings();
        await vm.LoadSettingsAsync();
        vm.TwentyOneTweaksEnabled = true;

        var warnings = CountWarnings(vm, () =>
        {
            vm.TwentyOneTweaksEnabled = false;
            vm.DailyDozenItems[0].IsEnabled = false;
        });

        warnings.Should().Be(0);
    }

    [TestMethod]
    public async Task ChangingChecklists_OnALaterVisit_DoesNotWarnAgain()
    {
        var first = CreateSettings();
        await first.LoadSettingsAsync();
        first.TwentyOneTweaksEnabled = true;

        // Settings is rebuilt from scratch every time the page is opened.
        var second = CreateSettings();
        await second.LoadSettingsAsync();

        var warnings = CountWarnings(second, () => second.DailyDozenItems[0].IsEnabled = false);

        warnings.Should().Be(0, "the preference outlives the page, so the warning does not come back");
    }

    [TestMethod]
    public async Task ChangingASettingThatDoesNotAffectCompletion_DoesNotWarn()
    {
        var vm = CreateSettings();
        await vm.LoadSettingsAsync();

        var warnings = CountWarnings(vm, () =>
        {
            vm.UseMetricUnits = false;
            vm.WeightTrackingEnabled = true;
        });

        warnings.Should().Be(0, "units and weight tracking have nothing to do with streaks");
    }
}
