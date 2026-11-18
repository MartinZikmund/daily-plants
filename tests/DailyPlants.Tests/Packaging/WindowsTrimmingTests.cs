using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class WindowsTrimmingTests
{
    // Trimming these away crashes the Release package at launch with 0xc000027b, while Debug runs fine.
    [TestMethod]
    public void TrimmerRoots_KeepXamlMarkup()
    {
        var roots = XDocument.Load(Path.Combine(ProjectDirectory(), "TrimmerRoots.xml"));

        var winUiNamespaces = roots.Descendants("assembly")
            .Where(a => (string?)a.Attribute("fullname") == "Microsoft.WinUI")
            .Elements("namespace")
            .Select(n => (string?)n.Attribute("fullname"));

        winUiNamespaces.Should().Contain(["Microsoft.UI.Xaml.Markup", "ABI.Microsoft.UI.Xaml.Markup"]);
    }

    [TestMethod]
    public void Project_PassesTrimmerRootsToTheWindowsHead()
    {
        var project = XDocument.Load(Path.Combine(ProjectDirectory(), "DailyPlants.csproj"));

        var descriptors = project.Descendants("TrimmerRootDescriptor")
            .Where(d => (string?)d.Attribute("Include") == "TrimmerRoots.xml");

        descriptors.Should().ContainSingle()
            .Which.Parent!.Attribute("Condition")!.Value.Should().Contain("IsWinAppSdk");
    }

    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return Path.Combine(directory!.FullName, "src", "DailyPlants");
    }
}
