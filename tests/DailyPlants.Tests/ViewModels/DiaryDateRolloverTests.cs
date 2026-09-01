using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// The diary caches the date it is showing. Left open across midnight it kept writing
/// to the previous day while still labelling it "Today" — and bedtime is exactly when
/// a nutrition tracker gets its last taps.
/// </summary>
[TestClass]
public class DiaryDateRolloverTests
{
    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _data = null!;
    private FakeTimeProvider _clock = null!;

    private static readonly DateTimeOffset LateEvening =
        new(2026, 4, 1, 23, 50, 0, TimeSpan.Zero);

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _data = new InMemoryDataService(_prefs);
        _clock = new FakeTimeProvider(LateEvening);
    }

    private DiaryViewModel NewViewModel() => new(_data, _prefs, achievementService: null, timeProvider: _clock);

    [TestMethod]
    public async Task RefreshIfDateChangedAsync_AfterMidnight_MovesToTheNewDay()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        vm.CurrentDate.Should().Be(new DateOnly(2026, 4, 1));

        _clock.Advance(TimeSpan.FromMinutes(20));
        await vm.RefreshIfDateChangedAsync();

        vm.CurrentDate.Should().Be(new DateOnly(2026, 4, 2));
    }

    [TestMethod]
    public async Task ServingsLoggedAfterMidnight_AreSavedToTheNewDay()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        _clock.Advance(TimeSpan.FromMinutes(20));
        await vm.RefreshIfDateChangedAsync();

        var beans = vm.Items.Single(i => i.Item.Id == "beans");
        beans.ServingsCompleted = 2;

        var newDay = await _data.GetEntryAsync(new DateOnly(2026, 4, 2), "beans");
        newDay.Should().NotBeNull("the tap belongs to the day it was made");
        newDay!.ServingsCompleted.Should().Be(2);
        (await _data.GetEntryAsync(new DateOnly(2026, 4, 1), "beans")).Should().BeNull();
    }

    [TestMethod]
    public async Task RefreshIfDateChangedAsync_WhileViewingAPastDay_LeavesTheUserThere()
    {
        var vm = NewViewModel();
        await vm.GoToDateAsync(new DateOnly(2026, 3, 20));

        _clock.Advance(TimeSpan.FromMinutes(20));
        await vm.RefreshIfDateChangedAsync();

        vm.CurrentDate.Should().Be(new DateOnly(2026, 3, 20),
            "someone reviewing an old day must not be yanked to today");
    }

    [TestMethod]
    public async Task RefreshIfDateChangedAsync_AfterReturningToToday_TracksTheDateAgain()
    {
        var vm = NewViewModel();
        await vm.GoToDateAsync(new DateOnly(2026, 3, 20));
        await vm.GoToTodayCommand.ExecuteAsync(null);

        _clock.Advance(TimeSpan.FromMinutes(20));
        await vm.RefreshIfDateChangedAsync();

        vm.CurrentDate.Should().Be(new DateOnly(2026, 4, 2));
    }

    [TestMethod]
    public async Task RefreshIfDateChangedAsync_WithinTheSameDay_DoesNothing()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        _clock.Advance(TimeSpan.FromMinutes(5));
        await vm.RefreshIfDateChangedAsync();

        vm.CurrentDate.Should().Be(new DateOnly(2026, 4, 1));
    }
}
