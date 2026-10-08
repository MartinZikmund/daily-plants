using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class MacBundleTests
{
    [TestMethod]
    public void InfoPlist_HasAnAppStoreCategory()
    {
        var info = ReadPlist("Info.plist");

        info.Should().ContainKey("LSApplicationCategoryType")
            .WhoseValue.Value.Should().StartWith("public.app-category.", "the Mac App Store rejects a bundle without a category");
    }

    [TestMethod]
    public void InfoPlist_DeclaresNoNonExemptEncryption()
    {
        var info = ReadPlist("Info.plist");

        info.Should().ContainKey("ITSAppUsesNonExemptEncryption").WhoseValue.Name.LocalName.Should().Be("false");
    }

    [TestMethod]
    public void InfoPlist_ListsEveryAppLanguage()
    {
        var info = ReadPlist("Info.plist");
        var appLanguages = Directory.GetDirectories(Path.Combine(RepoRoot(), "src", "DailyPlants", "Strings")).Select(Path.GetFileName);

        var bundleLanguages = info["CFBundleLocalizations"].Elements("string").Select(e => e.Value);

        bundleLanguages.Should().BeEquivalentTo(appLanguages);
    }

    [TestMethod]
    public void Entitlements_AreSandboxed()
    {
        var entitlements = ReadPlist("Entitlements.plist");

        entitlements.Should().ContainKey("com.apple.security.app-sandbox").WhoseValue.Name.LocalName.Should().Be("true");
    }

    [DataTestMethod]
    [DataRow("com.apple.security.network.client", "the Resources page loads the NutritionFacts.org feeds")]
    [DataRow("com.apple.security.files.user-selected.read-write", "export and import go through the save and open panels")]
    public void Entitlements_AllowWhatTheAppUses(string key, string because)
    {
        var entitlements = ReadPlist("Entitlements.plist");

        entitlements.Should().ContainKey(key, because).WhoseValue.Name.LocalName.Should().Be("true");
    }

    private static Dictionary<string, XElement> ReadPlist(string name)
    {
        var dict = XDocument.Load(Path.Combine(RepoRoot(), "src", "DailyPlants", "Platforms", "Desktop", "macOS", name)).Root!.Element("dict")!;
        return dict.Elements("key").ToDictionary(k => k.Value, k => (XElement)k.NextNode!);
    }

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
