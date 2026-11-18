using DailyPlants.Infrastructure;

namespace DailyPlants.Tests.Ui;

[TestClass]
public class DevChannelBadgeTests
{
    [TestMethod]
    [DataRow("DEV", "", "DEV")]
    [DataRow("CI", "", "CI")]
    [DataRow("DEV", "feature-x", "DEV · feature-x")]
    [DataRow("", "feature-x", "", DisplayName = "Prod never shows a worktree")]
    [DataRow("", "", "")]
    public void FormatChannelBadge_ComposesChannelAndWorktree(string channel, string worktree, string expected) =>
        AppEnvironment.FormatChannelBadge(channel, worktree).Should().Be(expected);
}
