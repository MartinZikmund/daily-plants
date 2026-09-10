using DailyPlants.Services.Tips;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// "Show tips again" exists so the flow can be replayed without wiping the database -
/// for anyone who skipped it, and for demoing the app on a machine with real history.
/// </summary>
[TestClass]
public class SettingsTipReplayTests
{
    private FakeAppPreferences _prefs = null!;
    private TipService _tips = null!;

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _tips = new TipService(_prefs);
    }

    private SettingsViewModel CreateSettings()
    {
        var localization = new Mock<ILocalizationService>();
        localization.SetupGet(l => l.SupportedLanguages).Returns([new LanguageOption { Code = "en" }]);
        localization.SetupGet(l => l.CurrentLanguage).Returns("en");
        return new SettingsViewModel(_prefs, Mock.Of<IExportService>(), localization.Object, _tips);
    }

    [TestMethod]
    public void ShowTipsAgain_MakesEveryTipEligibleAgain()
    {
        _tips.MarkSeen(TipId.DiaryLogServing, TipId.DiaryDayProgress, TipId.DiaryPastDays);
        var vm = CreateSettings();

        vm.ShowTipsAgainCommand.Execute(null);

        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeTrue();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeTrue();
        _tips.ShouldShow(TipId.DiaryPastDays).Should().BeTrue();
    }

    [TestMethod]
    public void ShowTipsAgain_TouchesNothingElse()
    {
        _prefs.DisabledItemIds = "beans";
        _tips.MarkSeen(TipId.DiaryLogServing);
        var vm = CreateSettings();

        vm.ShowTipsAgainCommand.Execute(null);

        _prefs.DisabledItemIds.Should().Be("beans",
            "replaying the tips is not a reset of anything the user configured");
    }

    [TestMethod]
    public void ShowTipsAgain_WithoutATipService_DoesNotThrow()
    {
        var localization = new Mock<ILocalizationService>();
        localization.SetupGet(l => l.SupportedLanguages).Returns([new LanguageOption { Code = "en" }]);
        localization.SetupGet(l => l.CurrentLanguage).Returns("en");
        var vm = new SettingsViewModel(_prefs, Mock.Of<IExportService>(), localization.Object);

        var replay = () => vm.ShowTipsAgainCommand.Execute(null);

        replay.Should().NotThrow();
    }
}
