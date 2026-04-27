using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class ChecklistItemViewModelTests
{
    private static ChecklistItem NewItem(int recommended = 3, string id = "beans", string metric = "1/4 cup", string imperial = "1/4 cup_imp") => new()
    {
        Id = id,
        Name = id,
        Description = "desc",
        RecommendedServings = recommended,
        ServingSizeMetric = metric,
        ServingSizeImperial = imperial,
        Checklists = new[] { ChecklistType.DailyDozen },
    };

    [TestMethod]
    public void TotalRecommendedServings_NoMergedChildren_EqualsBase()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);

        vm.TotalRecommendedServings.Should().Be(3);
        vm.HasMergedChildren.Should().BeFalse();
    }

    [TestMethod]
    public void TotalRecommendedServings_WithMergedChildren_EqualsSum()
    {
        var parent = NewItem(3, "beans");
        var child = NewItem(1, "more_legumes");
        var vm = new ChecklistItemViewModel(parent, DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true,
            mergedChildren: new[] { child });

        vm.TotalRecommendedServings.Should().Be(4);
        vm.HasMergedChildren.Should().BeTrue();
        vm.MergedChildren.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow(0, 3, 0.0)]
    [DataRow(1, 3, 1.0 / 3.0)]
    [DataRow(3, 3, 1.0)]
    [DataRow(5, 3, 1.0)] // clamped at 1
    public void Progress_IsClampedToRange(int completed, int recommended, double expected)
    {
        var vm = new ChecklistItemViewModel(NewItem(recommended), DateOnly.FromDateTime(DateTime.Today), completed, useMetricUnits: true);

        vm.Progress.Should().BeApproximately(expected, 1e-6);
    }

    [TestMethod]
    public void IsComplete_TrueWhenServingsAtOrAboveTotal()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 3, useMetricUnits: true);

        vm.IsComplete.Should().BeTrue();
    }

    [TestMethod]
    public void IsComplete_FalseWhenServingsBelowTotal()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 2, useMetricUnits: true);

        vm.IsComplete.Should().BeFalse();
    }

    [TestMethod]
    public void ServingsDisplayText_FormatsAsCurrentOverTotal()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 1, useMetricUnits: true);

        vm.ServingsDisplayText.Should().Be("1/3");
    }

    [TestMethod]
    public void ServingSizeDisplay_Metric_ReturnsMetricString()
    {
        var vm = new ChecklistItemViewModel(NewItem(3, metric: "M", imperial: "I"), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);

        vm.ServingSizeDisplay.Should().Be("M");
    }

    [TestMethod]
    public void ServingSizeDisplay_Imperial_ReturnsImperialString()
    {
        var vm = new ChecklistItemViewModel(NewItem(3, metric: "M", imperial: "I"), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: false);

        vm.ServingSizeDisplay.Should().Be("I");
    }

    [TestMethod]
    public void IncrementServingCommand_BelowMax_Increments()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 1, useMetricUnits: true);

        vm.IncrementServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(2);
    }

    [TestMethod]
    public void IncrementServingCommand_AtMax_DoesNotIncrement()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 3, useMetricUnits: true);

        vm.IncrementServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(3);
    }

    [TestMethod]
    public void DecrementServingCommand_AboveZero_Decrements()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 2, useMetricUnits: true);

        vm.DecrementServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(1);
    }

    [TestMethod]
    public void DecrementServingCommand_AtZero_DoesNotDecrement()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);

        vm.DecrementServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(0);
    }

    [TestMethod]
    public void ToggleServingCommand_BelowMax_Increments()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);

        vm.ToggleServingCommand.Execute(null);
        vm.ToggleServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(2);
    }

    [TestMethod]
    public void ToggleServingCommand_AtMax_DoesNotWrap()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 3, useMetricUnits: true);

        vm.ToggleServingCommand.Execute(null);

        vm.ServingsCompleted.Should().Be(3);
    }

    [TestMethod]
    public void ServingsChanged_RaisedWhenServingsChange()
    {
        var vm = new ChecklistItemViewModel(NewItem(3), DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);
        var capturedValues = new List<int>();
        vm.ServingsChanged += (_, value) => capturedValues.Add(value);

        vm.ServingsCompleted = 2;

        capturedValues.Should().ContainSingle().Which.Should().Be(2);
    }

    [TestMethod]
    public void ShowItemDetailCommand_RaisesItemDetailRequested()
    {
        var item = NewItem(3);
        var vm = new ChecklistItemViewModel(item, DateOnly.FromDateTime(DateTime.Today), 0, useMetricUnits: true);
        ChecklistItem? captured = null;
        vm.ItemDetailRequested += (_, requested) => captured = requested;

        vm.ShowItemDetailCommand.Execute(null);

        captured.Should().BeSameAs(item);
    }
}
