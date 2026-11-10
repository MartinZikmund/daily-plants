using System.Xml.Linq;

namespace DailyPlants.Tests.Ui;

[TestClass]
public class ShellNavigationTests
{
    // Four 80px tabs fit a 320px screen with labels at full size.
    private const int MaxTabs = 4;
    private const string ProgressTab = "Progress";

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void EverySection_IsReachableOnAPhone()
    {
        var shell = LoadXaml("Views", "ShellView.xaml");

        var reachable = TabTags(shell).Where(tag => tag != ProgressTab).Concat(ProgressSectionTags(shell));

        shell.Descendants(Presentation + "NavigationViewItem").Select(Tag)
            .Should().BeEquivalentTo(reachable, "phones hide the pane entirely, so a section without a tab is unreachable");
    }

    [TestMethod]
    public void PhoneTabBar_FitsOnASmallPhone()
    {
        TabTags(LoadXaml("Views", "ShellView.xaml")).Should().HaveCountLessThanOrEqualTo(MaxTabs);
    }

    [TestMethod]
    public void ProgressTab_IsLocalizedInEveryLanguage()
    {
        var strings = Path.Combine(RepoRoot(), "src", "DailyPlants", "Strings");

        foreach (var file in Directory.GetFiles(strings, "Resources.resw", SearchOption.AllDirectories))
        {
            XDocument.Load(file).Root!.Elements("data")
                .Should().Contain(d => d.Attribute("name")!.Value == "Nav_Progress", file);
        }
    }

    private static IEnumerable<string?> ProgressSectionTags(XDocument shell) =>
        shell.Descendants(Presentation + "SelectorBar")
            .Single(s => s.Attribute(Xaml + "Name")?.Value == "ProgressSelector")
            .Elements(Presentation + "SelectorBarItem")
            .Select(Tag);

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
