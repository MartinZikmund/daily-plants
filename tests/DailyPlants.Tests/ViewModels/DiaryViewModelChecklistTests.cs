using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class DiaryViewModelChecklistTests
{
    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _data = null!;

    [TestInitialize]
    public void Init()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true, TwentyOneTweaksEnabled = true };
        _data = new InMemoryDataService(_prefs);
    }

    private DiaryViewModel NewViewModel() => new(_data, _prefs);

    [TestMethod]
    public async Task LoadDataAsync_BothChecklists_BeveragesAndStayHydratedAreSeparateRows()
    {
        var vm = NewViewModel();

        await vm.LoadDataAsync();

        vm.Items.Should().ContainSingle(i => i.Item.Id == "beverages").Which.ServingIndicators.Should().HaveCount(5);
        vm.Items.Should().ContainSingle(i => i.Item.Id == "stay_hydrated").Which.ServingIndicators.Should().ContainSingle();
    }

    [TestMethod]
    public async Task LoadDataAsync_DailyDozenOnly_StillListsB12()
    {
        _prefs.TwentyOneTweaksEnabled = false;

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Should().Contain(i => i.Item.Id == "vitamin_b12");
    }

    [TestMethod]
    public async Task ServingsChanged_AllCountedItemsDone_CompletesTheDayWithoutB12()
    {
        _prefs.TwentyOneTweaksEnabled = false;
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        DayCompleteInfo? completed = null;
        vm.DayCompleted += (_, info) => completed = info;

        foreach (var item in vm.Items.Where(i => i.Item.Id != "vitamin_b12"))
        {
            item.ServingsCompleted = item.Item.RecommendedServings;
            await Task.Yield();
        }

        vm.OverallProgress.Should().Be(1);
        completed.Should().NotBeNull();
        completed!.TotalServings.Should().Be(24);
    }

    [TestMethod]
    public async Task ServingsChanged_OnlyB12Logged_LeavesProgressAtZero()
    {
        _prefs.TwentyOneTweaksEnabled = false;
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Single(i => i.Item.Id == "vitamin_b12").ServingsCompleted = 1;
        await Task.Yield();

        vm.OverallProgress.Should().Be(0);
        (await _data.GetEntryAsync(vm.CurrentDate, "vitamin_b12"))!.ServingsCompleted.Should().Be(1);
    }

    [TestMethod]
    public async Task LoadDataAsync_OrdersBySortOrderAscending()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Select(i => i.Item.SortOrder).Should().BeInAscendingOrder();
    }

    [TestMethod]
    public async Task LoadDataAsync_NoEnabledChecklists_ItemsEmpty()
    {
        _prefs.DailyDozenEnabled = false;
        _prefs.TwentyOneTweaksEnabled = false;

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Should().BeEmpty();
        vm.ShowEmptyState.Should().BeTrue();
    }

    [TestMethod]
    public async Task ItemServingsChanged_StayHydrated_WritesOnlyItsOwnRow()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Single(i => i.Item.Id == "stay_hydrated").ServingsCompleted = 1;
        await Task.Yield();

        var entries = await _data.GetEntriesForDateAsync(vm.CurrentDate);
        entries.Should().ContainSingle(e => e.ItemId == "stay_hydrated" && e.ServingsCompleted == 1);
        entries.Should().NotContain(e => e.ItemId == "beverages");
    }

    [TestMethod]
    public async Task GoToTodayAsync_ClearsRelativeAndGoToTodayFlags()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();
        await vm.GoToPreviousDayCommand.ExecuteAsync(null);
        await vm.GoToPreviousDayCommand.ExecuteAsync(null);

        await vm.GoToTodayCommand.ExecuteAsync(null);

        vm.CurrentDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        vm.ShowGoToToday.Should().BeFalse();
    }

    [TestMethod]
    public async Task GoToDateAsync_FutureDate_ClampsToToday()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var future = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
        await vm.GoToDateAsync(future);

        vm.CurrentDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        vm.CanGoToNextDay.Should().BeFalse();
    }

    [TestMethod]
    public async Task GoToNextDayAsync_FromToday_DoesNotAdvance()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        await vm.GoToNextDayCommand.ExecuteAsync(null);

        vm.CurrentDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
    }

    [TestMethod]
    public async Task GoToPreviousDayAsync_DecrementsAndShowsGoToToday()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        await vm.GoToPreviousDayCommand.ExecuteAsync(null);
        await vm.GoToPreviousDayCommand.ExecuteAsync(null);

        vm.CurrentDate.Should().Be(DateOnly.FromDateTime(DateTime.Today).AddDays(-2));
        vm.ShowGoToToday.Should().BeTrue();
        vm.CanGoToNextDay.Should().BeTrue();
    }
}
