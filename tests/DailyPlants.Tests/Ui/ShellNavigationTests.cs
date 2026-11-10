using System.Xml.Linq;

namespace DailyPlants.Tests.Ui;

[TestClass]
public class ShellNavigationTests
{
    // Five 64px tabs fill a 320px screen; a sixth would not fit.
    private const int MaxTabs = 5;

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void EverySection_IsReachableFromThePhoneTabBar()
    {
        var shell = LoadXaml("Views", "ShellView.xaml");

        shell.Descendants(Presentation + "NavigationViewItem").Select(Tag)
            .Should().BeEquivalentTo(TabTags(shell), "phones hide the pane entirely, so a section without a tab is unreachable");
    }

    [TestMethod]
    public void PhoneTabBar_FitsOnASmallPhone()
    {
        TabTags(LoadXaml("Views", "ShellView.xaml")).Should().HaveCountLessThanOrEqualTo(MaxTabs);
    }

    [TestMethod]
    public void About_LivesInSettings()
    {
        File.Exists(Path.Combine(RepoRoot(), "src", "DailyPlants", "Views", "AboutView.xaml")).Should().BeFalse();

        LoadXaml("Views", "SettingsView.xaml").Descendants()
            .Select(e => e.Attribute("AutomationProperties.AutomationId")?.Value)
            .Should().Contain(["AboutAppCard", "AboutVersionText", "NutritionFactsLink", "Icons8Link"]);
    }

    private static IEnumerable<string?> TabTags(XDocument shell) =>
        shell.Descendants(Presentation + "Grid")
            .Single(g => g.Attribute(Xaml + "Name")?.Value == "TabBar")
            .Elements(Presentation + "RadioButton")
            .Select(Tag);

    private static string? Tag(XElement element) => element.Attribute("Tag")?.Value;

    private static XDocument LoadXaml(params string[] path)
        => XDocument.Load(Path.Combine([RepoRoot(), "src", "DailyPlants", .. path]));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return directory!.FullName;
    }
}
