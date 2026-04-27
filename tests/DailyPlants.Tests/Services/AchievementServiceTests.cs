namespace DailyPlants.Tests.Services;

[TestClass]
public class AchievementServiceTests
{
    private static Mock<IDataService> NewDataServiceMock()
    {
        var mock = new Mock<IDataService>(MockBehavior.Loose);
        mock.Setup(d => d.GetEarnedAchievementsAsync())
            .ReturnsAsync(Array.Empty<EarnedAchievement>());
        mock.Setup(d => d.GetCurrentStreakAsync()).ReturnsAsync(0);
        mock.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(0);
        mock.Setup(d => d.GetTotalDaysTrackedAsync()).ReturnsAsync(0);
        mock.Setup(d => d.GetPerfectDaysCountAsync()).ReturnsAsync(0);
        mock.Setup(d => d.GetItemCompletionCountAsync(It.IsAny<string>())).ReturnsAsync(0);
        mock.Setup(d => d.GetUnseenAchievementCountAsync()).ReturnsAsync(0);
        return mock;
    }

    [TestMethod]
    public async Task GetAllAchievements_ReturnsDefinitionsList()
    {
        var service = new AchievementService(NewDataServiceMock().Object);

        await service.InitializeAsync();

        service.GetAllAchievements().Should().BeEquivalentTo(AchievementDefinitions.All);
    }

    [TestMethod]
    public async Task IsAchievementEarnedAsync_FromInitiallyEarned_ReturnsTrue()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetEarnedAchievementsAsync()).ReturnsAsync(new[]
        {
            new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow },
        });
        var service = new AchievementService(data.Object);

        (await service.IsAchievementEarnedAsync("streak_7")).Should().BeTrue();
        (await service.IsAchievementEarnedAsync("streak_14")).Should().BeFalse();
    }

    [TestMethod]
    public async Task InitializeAsync_IsIdempotent()
    {
        var data = NewDataServiceMock();
        var service = new AchievementService(data.Object);

        await service.InitializeAsync();
        await service.InitializeAsync();

        // GetEarnedAchievementsAsync is invoked once during init; the second InitializeAsync is a no-op.
        data.Verify(d => d.GetEarnedAchievementsAsync(), Times.Once);
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_UsesMaxOfCurrentAndLongestStreak()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetCurrentStreakAsync()).ReturnsAsync(2);
        data.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(10);
        var service = new AchievementService(data.Object);

        await service.CheckAndAwardAchievementsAsync();

        // Best streak is 10 → 7-day awarded, 14-day not yet.
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "streak_7")), Times.Once);
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "streak_14")), Times.Never);
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_AlreadyEarnedNotReAwarded()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetEarnedAchievementsAsync()).ReturnsAsync(new[]
        {
            new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow },
        });
        data.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(50);
        var service = new AchievementService(data.Object);

        await service.CheckAndAwardAchievementsAsync();

        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "streak_7")), Times.Never);
        // streak_14 and streak_30 should still be awarded.
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "streak_14")), Times.Once);
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "streak_30")), Times.Once);
    }

    [DataTestMethod]
    [DataRow(1, 0, "milestone_first_day", true)]
    [DataRow(0, 0, "milestone_first_day", false)]
    [DataRow(7, 0, "milestone_first_week", true)]
    [DataRow(6, 0, "milestone_first_week", false)]
    [DataRow(30, 0, "milestone_first_month", true)]
    [DataRow(29, 0, "milestone_first_month", false)]
    [DataRow(0, 1, "milestone_first_perfect", true)]
    [DataRow(0, 0, "milestone_first_perfect", false)]
    public async Task CheckAndAwardAchievementsAsync_MilestonesTriggerAtThresholds(int totalDays, int perfectDays, string id, bool shouldAward)
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetTotalDaysTrackedAsync()).ReturnsAsync(totalDays);
        data.Setup(d => d.GetPerfectDaysCountAsync()).ReturnsAsync(perfectDays);
        var service = new AchievementService(data.Object);

        await service.CheckAndAwardAchievementsAsync();

        data.Verify(
            d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == id)),
            shouldAward ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_CompletionAwardedAtTargetValue()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetPerfectDaysCountAsync()).ReturnsAsync(10);
        var service = new AchievementService(data.Object);

        await service.CheckAndAwardAchievementsAsync();

        // completion_10 has TargetValue 10
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "completion_10")), Times.Once);
        // completion_25 has TargetValue 25 — not yet
        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == "completion_25")), Times.Never);
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_ItemSpecificAwardedPerItem()
    {
        var sampleItem = AchievementDefinitions.GetByType(AchievementType.ItemSpecific).First();
        sampleItem.ItemId.Should().NotBeNullOrEmpty();

        var data = NewDataServiceMock();
        data.Setup(d => d.GetItemCompletionCountAsync(sampleItem.ItemId!)).ReturnsAsync(sampleItem.TargetValue);
        var service = new AchievementService(data.Object);

        await service.CheckAndAwardAchievementsAsync();

        data.Verify(d => d.SaveEarnedAchievementAsync(It.Is<EarnedAchievement>(e => e.AchievementId == sampleItem.Id)), Times.Once);
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_RaisesEventForNewlyEarned()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetTotalDaysTrackedAsync()).ReturnsAsync(1);
        var service = new AchievementService(data.Object);

        var awarded = new List<Achievement>();
        service.AchievementEarned += (_, a) => awarded.Add(a);

        await service.CheckAndAwardAchievementsAsync();

        awarded.Should().Contain(a => a.Id == "milestone_first_day");
    }

    [TestMethod]
    public async Task CheckAndAwardAchievementsAsync_NoNewAwards_NoEventsRaised()
    {
        var service = new AchievementService(NewDataServiceMock().Object);
        var raised = 0;
        service.AchievementEarned += (_, _) => raised++;

        await service.CheckAndAwardAchievementsAsync();

        raised.Should().Be(0);
    }

    [TestMethod]
    public async Task GetProgressAsync_AlreadyEarned_ReturnsOne()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetEarnedAchievementsAsync()).ReturnsAsync(new[]
        {
            new EarnedAchievement { AchievementId = "streak_7", EarnedAt = DateTime.UtcNow },
        });
        var service = new AchievementService(data.Object);

        var progress = await service.GetProgressAsync("streak_7");

        progress.Should().Be(1.0);
    }

    [TestMethod]
    public async Task GetProgressAsync_UnknownId_ReturnsZero()
    {
        var service = new AchievementService(NewDataServiceMock().Object);

        var progress = await service.GetProgressAsync("not_real");

        progress.Should().Be(0);
    }

    [TestMethod]
    public async Task GetProgressAsync_PartialProgress_ReturnsClampedRatio()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(3);
        var service = new AchievementService(data.Object);

        var progress = await service.GetProgressAsync("streak_7");

        progress.Should().BeApproximately(3.0 / 7.0, 1e-6);
    }

    [TestMethod]
    public async Task GetProgressAsync_OverTarget_ClampedAtOne()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(100);
        var service = new AchievementService(data.Object);

        var progress = await service.GetProgressAsync("streak_7");

        progress.Should().Be(1.0);
    }

    [TestMethod]
    public async Task GetCurrentValueAsync_Streak_ReturnsMaxOfCurrentAndLongest()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetCurrentStreakAsync()).ReturnsAsync(3);
        data.Setup(d => d.GetLongestStreakAsync()).ReturnsAsync(15);
        var service = new AchievementService(data.Object);

        var value = await service.GetCurrentValueAsync("streak_7");

        value.Should().Be(15);
    }

    [TestMethod]
    public async Task GetCurrentValueAsync_Completion_ReturnsPerfectDays()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetPerfectDaysCountAsync()).ReturnsAsync(42);
        var service = new AchievementService(data.Object);

        var value = await service.GetCurrentValueAsync("completion_10");

        value.Should().Be(42);
    }

    [TestMethod]
    [DataRow("milestone_first_day")]
    [DataRow("milestone_first_week")]
    [DataRow("milestone_first_month")]
    public async Task GetCurrentValueAsync_DayMilestones_ReturnTotalDaysTracked(string id)
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetTotalDaysTrackedAsync()).ReturnsAsync(20);
        var service = new AchievementService(data.Object);

        var value = await service.GetCurrentValueAsync(id);

        value.Should().Be(20);
    }

    [TestMethod]
    public async Task GetCurrentValueAsync_PerfectMilestone_ReturnsPerfectDays()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetPerfectDaysCountAsync()).ReturnsAsync(5);
        var service = new AchievementService(data.Object);

        var value = await service.GetCurrentValueAsync("milestone_first_perfect");

        value.Should().Be(5);
    }

    [TestMethod]
    public async Task GetCurrentValueAsync_ItemSpecific_ReturnsCompletionCount()
    {
        var sampleItem = AchievementDefinitions.GetByType(AchievementType.ItemSpecific).First();

        var data = NewDataServiceMock();
        data.Setup(d => d.GetItemCompletionCountAsync(sampleItem.ItemId!)).ReturnsAsync(7);
        var service = new AchievementService(data.Object);

        var value = await service.GetCurrentValueAsync(sampleItem.Id);

        value.Should().Be(7);
    }

    [TestMethod]
    public async Task GetUnseenCountAsync_DelegatesToDataService()
    {
        var data = NewDataServiceMock();
        data.Setup(d => d.GetUnseenAchievementCountAsync()).ReturnsAsync(3);
        var service = new AchievementService(data.Object);

        var count = await service.GetUnseenCountAsync();

        count.Should().Be(3);
    }

    [TestMethod]
    public async Task MarkAllAsSeenAsync_DelegatesToDataService()
    {
        var data = NewDataServiceMock();
        var service = new AchievementService(data.Object);

        await service.MarkAllAsSeenAsync();

        data.Verify(d => d.MarkAllAchievementsAsSeenAsync(), Times.Once);
    }
}
