using DailyPlants.Helpers;

namespace DailyPlants.Tests.Services;

/// <summary>
/// ChecklistDefinitions bakes localized strings into its items on first access and caches
/// them for the process lifetime. Resetting the resource loader without dropping that cache
/// leaves every item name, description, serving size and benefit in the previous language.
/// </summary>
[TestClass]
public class ChecklistDefinitionsCacheTests
{
    [TestMethod]
    public void Invalidate_RebuildsTheItemsWithFreshlyResolvedStrings()
    {
        var before = ChecklistDefinitions.AllItems;

        ChecklistDefinitions.Invalidate();

        ChecklistDefinitions.AllItems.Should().NotBeSameAs(before,
            "the cached items hold strings resolved in the previous language");
    }

    [TestMethod]
    public void LocalizerReset_InvalidatesTheChecklistCache()
    {
        var before = ChecklistDefinitions.AllItems;

        Localizer.Reset();

        ChecklistDefinitions.AllItems.Should().NotBeSameAs(before,
            "changing language resets the resource loader, which must also drop baked-in strings");
    }

    [TestMethod]
    public void AllItems_WithoutInvalidation_IsStillCached()
    {
        var first = ChecklistDefinitions.AllItems;

        ChecklistDefinitions.AllItems.Should().BeSameAs(first,
            "the cache must survive ordinary access, or every read rebuilds 34 items");
    }
}
