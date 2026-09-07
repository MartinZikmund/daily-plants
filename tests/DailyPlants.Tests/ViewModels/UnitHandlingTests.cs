using System.Globalization;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// Weight and height are stored canonically (kg, cm) regardless of the unit the user
/// types in. Switching units must convert the stored history for display, never
/// reinterpret it, so the chart, the goal line and the 30-day delta all share one scale.
/// </summary>
[TestClass]
public class UnitHandlingTests
{
    private CultureInfo _originalCulture = null!;
    private FakeAppPreferences _prefs = null!;

    [TestInitialize]
    public void Initialize()
    {
        // Height/weight text goes through double.TryParse, which is culture sensitive.
        _originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        _prefs = new FakeAppPreferences { WeightTrackingEnabled = true, UseMetricUnits = true };
    }

    [TestCleanup]
    public void Cleanup() => CultureInfo.CurrentCulture = _originalCulture;

    private SettingsViewModel CreateSettings()
    {
        var localization = new Mock<ILocalizationService>();
        localization.SetupGet(l => l.SupportedLanguages).Returns([new LanguageOption { Code = "en" }]);
        localization.SetupGet(l => l.CurrentLanguage).Returns("en");
        return new SettingsViewModel(_prefs, Mock.Of<IExportService>(), localization.Object);
    }

    private StatisticsViewModel CreateStatistics(IDataService data) => new(data, _prefs);

    // ===== Height =====

    [TestMethod]
    public void HeightText_EnteredInInches_IsStoredInCentimetres()
    {
        var vm = CreateSettings();
        vm.UseMetricUnits = false;

        vm.HeightText = "70";

        _prefs.HeightCm.Should().BeApproximately(177.8, 0.05, "70 inches is 177.8 cm");
    }

    [TestMethod]
    public void HeightText_EnteredInCentimetres_IsStoredUnchanged()
    {
        var vm = CreateSettings();
        vm.UseMetricUnits = true;

        vm.HeightText = "178";

        _prefs.HeightCm.Should().BeApproximately(178, 0.001);
    }

    [TestMethod]
    public void SwitchingToImperial_ShowsTheSameHeightInInches()
    {
        var vm = CreateSettings();
        vm.UseMetricUnits = true;
        vm.HeightText = "177.8";

        vm.UseMetricUnits = false;

        double.Parse(vm.HeightText, CultureInfo.InvariantCulture).Should().BeApproximately(70, 0.05);
        _prefs.HeightCm.Should().BeApproximately(177.8, 0.05, "the stored value is canonical and must not drift");
    }

    // ===== Weight =====

    [TestMethod]
    public void GoalWeight_EnteredInPounds_IsStoredInKilograms()
    {
        var vm = CreateSettings();
        vm.UseMetricUnits = false;

        vm.GoalWeightText = "176.4";

        _prefs.GoalWeight.Should().BeApproximately(80, 0.05, "176.4 lb is 80 kg");
    }

    [TestMethod]
    public void SwitchingUnitsBackAndForth_LeavesTheStoredWeightUnchanged()
    {
        var vm = CreateSettings();
        vm.UseMetricUnits = true;
        vm.GoalWeightText = "80";

        vm.UseMetricUnits = false;
        vm.UseMetricUnits = true;

        _prefs.GoalWeight.Should().BeApproximately(80, 0.05, "a round trip through the toggle must not drift");
        double.Parse(vm.GoalWeightText, CultureInfo.InvariantCulture).Should().BeApproximately(80, 0.05);
    }

    [TestMethod]
    public async Task TodayWeight_ForAnImperialUser_ConvertsTheStoredKilograms()
    {
        _prefs.UseMetricUnits = false;
        var data = new InMemoryDataService(_prefs);
        await data.SaveWeightEntryAsync(new WeightEntry { Date = DateOnly.FromDateTime(DateTime.Today), Weight = 80 });
        var vm = CreateStatistics(data);

        await vm.LoadStatisticsAsync();

        vm.TodayWeight.Should().NotBeNull();
        vm.TodayWeight!.Value.Should().BeApproximately(176.4, 0.05, "80 kg shown to an imperial user is 176.4 lb");
        vm.WeightUnit.Should().Be("lb");
        double.Parse(vm.WeightInputText, CultureInfo.InvariantCulture).Should().BeApproximately(176.4, 0.05);
    }

    [TestMethod]
    public async Task SaveTodayWeight_TypedInPounds_IsStoredInKilograms()
    {
        _prefs.UseMetricUnits = false;
        var data = new InMemoryDataService(_prefs);
        var vm = CreateStatistics(data);
        await vm.LoadStatisticsAsync();

        vm.WeightInputText = "176.4";
        await vm.SaveTodayWeightCommand.ExecuteAsync(null);

        var stored = await data.GetWeightEntryAsync(DateOnly.FromDateTime(DateTime.Today));
        stored.Should().NotBeNull();
        stored!.Weight.Should().BeApproximately(80, 0.05);
    }

    [TestMethod]
    public async Task WeightChange_ForAnImperialUser_IsReportedInPounds()
    {
        _prefs.UseMetricUnits = false;
        var data = new InMemoryDataService(_prefs);
        var today = DateOnly.FromDateTime(DateTime.Today);
        await data.SaveWeightEntryAsync(new WeightEntry { Date = today.AddDays(-5), Weight = 80 });
        await data.SaveWeightEntryAsync(new WeightEntry { Date = today, Weight = 78 });
        var vm = CreateStatistics(data);

        await vm.LoadStatisticsAsync();

        // 2 kg lost is 4.4 lb, not 2.
        vm.WeightChangeText.Should().Contain("4.4");
        vm.WeightChangeText.Should().Contain("lb");
    }

    // ===== Chart values =====

    [TestMethod]
    public async Task WeightHistory_ForAnImperialUser_IsPlottedInPounds()
    {
        _prefs.UseMetricUnits = false;
        var data = new InMemoryDataService(_prefs);
        var today = DateOnly.FromDateTime(DateTime.Today);
        await data.SaveWeightEntryAsync(new WeightEntry { Date = today, Weight = 80 });
        var vm = CreateStatistics(data);

        await vm.LoadStatisticsAsync();

        vm.WeightHistory.Should().ContainSingle();
        vm.WeightHistory[0].Weight.Should().BeApproximately(176.4, 0.05);
    }

    [TestMethod]
    public async Task GoalWeightForChart_ForAnImperialUser_MatchesThePlottedScale()
    {
        _prefs.UseMetricUnits = false;
        _prefs.GoalWeight = 75; // stored canonically, in kilograms
        var data = new InMemoryDataService(_prefs);
        var vm = CreateStatistics(data);

        await vm.LoadStatisticsAsync();

        vm.GoalWeightForChart.Should().BeApproximately(165.3, 0.05, "the goal line shares the axis with the plotted pounds");
    }
}
