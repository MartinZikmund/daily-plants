using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class PackageManifestTests
{
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    // Left out, Uno Resizetizer fills it from the icon's transparent #00000000 with the alpha dropped,
    // which is opaque black: the Store and Windows then draw the icon on a black plate.
    [TestMethod]
    public void VisualElements_BackgroundColor_IsExplicitlyTransparent()
    {
        var manifest = XDocument.Load(FindManifest());

        var visualElements = manifest.Descendants(Uap + "VisualElements").Single();

        ((string?)visualElements.Attribute("BackgroundColor")).Should().Be("transparent");
    }

    private static string FindManifest()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return Path.Combine(directory!.FullName, "src", "DailyPlants", "Package.appxmanifest");
    }
}
