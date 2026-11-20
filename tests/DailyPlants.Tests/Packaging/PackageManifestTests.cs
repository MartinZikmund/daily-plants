using System.Xml.Linq;

namespace DailyPlants.Tests.Packaging;

[TestClass]
public class PackageManifestTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
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

    [TestMethod]
    public void Identity_Version_IsTheNbgvPlaceholder()
    {
        var identity = XDocument.Load(FindManifest()).Descendants(Foundation + "Identity").Single();

        ((string?)identity.Attribute("Version")).Should().Be("0.0.0.0", "the build stamps the real version");
    }

    // Prod ships the checked-in identity as is, and Partner Center rejects any other.
    [TestMethod]
    public void Identity_IsThePartnerCenterProductIdentity()
    {
        var identity = XDocument.Load(FindManifest()).Descendants(Foundation + "Identity").Single();

        ((string?)identity.Attribute("Name")).Should().Be("5143MartinZikmund.DailyPlants");
        ((string?)identity.Attribute("Publisher")).Should().Be("CN=5EDFBB6C-AD5D-4B76-B479-1C5F44017714");
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
