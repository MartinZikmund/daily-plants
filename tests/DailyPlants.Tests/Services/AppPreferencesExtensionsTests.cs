using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class AppPreferencesExtensionsTests
{
    [TestMethod]
    public void GetDisabledItemIdSet_EmptyString_ReturnsEmptySet()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = string.Empty };

        prefs.GetDisabledItemIdSet().Should().BeEmpty();
    }

    [TestMethod]
    public void GetDisabledItemIdSet_SingleId_ReturnsSetWithOneElement()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans" };

        prefs.GetDisabledItemIdSet().Should().BeEquivalentTo(new[] { "beans" });
    }

    [TestMethod]
    public void GetDisabledItemIdSet_CommaSeparated_ParsesAll()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans,berries,greens" };

        prefs.GetDisabledItemIdSet().Should().BeEquivalentTo(new[] { "beans", "berries", "greens" });
    }

    [TestMethod]
    public void GetDisabledItemIdSet_DoubleCommas_AreSkipped()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans,,berries,," };

        prefs.GetDisabledItemIdSet().Should().BeEquivalentTo(new[] { "beans", "berries" });
    }

    [TestMethod]
    public void IsItemDisabled_Present_ReturnsTrue()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans,berries" };

        prefs.IsItemDisabled("beans").Should().BeTrue();
    }

    [TestMethod]
    public void IsItemDisabled_Absent_ReturnsFalse()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans" };

        prefs.IsItemDisabled("berries").Should().BeFalse();
    }

    [TestMethod]
    public void SetItemDisabled_True_AddsToDisabledSet()
    {
        var prefs = new FakeAppPreferences();

        prefs.SetItemDisabled("beans", true);

        prefs.IsItemDisabled("beans").Should().BeTrue();
    }

    [TestMethod]
    public void SetItemDisabled_False_RemovesFromDisabledSet()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans,berries" };

        prefs.SetItemDisabled("beans", false);

        prefs.IsItemDisabled("beans").Should().BeFalse();
        prefs.IsItemDisabled("berries").Should().BeTrue();
    }

    [TestMethod]
    public void SetItemDisabled_True_IsIdempotent()
    {
        var prefs = new FakeAppPreferences();

        prefs.SetItemDisabled("beans", true);
        prefs.SetItemDisabled("beans", true);

        prefs.GetDisabledItemIdSet().Should().BeEquivalentTo(new[] { "beans" });
    }

    [TestMethod]
    public void SetItemDisabled_PrunesStaleIds()
    {
        // Persisted CSV contains a stale id (sun_protection — removed in v2 migration).
        // Calling SetItemDisabled on any valid id must intersect with current valid items.
        var prefs = new FakeAppPreferences { DisabledItemIds = "sun_protection,beans" };

        prefs.SetItemDisabled("berries", true);

        var set = prefs.GetDisabledItemIdSet();
        set.Should().NotContain("sun_protection");
        set.Should().Contain("beans");
        set.Should().Contain("berries");
    }

    [TestMethod]
    public void SetItemDisabled_FalseOnAbsentItem_LeavesStateUnchanged()
    {
        var prefs = new FakeAppPreferences { DisabledItemIds = "beans" };

        prefs.SetItemDisabled("berries", false);

        prefs.GetDisabledItemIdSet().Should().BeEquivalentTo(new[] { "beans" });
    }
}
