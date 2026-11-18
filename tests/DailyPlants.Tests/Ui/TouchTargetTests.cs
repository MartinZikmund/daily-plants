using System.Xml.Linq;

namespace DailyPlants.Tests.Ui;

[TestClass]
public class TouchTargetTests
{
    // Fluent's minimum for touch; a fingertip is roughly this wide.
    private const double MinimumTouchTarget = 40;

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    [DataRow("Width")]
    [DataRow("Height")]
    public void StepperButton_IsLargeEnoughToTap(string dimension)
    {
        var style = LoadXaml("Styles", "Controls.xaml").Root!
            .Elements(Presentation + "Style")
            .Single(s => s.Attribute(Xaml + "Key")?.Value == "DpStepperButtonStyle");

        var value = style.Elements(Presentation + "Setter")
            .Single(s => s.Attribute("Property")?.Value == dimension)
            .Attribute("Value")!.Value;

        double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeGreaterThanOrEqualTo(MinimumTouchTarget, "a near-miss on - or + would otherwise open the item details");
    }

    [TestMethod]
    public void ServingStepper_CatchesTapsBetweenItsButtons()
    {
        var root = LoadXaml("Controls", "ServingStepper.xaml").Root!.Elements().Single();

        root.Attribute("Background").Should().NotBeNull("taps on the gaps around the dots would fall through to the row and open the details");
    }

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
