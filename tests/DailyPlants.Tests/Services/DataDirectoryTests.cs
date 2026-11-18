using DailyPlants.Infrastructure;

namespace DailyPlants.Tests.Services;

[TestClass]
public class DataDirectoryTests
{
    [TestMethod]
    [DataRow(false, "", "DailyPlants", DisplayName = "Prod keeps the store app's folder")]
    [DataRow(false, "feature-x", "DailyPlants", DisplayName = "Prod ignores the worktree")]
    [DataRow(true, "", "DailyPlants.Dev")]
    [DataRow(true, "feature-x", "DailyPlants.Dev.feature-x")]
    public void FormatDataFolderName_SeparatesChannelsAndWorktrees(bool isDevChannel, string worktree, string expected) =>
        AppEnvironment.FormatDataFolderName(isDevChannel, worktree).Should().Be(expected);

    [TestMethod]
    public void FormatDataFolderName_ReplacesCharactersAFolderCantHave() =>
        AppEnvironment.FormatDataFolderName(true, "a/b").Should().Be("DailyPlants.Dev.a-b");

    // The tests build on the Dev channel, so they must never resolve to the store app's data.
    [TestMethod]
    public void DataDirectory_IsNotTheStoreAppsFolder() =>
        Path.GetFileName(AppEnvironment.DataDirectory).Should().StartWith("DailyPlants.Dev");
}
