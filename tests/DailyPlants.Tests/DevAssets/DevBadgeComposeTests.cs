using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DailyPlants.Build;

namespace DailyPlants.Tests.DevAssets;

[TestClass]
public sealed class DevBadgeComposeTests
{
    static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    const string Logo = """
        <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="450" height="450" viewBox="0 0 50 50" id="logo">
          <defs><path id="p" d="M0 0H10V10Z" /></defs>
          <use xlink:href="#p" fill="#7a67f8" />
        </svg>
        """;

    [TestMethod]
    public void Compose_Logo_NestsOriginalWithSameViewBox()
    {
        XElement result = XElement.Parse(DevBadge.Compose(Logo, DevBadgeMask.None, 1, BadgeLabel.Dev, out _));

        result.Attribute("viewBox")!.Value.Should().Be("0 0 50 50");
        result.Attribute("width")!.Value.Should().Be("450");
        XElement nested = result.Elements(Svg + "svg").Single();
        nested.Attribute("id")!.Value.Should().Be("logo");
        nested.Attribute("width")!.Value.Should().Be("50");
        nested.Attribute("height")!.Value.Should().Be("50");
        nested.Descendants(Svg + "use").Single().Attribute(XLink + "href")!.Value.Should().Be("#p");
    }

    [TestMethod]
    public void Compose_Logo_DrawsBadgeBottomCentreInViewBoxUnits()
    {
        XElement rect = Badge(DevBadge.Compose(Logo, DevBadgeMask.None, 1, BadgeLabel.Dev, out _)).Element(Svg + "rect")!;

        (Number(rect, "x") + Number(rect, "width") / 2).Should().BeApproximately(25, 1e-3);
        (Number(rect, "y") + Number(rect, "height")).Should().BeApproximately(50, 1e-3);
        Number(rect, "height").Should().BeApproximately(14, 1e-3);
        rect.Attribute("fill")!.Value.Should().Be(BadgeLabel.Dev.Fill);
    }

    [TestMethod]
    public void Compose_Logo_DrawsDevAsOutlinesNotText()
    {
        XElement badge = Badge(DevBadge.Compose(Logo, DevBadgeMask.None, 1, BadgeLabel.Dev, out _));

        badge.Descendants(Svg + "text").Should().BeEmpty();
        XElement path = badge.Element(Svg + "path")!;
        path.Attribute("fill")!.Value.Should().Be(BadgeLabel.Dev.TextFill);
        path.Attribute("d")!.Value.Should().StartWith("M172 -1433");
    }

    [TestMethod]
    public void Compose_CiLabel_DrawsBlueBadgeWithCiOutlines()
    {
        XElement badge = Badge(DevBadge.Compose(Logo, DevBadgeMask.None, 1, BadgeLabel.CI, out _));

        badge.Element(Svg + "rect")!.Attribute("fill")!.Value.Should().Be("#0078D4");
        XElement path = badge.Element(Svg + "path")!;
        path.Attribute("fill")!.Value.Should().Be("#FFFFFF");
        path.Attribute("d")!.Value.Should().StartWith("M80 -713");
    }

    [TestMethod]
    public void Compose_NoViewBox_UsesPixelWidthAndHeight()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="200px" height="100"><rect width="10" height="10" /></svg>""";

        XElement result = XElement.Parse(DevBadge.Compose(svg, DevBadgeMask.None, 1, BadgeLabel.Dev, out _));

        result.Attribute("viewBox")!.Value.Should().Be("0 0 200 100");
        Number(Badge(result).Element(Svg + "rect")!, "height").Should().BeApproximately(28, 1e-3);
    }

    [TestMethod]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" />""")]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" />""")]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 0 10" />""")]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ten 10" />""")]
    [DataRow("""<html />""")]
    public void Compose_NoUsableSvg_ThrowsFormatException(string svg)
    {
        Action compose = () => DevBadge.Compose(svg, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        compose.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void Compose_MalformedXml_ThrowsXmlException()
    {
        Action compose = () => DevBadge.Compose("<svg", DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        compose.Should().Throw<XmlException>();
    }

    [TestMethod]
    public void Compose_WithoutSvgNamespace_MovesElementsIntoIt()
    {
        const string svg = """<svg viewBox="0 0 10 10"><rect width="5" height="5" /></svg>""";

        XElement result = XElement.Parse(DevBadge.Compose(svg, DevBadgeMask.None, 1, BadgeLabel.Dev, out _));

        result.DescendantsAndSelf().Should().OnlyContain(e => e.Name.Namespace == Svg);
    }

    [TestMethod]
    public void Compose_WithDoctype_IgnoresIt()
    {
        const string svg = """<!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" />""";

        Action compose = () => DevBadge.Compose(svg, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        compose.Should().NotThrow();
    }

    [TestMethod]
    public void Compose_SameInputUnderDifferentCultures_IsIdentical()
    {
        string english = WithCulture("en-US", () => DevBadge.Compose(Logo, DevBadgeMask.AndroidAdaptive, 0.6, BadgeLabel.Dev, out _));
        string czech = WithCulture("cs-CZ", () => DevBadge.Compose(Logo, DevBadgeMask.AndroidAdaptive, 0.6, BadgeLabel.Dev, out _));

        czech.Should().Be(english);
    }

    [TestMethod]
    [DataRow(0.6, true)]
    [DataRow(3.0, false)]
    public void Compose_AndroidAdaptive_ReportsWhetherBadgeFits(double scale, bool expected)
    {
        DevBadge.Compose(Logo, DevBadgeMask.AndroidAdaptive, scale, BadgeLabel.Dev, out bool fits);

        fits.Should().Be(expected);
    }

    static XElement Badge(string svg) => Badge(XElement.Parse(svg));

    static XElement Badge(XElement root) => root.Elements(Svg + "g").Single(g => (string?)g.Attribute("id") == "dev-badge");

    static double Number(XElement element, string name) => double.Parse(element.Attribute(name)!.Value, CultureInfo.InvariantCulture);

    static T WithCulture<T>(string name, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
