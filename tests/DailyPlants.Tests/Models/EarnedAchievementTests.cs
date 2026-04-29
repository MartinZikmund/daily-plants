namespace DailyPlants.Tests.Models;

[TestClass]
public class EarnedAchievementTests
{
    [TestMethod]
    public void Properties_RoundTrip()
    {
        var when = new DateTime(2026, 4, 27, 12, 30, 0, DateTimeKind.Utc);
        var earned = new EarnedAchievement
        {
            AchievementId = "streak_7",
            EarnedAt = when,
            HasBeenSeen = true
        };

        earned.AchievementId.Should().Be("streak_7");
        earned.EarnedAt.Should().Be(when);
        earned.HasBeenSeen.Should().BeTrue();
    }

    [TestMethod]
    public void HasBeenSeen_DefaultsToFalse()
    {
        var earned = new EarnedAchievement
        {
            AchievementId = "streak_7",
            EarnedAt = DateTime.UtcNow
        };

        earned.HasBeenSeen.Should().BeFalse();
    }

    [TestMethod]
    public void HasBeenSeen_IsMutable()
    {
        var earned = new EarnedAchievement
        {
            AchievementId = "streak_7",
            EarnedAt = DateTime.UtcNow
        };

        earned.HasBeenSeen = true;

        earned.HasBeenSeen.Should().BeTrue();
    }
}
