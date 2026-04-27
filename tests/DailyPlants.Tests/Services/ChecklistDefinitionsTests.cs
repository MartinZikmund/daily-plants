using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class ChecklistDefinitionsTests
{
    [TestMethod]
    public void AllItems_IsNotEmpty()
    {
        ChecklistDefinitions.AllItems.Should().NotBeEmpty();
    }

    [TestMethod]
    public void AllItems_HasUniqueIds()
    {
        var ids = ChecklistDefinitions.AllItems.Select(i => i.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void GetItemsForChecklist_DailyDozen_IncludesCanonicalItems()
    {
        var ids = ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen)
            .Select(i => i.Id)
            .ToList();

        ids.Should().Contain("beans");
        ids.Should().Contain("berries");
        ids.Should().Contain("greens");
        ids.Should().Contain("beverages");
    }

    [TestMethod]
    public void GetItemsForChecklist_ReturnsOnlyMembers()
    {
        var ddItems = ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen).ToList();

        ddItems.Should().OnlyContain(i => i.Checklists.Contains(ChecklistType.DailyDozen));
    }

    [TestMethod]
    public void GetItemById_KnownId_ReturnsItem()
    {
        var first = ChecklistDefinitions.AllItems.First();

        var item = ChecklistDefinitions.GetItemById(first.Id);

        item.Should().NotBeNull();
        item!.Id.Should().Be(first.Id);
    }

    [TestMethod]
    public void GetItemById_UnknownId_ReturnsNull()
    {
        var item = ChecklistDefinitions.GetItemById("not_a_real_item");

        item.Should().BeNull();
    }

    [TestMethod]
    public void GetEnabledItems_BothChecklistsDisabled_ReturnsEmpty()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = false,
            TwentyOneTweaksEnabled = false,
        };

        ChecklistDefinitions.GetEnabledItems(prefs).Should().BeEmpty();
    }

    [TestMethod]
    public void GetEnabledItems_DailyDozenOnly_ContainsDdItemsOnly()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = true,
            TwentyOneTweaksEnabled = false,
        };

        var enabled = ChecklistDefinitions.GetEnabledItems(prefs);

        enabled.Should().NotBeEmpty();
        enabled.Should().OnlyContain(i => i.Checklists.Contains(ChecklistType.DailyDozen));
    }

    [TestMethod]
    public void GetEnabledItems_TweaksOnly_ContainsTweaksItemsOnly()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = false,
            TwentyOneTweaksEnabled = true,
        };

        var enabled = ChecklistDefinitions.GetEnabledItems(prefs);

        enabled.Should().NotBeEmpty();
        enabled.Should().OnlyContain(i => i.Checklists.Contains(ChecklistType.TwentyOneTweaks));
    }

    [TestMethod]
    public void GetEnabledItems_BothChecklists_OrderedBySortOrderAscending()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = true,
            TwentyOneTweaksEnabled = true,
        };

        var enabled = ChecklistDefinitions.GetEnabledItems(prefs);

        enabled.Select(i => i.SortOrder).Should().BeInAscendingOrder();
    }

    [TestMethod]
    public void GetEnabledItems_BothChecklists_NoDuplicateIds()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = true,
            TwentyOneTweaksEnabled = true,
        };

        var enabled = ChecklistDefinitions.GetEnabledItems(prefs);

        enabled.Select(i => i.Id).Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void GetEnabledItems_DisabledItemId_IsExcluded()
    {
        var prefs = new FakeAppPreferences
        {
            DailyDozenEnabled = true,
            DisabledItemIds = "beans"
        };

        var enabled = ChecklistDefinitions.GetEnabledItems(prefs);

        enabled.Should().NotContain(i => i.Id == "beans");
        enabled.Should().Contain(i => i.Id == "berries");
    }

    [TestMethod]
    public void GetEnabledItemIds_MatchesGetEnabledItems()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };

        var ids = ChecklistDefinitions.GetEnabledItemIds(prefs);
        var fromItems = ChecklistDefinitions.GetEnabledItems(prefs).Select(i => i.Id).ToList();

        ids.Should().Equal(fromItems);
    }

    [TestMethod]
    public void GetRequiredServingsMap_KeysMatchEnabledItems()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };

        var map = ChecklistDefinitions.GetRequiredServingsMap(prefs);

        var enabledIds = ChecklistDefinitions.GetEnabledItems(prefs).Select(i => i.Id).ToHashSet();
        map.Keys.Should().BeEquivalentTo(enabledIds);
    }

    [TestMethod]
    public void GetRequiredServingsMap_ValuesMatchRecommendedServings()
    {
        var prefs = new FakeAppPreferences { DailyDozenEnabled = true };

        var map = ChecklistDefinitions.GetRequiredServingsMap(prefs);

        foreach (var item in ChecklistDefinitions.GetEnabledItems(prefs))
        {
            map[item.Id].Should().Be(item.RecommendedServings);
        }
    }
}
