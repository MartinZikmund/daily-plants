namespace DailyPlants.Tests.Services;

[TestClass]
public class MergeRulesTests
{
    [TestMethod]
    public void All_ContainsTheFourDocumentedPairs()
    {
        var pairs = MergeRules.All.Select(r => (r.ChildId, r.ParentId)).ToHashSet();

        pairs.Should().BeEquivalentTo(new[]
        {
            ("more_legumes", "beans"),
            ("more_berries", "berries"),
            ("more_greens", "greens"),
            ("stay_hydrated", "beverages"),
        });
    }

    [TestMethod]
    public void GetActiveMerges_EmptyEnabledSet_ReturnsEmpty()
    {
        var result = MergeRules.GetActiveMerges(new HashSet<string>());

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GetActiveMerges_OnlyParent_ReturnsEmpty()
    {
        var result = MergeRules.GetActiveMerges(new HashSet<string> { "beans" });

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GetActiveMerges_OnlyChild_ReturnsEmpty()
    {
        var result = MergeRules.GetActiveMerges(new HashSet<string> { "more_legumes" });

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void GetActiveMerges_BothEnabled_ReturnsThatMerge()
    {
        var result = MergeRules.GetActiveMerges(new HashSet<string> { "beans", "more_legumes" });

        result.Should().ContainSingle();
        result[0].ChildId.Should().Be("more_legumes");
        result[0].ParentId.Should().Be("beans");
    }

    [TestMethod]
    public void GetActiveMerges_AllPairsEnabled_ReturnsAllFour()
    {
        var enabled = new HashSet<string>
        {
            "beans", "more_legumes",
            "berries", "more_berries",
            "greens", "more_greens",
            "beverages", "stay_hydrated",
        };

        var result = MergeRules.GetActiveMerges(enabled);

        result.Should().HaveCount(4);
    }

    [TestMethod]
    public void GetActiveMerges_PartialOverlap_ReturnsOnlyFullPairs()
    {
        // beans+more_legumes (both) → active.
        // berries (only parent) → not active.
        // more_greens (only child) → not active.
        var enabled = new HashSet<string>
        {
            "beans", "more_legumes",
            "berries",
            "more_greens",
        };

        var result = MergeRules.GetActiveMerges(enabled);

        result.Should().ContainSingle();
        result[0].ChildId.Should().Be("more_legumes");
    }
}
