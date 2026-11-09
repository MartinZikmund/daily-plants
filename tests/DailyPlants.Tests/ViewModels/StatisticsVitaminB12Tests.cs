using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class StatisticsVitaminB12Tests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private FakeAppPreferences _prefs = null!;
    private InMemoryDataService _data = null!;

    [TestInitialize]
    public async Task InitAsync()
    {
        _prefs = new FakeAppPreferences { DailyDozenEnabled = true, TwentyOneTweaksEnabled = false };
        _data = new InMemoryDataService(_prefs);

        foreach (var (itemId, servings) in ChecklistDefinitions.GetRequiredServingsMap(_prefs))
        {
            await _data.SaveEntryAsync(new DailyEntry { Date = Today, ItemId = itemId, ServingsCompleted = servings });
        }
    }

    [TestMethod]
    public async Task DayWithoutB12_CountsAsFullyComplete()
    {
        StatisticsViewModel vm = new(_data, _prefs);

        await vm.LoadStatisticsAsync();

        vm.DailyCompletion[^1].Share.Should().Be(1);
        vm.CurrentStreak.Should().Be(1);
    }

    [TestMethod]
    public async Task MissedItems_LeaveOutB12()
    {
        StatisticsViewModel vm = new(_data, _prefs);

        await vm.LoadStatisticsAsync();

        vm.MissedItems.Should().HaveCount(12);
    }
}
