using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class AndroidManifestTests
{
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    // Debug builds get INTERNET added for the debugger, Release builds only get what the manifest declares.
    [TestMethod]
    public void Manifest_DeclaresInternetPermission()
    {
        var manifest = XDocument.Load(FindManifest());

        var permissions = manifest.Root!.Elements("uses-permission").Select(p => (string?)p.Attribute(Android + "name"));

        permissions.Should().Contain("android.permission.INTERNET", "the Resources page loads the NutritionFacts.org feeds");
    }

    private static string FindManifest()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return Path.Combine(directory!.FullName, "src", "DailyPlants", "Platforms", "Android", "AndroidManifest.xml");
    }
}
