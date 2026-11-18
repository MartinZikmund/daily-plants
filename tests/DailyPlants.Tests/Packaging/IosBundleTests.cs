using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class IosBundleTests
{
    // The SDK only fills CFBundleDisplayName from ApplicationTitle when Info.plist leaves it out,
    // and that is what makes the Dev build show up as "Daily Plants Dev".
    [TestMethod]
    public void InfoPlist_LeavesTheDisplayNameToTheChannel()
    {
        var plist = XDocument.Load(Path.Combine(BuildFiles.RepoRoot, "src", "DailyPlants", "Platforms", "iOS", "Info.plist"));

        var keys = plist.Root!.Element("dict")!.Elements("key").Select(key => key.Value);

        keys.Should().NotContain("CFBundleDisplayName");
    }
}
