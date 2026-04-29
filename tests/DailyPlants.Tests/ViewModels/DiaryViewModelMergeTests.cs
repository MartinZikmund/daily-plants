using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class DiaryViewModelMergeTests
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

    // Active merge pair under current ChecklistDefinitions: stay_hydrated (child, TwentyOneTweaks) → beverages (parent, DailyDozen).
    private const string ParentId = "beverages";
    private const string ChildId = "stay_hydrated";

    [TestMethod]
    public async Task LoadDataAsync_BothChecklists_ChildIsAbsorbedIntoParent()
    {
        var vm = NewViewModel();

        await vm.LoadDataAsync();

        var parent = vm.Items.SingleOrDefault(i => i.Item.Id == ParentId);
        parent.Should().NotBeNull();
        parent!.HasMergedChildren.Should().BeTrue();
        parent.MergedChildren.Should().Contain(c => c.Id == ChildId);

        // Child item should not appear as its own VM.
        vm.Items.Should().NotContain(i => i.Item.Id == ChildId);
    }

    [TestMethod]
    public async Task LoadDataAsync_TotalServings_IsSumOfParentAndChild()
    {
        var parent = ChecklistDefinitions.GetItemById(ParentId)!;
        var child = ChecklistDefinitions.GetItemById(ChildId)!;

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        parentVm.TotalRecommendedServings.Should().Be(parent.RecommendedServings + child.RecommendedServings);
    }

    [TestMethod]
    public async Task LoadDataAsync_ExistingEntries_ParentSumIncludesChildServings()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await _data.SaveEntryAsync(new DailyEntry { Date = today, ItemId = ParentId, ServingsCompleted = 2 });
        await _data.SaveEntryAsync(new DailyEntry { Date = today, ItemId = ChildId, ServingsCompleted = 1 });

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        parentVm.ServingsCompleted.Should().Be(3);
    }

    [TestMethod]
    public async Task LoadDataAsync_ParentOnlyEnabled_NoMergeChildItemAbsent()
    {
        // Disable Tweaks (which contains the child) so child is not enabled.
        _prefs.TwentyOneTweaksEnabled = false;

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        parentVm.HasMergedChildren.Should().BeFalse();
        vm.Items.Should().NotContain(i => i.Item.Id == ChildId);
    }

    [TestMethod]
    public async Task LoadDataAsync_ChildOnlyEnabled_ChildAppearsAsStandalone()
    {
        // Disable parent via the disabled-items set; child remains enabled via Tweaks.
        _prefs.SetItemDisabled(ParentId, true);

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        vm.Items.Should().NotContain(i => i.Item.Id == ParentId);
        // Child appears alone since merge is inactive (parent missing from enabled set).
        vm.Items.Should().Contain(i => i.Item.Id == ChildId);
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
    public async Task ItemServingsChanged_NoMerge_PersistsParentOnly()
    {
        // Disable Tweaks so the parent has no merged child.
        _prefs.TwentyOneTweaksEnabled = false;

        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        parentVm.ServingsCompleted = 2;

        // Allow async event handler in OnItemServingsChanged to complete.
        await Task.Yield();

        var entries = await _data.GetEntriesForDateAsync(vm.CurrentDate);
        entries.Should().Contain(e => e.ItemId == ParentId && e.ServingsCompleted == 2);
        entries.Should().NotContain(e => e.ItemId == ChildId);
    }

    [TestMethod]
    public async Task ItemServingsChanged_WithinParentCap_OnlyParentGetsServings()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        var parentItem = ChecklistDefinitions.GetItemById(ParentId)!;

        parentVm.ServingsCompleted = parentItem.RecommendedServings; // exactly fills parent
        await Task.Yield();

        var entries = await _data.GetEntriesForDateAsync(vm.CurrentDate);
        entries.Single(e => e.ItemId == ParentId).ServingsCompleted.Should().Be(parentItem.RecommendedServings);
        var childEntry = entries.SingleOrDefault(e => e.ItemId == ChildId);
        (childEntry?.ServingsCompleted ?? 0).Should().Be(0);
    }

    [TestMethod]
    public async Task ItemServingsChanged_OverflowSpillsToChild()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        var parentItem = ChecklistDefinitions.GetItemById(ParentId)!;

        // Fill parent + 1 into child.
        parentVm.ServingsCompleted = parentItem.RecommendedServings + 1;
        await Task.Yield();

        var entries = await _data.GetEntriesForDateAsync(vm.CurrentDate);
        entries.Single(e => e.ItemId == ParentId).ServingsCompleted.Should().Be(parentItem.RecommendedServings);
        entries.Single(e => e.ItemId == ChildId).ServingsCompleted.Should().Be(1);
    }

    [TestMethod]
    public async Task ItemServingsChanged_OverflowExceedingChildCap_StaysAtChildMax()
    {
        var vm = NewViewModel();
        await vm.LoadDataAsync();

        var parentVm = vm.Items.Single(i => i.Item.Id == ParentId);
        var parentItem = ChecklistDefinitions.GetItemById(ParentId)!;
        var childItem = ChecklistDefinitions.GetItemById(ChildId)!;

        // Fill parent + double child cap → child should clamp at its own recommended.
        parentVm.ServingsCompleted = parentItem.RecommendedServings + childItem.RecommendedServings + 5;
        await Task.Yield();

        var entries = await _data.GetEntriesForDateAsync(vm.CurrentDate);
        entries.Single(e => e.ItemId == ParentId).ServingsCompleted.Should().Be(parentItem.RecommendedServings);
        entries.Single(e => e.ItemId == childItem.Id).ServingsCompleted.Should().Be(childItem.RecommendedServings);
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
