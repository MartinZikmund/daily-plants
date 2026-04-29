namespace DailyPlants.Tests.Services;

[TestClass]
public class AchievementDefinitionsTests
{
    [TestMethod]
    public void All_IsNotEmpty()
    {
        AchievementDefinitions.All.Should().NotBeEmpty();
    }

    [TestMethod]
    public void All_HasUniqueIds()
    {
        var ids = AchievementDefinitions.All.Select(a => a.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void GetById_KnownId_ReturnsAchievement()
    {
        var first = AchievementDefinitions.All.First();

        var result = AchievementDefinitions.GetById(first.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(first.Id);
    }

    [TestMethod]
    public void GetById_UnknownId_ReturnsNull()
    {
        var result = AchievementDefinitions.GetById("not_a_real_id_xyz");

        result.Should().BeNull();
    }

    [TestMethod]
    [DataRow(AchievementType.Streak)]
    [DataRow(AchievementType.Milestone)]
    [DataRow(AchievementType.Completion)]
    [DataRow(AchievementType.ItemSpecific)]
    public void GetByType_ReturnsOnlyThatType(AchievementType type)
    {
        var result = AchievementDefinitions.GetByType(type).ToList();

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(a => a.Type == type);
    }

    [TestMethod]
    public void GetByType_UnionAcrossAllTypes_EqualsAll()
    {
        var union = Enum.GetValues<AchievementType>()
            .SelectMany(t => AchievementDefinitions.GetByType(t))
            .Select(a => a.Id)
            .ToHashSet();

        union.Should().BeEquivalentTo(AchievementDefinitions.All.Select(a => a.Id));
    }

    [TestMethod]
    public void ItemSpecificAchievements_HaveValidItemId()
    {
        var itemSpecific = AchievementDefinitions.GetByType(AchievementType.ItemSpecific).ToList();

        itemSpecific.Should().NotBeEmpty();
        foreach (var achievement in itemSpecific)
        {
            achievement.ItemId.Should().NotBeNullOrEmpty($"item-specific achievement {achievement.Id} must reference an item");
            ChecklistDefinitions.GetItemById(achievement.ItemId!)
                .Should().NotBeNull($"item-specific achievement {achievement.Id} references unknown item {achievement.ItemId}");
        }
    }

    [TestMethod]
    public void StreakCompletionItemSpecific_HavePositiveTargetValue()
    {
        var typesRequiringTarget = new[]
        {
            AchievementType.Streak,
            AchievementType.Completion,
            AchievementType.ItemSpecific,
        };

        var typed = AchievementDefinitions.All.Where(a => typesRequiringTarget.Contains(a.Type));

        typed.Should().OnlyContain(a => a.TargetValue > 0);
    }

    [TestMethod]
    public void All_AchievementsHaveBadgeColor()
    {
        AchievementDefinitions.All.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.BadgeColor));
    }
}
