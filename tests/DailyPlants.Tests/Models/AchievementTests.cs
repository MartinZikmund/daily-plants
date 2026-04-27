namespace DailyPlants.Tests.Models;

[TestClass]
public class AchievementTests
{
    [TestMethod]
    public void Construct_WithRequiredProperties_AssignsValues()
    {
        var achievement = new Achievement
        {
            Id = "streak_7",
            NameKey = "Achievement_7Day_Name",
            DescriptionKey = "Achievement_7Day_Desc",
            Type = AchievementType.Streak,
            IconGlyph = "",
            BadgeColor = "#4CAF50",
            TargetValue = 7
        };

        achievement.Id.Should().Be("streak_7");
        achievement.Type.Should().Be(AchievementType.Streak);
        achievement.TargetValue.Should().Be(7);
        achievement.ItemId.Should().BeNull();
        achievement.IconPath.Should().BeEmpty();
    }

    [TestMethod]
    public void TargetValue_DefaultsToZero()
    {
        var achievement = NewMinimal();

        achievement.TargetValue.Should().Be(0);
    }

    [TestMethod]
    public void Records_WithSameValues_AreEqual()
    {
        var a = NewMinimal();
        var b = NewMinimal();

        a.Should().Be(b);
    }

    [TestMethod]
    [DataRow(AchievementType.Streak)]
    [DataRow(AchievementType.Milestone)]
    [DataRow(AchievementType.ItemSpecific)]
    [DataRow(AchievementType.Completion)]
    public void Type_AcceptsAllEnumValues(AchievementType type)
    {
        var achievement = NewMinimal() with { Type = type };

        achievement.Type.Should().Be(type);
    }

    private static Achievement NewMinimal() => new()
    {
        Id = "test",
        NameKey = "name",
        DescriptionKey = "desc",
        Type = AchievementType.Milestone,
        IconGlyph = "",
        BadgeColor = "#FFFFFF"
    };
}
