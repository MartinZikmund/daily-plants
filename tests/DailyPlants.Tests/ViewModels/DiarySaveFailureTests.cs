using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// Tapping a serving is the app's most frequent action and it writes to SQLite. A
/// transient write failure there must degrade gracefully rather than take the process
/// down through an unobserved async void handler.
/// </summary>
[TestClass]
public class DiarySaveFailureTests
{
    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _inner = null!;
    private FailingDataService _data = null!;

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true };
        _inner = new InMemoryDataService(_prefs);
        _data = new FailingDataService(_inner);
    }

    private DiaryViewModel NewViewModel() => new(_data, _prefs);

    [TestMethod]
    public async Task ServingsChanged_WhenTheSaveFails_ReportsTheFailure()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        Exception? reported = null;
        vm.SaveFailed += (_, ex) => reported = ex;

        _data.FailWrites = true;
        vm.Items.Single(i => i.Item.Id == "beans").ServingsCompleted = 1;
        await Task.Yield();

        reported.Should().NotBeNull("a failed save must be surfaced, not swallowed");
    }

    [TestMethod]
    public async Task ServingsChanged_WhenTheSaveFails_RevertsTheDisplayedCount()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        var beans = vm.Items.Single(i => i.Item.Id == "beans");
        beans.ServingsCompleted = 2;

        _data.FailWrites = true;
        beans.ServingsCompleted = 3;
        await Task.Yield();

        beans.ServingsCompleted.Should().Be(2, "the UI must not claim a serving that was never stored");
    }

    [TestMethod]
    public async Task ServingsChanged_WhenTheSaveSucceeds_KeepsTheNewCount()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        var beans = vm.Items.Single(i => i.Item.Id == "beans");

        beans.ServingsCompleted = 3;
        await Task.Yield();

        beans.ServingsCompleted.Should().Be(3);
        var stored = await _inner.GetEntryAsync(vm.CurrentDate, "beans");
        stored!.ServingsCompleted.Should().Be(3);
    }

    [TestMethod]
    public async Task ServingsChanged_AfterARecoveredFailure_SavesAgain()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        var beans = vm.Items.Single(i => i.Item.Id == "beans");

        _data.FailWrites = true;
        beans.ServingsCompleted = 1;
        await Task.Yield();

        _data.FailWrites = false;
        beans.ServingsCompleted = 2;
        await Task.Yield();

        var stored = await _inner.GetEntryAsync(vm.CurrentDate, "beans");
        stored!.ServingsCompleted.Should().Be(2, "recovery must not be blocked by the earlier failure");
    }
}
