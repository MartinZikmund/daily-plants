using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// The toggles are rebuilt every time Settings opens. Building one is not the user
/// changing it, and must not write to the preference store.
/// </summary>
[TestClass]
public class ChecklistToggleTests
{
    /// <summary>Counts writes to DisabledItemIds, which is where a toggle lands.</summary>
    private sealed class CountingPreferences : IAppPreferences
    {
        private string _disabledItemIds = string.Empty;

        public int DisabledItemIdWrites { get; private set; }

        public bool DailyDozenEnabled { get; set; } = true;
        public bool TwentyOneTweaksEnabled { get; set; }
        public bool WeightTrackingEnabled { get; set; }
        public bool UseMetricUnits { get; set; } = true;
        public double? HeightCm { get; set; }
        public double? GoalWeight { get; set; }
        public int ThemePreference { get; set; }
        public string? Language { get; set; }
        public bool UnitsAreCanonical { get; set; }

        public string DisabledItemIds
        {
            get => _disabledItemIds;
            set
            {
                DisabledItemIdWrites++;
                _disabledItemIds = value;
            }
        }
    }

    [TestMethod]
    public void Constructing_AnEnabledToggle_WritesNothing()
    {
        var prefs = new CountingPreferences();
        var item = ChecklistDefinitions.GetItemById("beans")!;

        _ = new ChecklistItemToggleViewModel(prefs, item);

        prefs.DisabledItemIdWrites.Should().Be(0,
            "opening Settings rebuilds every toggle, and reading state is not changing it");
    }

    [TestMethod]
    public void Constructing_ADisabledToggle_WritesNothing()
    {
        var prefs = new CountingPreferences { DisabledItemIds = "beans" };
        var item = ChecklistDefinitions.GetItemById("beans")!;

        _ = new ChecklistItemToggleViewModel(prefs, item);

        prefs.DisabledItemIdWrites.Should().Be(1, "only the test's own setup write");
    }

    [TestMethod]
    public void Toggling_AfterConstruction_StillWrites()
    {
        var prefs = new CountingPreferences();
        var item = ChecklistDefinitions.GetItemById("beans")!;
        var toggle = new ChecklistItemToggleViewModel(prefs, item);

        toggle.IsEnabled = false;

        prefs.DisabledItemIds.Should().Contain("beans", "the guard must not swallow real toggles");
        prefs.DisabledItemIdWrites.Should().Be(1);
    }
}
