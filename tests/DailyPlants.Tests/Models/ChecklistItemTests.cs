namespace DailyPlants.Tests.Models;

[TestClass]
public class ChecklistItemTests
{
    [TestMethod]
    public void Construct_WithRequiredProperties_AssignsValues()
    {
        var item = new ChecklistItem
        {
            Id = "beans",
            Name = "Beans",
            Description = "3 servings/day",
            RecommendedServings = 3,
            ServingSizeMetric = "1/4 cup",
            ServingSizeImperial = "1/4 cup",
            Checklists = new[] { ChecklistType.DailyDozen }
        };

        item.Id.Should().Be("beans");
        item.Name.Should().Be("Beans");
        item.RecommendedServings.Should().Be(3);
        item.Checklists.Should().ContainSingle().Which.Should().Be(ChecklistType.DailyDozen);
    }

    [TestMethod]
    public void OptionalProperties_DefaultToNull_OrEmpty()
    {
        var item = NewMinimal();

        item.HealthBenefits.Should().BeNull();
        item.MoreInfoUrl.Should().BeNull();
        item.IconPath.Should().BeNull();
        item.SortOrder.Should().Be(0);
    }

    [TestMethod]
    public void Records_WithDifferentIds_AreNotEqual()
    {
        var a = NewMinimal();
        var b = a with { Id = "berries" };

        a.Should().NotBe(b);
    }

    [TestMethod]
    public void With_SameValues_AreEqual()
    {
        var a = NewMinimal();
        var b = a with { };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void With_PreservesUnchangedProperties()
    {
        var original = NewMinimal();
        var modified = original with { RecommendedServings = 5 };

        modified.Id.Should().Be(original.Id);
        modified.Name.Should().Be(original.Name);
        modified.RecommendedServings.Should().Be(5);
    }

    private static ChecklistItem NewMinimal() => new()
    {
        Id = "beans",
        Name = "Beans",
        Description = "desc",
        RecommendedServings = 3,
        ServingSizeMetric = "metric",
        ServingSizeImperial = "imperial",
        Checklists = new[] { ChecklistType.DailyDozen }
    };
}
