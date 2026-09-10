using DailyPlants.Services.Tips;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// The teaching flow is a two-step tour on first launch, then one contextual tip about
/// editing past days. Sequencing lives in the ViewModel so it can be proven without a
/// visual tree - the view only supplies targets and templates.
/// </summary>
[TestClass]
public class DiaryTipTests
{
    private static readonly DateTimeOffset Noon = new(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 4, 20);

    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _data = null!;
    private FakeTimeProvider _clock = null!;
    private TipService _tips = null!;

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _data = new InMemoryDataService(_prefs);
        _clock = new FakeTimeProvider(Noon);
        _tips = new TipService(_prefs);
    }

    private DiaryViewModel NewViewModel(IDataService? data = null) =>
        new(data ?? _data, _prefs, achievementService: null, timeProvider: _clock, tipService: _tips);

    private async Task LogSomethingOnAsync(DateOnly date) =>
        await _data.SaveEntryAsync(new DailyEntry { Date = date, ItemId = "beans", ServingsCompleted = 1 });

    private void CompleteTheTour() =>
        _tips.MarkSeen(TipId.DiaryLogServing, TipId.DiaryDayProgress);

    [TestMethod]
    public async Task EvaluateTipsAsync_NothingSeen_OpensTheFirstTourStep()
    {
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().Be(TipId.DiaryLogServing);
    }

    [TestMethod]
    public async Task TipNext_OnTheFirstStep_MarksItSeenAndOpensTheSecond()
    {
        var vm = NewViewModel();
        await vm.EvaluateTipsAsync();

        vm.TipNextCommand.Execute(null);

        vm.ActiveTip.Should().Be(TipId.DiaryDayProgress);
        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeFalse();
    }

    [TestMethod]
    public async Task TipDismissed_OnTheSecondStep_EndsTheTour()
    {
        var vm = NewViewModel();
        await vm.EvaluateTipsAsync();
        vm.TipNextCommand.Execute(null);

        vm.TipDismissedCommand.Execute(null);

        vm.ActiveTip.Should().BeNull();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeFalse();
    }

    [TestMethod]
    public async Task TipSkip_OnTheFirstStep_MarksBothTourStepsSeen()
    {
        var vm = NewViewModel();
        await vm.EvaluateTipsAsync();

        vm.TipSkipCommand.Execute(null);

        vm.ActiveTip.Should().BeNull();
        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeFalse();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeFalse(
            "skipping step one must not leave step two waiting to ambush the next launch");
    }

    [TestMethod]
    public async Task TipSkip_LeavesTheContextualTipEligible()
    {
        var vm = NewViewModel();
        await vm.EvaluateTipsAsync();

        vm.TipSkipCommand.Execute(null);

        _tips.ShouldShow(TipId.DiaryPastDays).Should().BeTrue(
            "Skip means 'not now, let me look around', not 'never tell me anything'");
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_WithTheTourStillPending_DoesNotOpenTheContextualTip()
    {
        await LogSomethingOnAsync(Today.AddDays(-5));
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().Be(TipId.DiaryLogServing,
            "the tour drains first, even when a gap makes the contextual tip relevant");
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_TourDoneAndAGapInHistory_OpensThePastDaysTip()
    {
        CompleteTheTour();
        await LogSomethingOnAsync(Today.AddDays(-5));
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().Be(TipId.DiaryPastDays);
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_TourDoneAndAnEntryYesterday_OpensNothing()
    {
        CompleteTheTour();
        await LogSomethingOnAsync(Today.AddDays(-1));
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().BeNull("there is no gap to teach them about");
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_TourDoneAndNoHistoryAtAll_OpensNothing()
    {
        CompleteTheTour();
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().BeNull("a user who has never logged anything has no gap to fix");
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_EverythingSeen_OpensNothing()
    {
        CompleteTheTour();
        _tips.MarkSeen(TipId.DiaryPastDays);
        await LogSomethingOnAsync(Today.AddDays(-5));
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().BeNull();
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_WhenTheDateQueryThrows_OpensNothingAndLeavesTheTipUnseen()
    {
        CompleteTheTour();
        await LogSomethingOnAsync(Today.AddDays(-5));
        var failing = new FailingDataService(_data) { FailDateQueries = true };
        var vm = NewViewModel(failing);

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().BeNull();
        _tips.ShouldShow(TipId.DiaryPastDays).Should().BeTrue(
            "a tip nobody saw must not be burned by a database that was busy");
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_WhileATipIsAlreadyOpen_LeavesItAlone()
    {
        var vm = NewViewModel();
        await vm.EvaluateTipsAsync();
        vm.TipNextCommand.Execute(null);

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().Be(TipId.DiaryDayProgress);
    }

    [TestMethod]
    public async Task ActiveTip_DrivesTheViewsOpenFlags()
    {
        var vm = NewViewModel();

        await vm.EvaluateTipsAsync();

        vm.ShowLogServingTip.Should().BeTrue();
        vm.ShowDayProgressTip.Should().BeFalse();
        vm.ShowPastDaysTip.Should().BeFalse();
    }

    [TestMethod]
    public async Task EvaluateTipsAsync_WithNoTipService_DoesNothing()
    {
        var vm = new DiaryViewModel(_data, _prefs, achievementService: null, timeProvider: _clock);

        await vm.EvaluateTipsAsync();

        vm.ActiveTip.Should().BeNull();
    }
}
