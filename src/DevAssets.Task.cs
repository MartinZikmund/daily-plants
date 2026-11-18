// Compiled by RoslynCodeTaskFactory (src/DevAssets.targets) and by DailyPlants.Tests. The factory has no implicit
// usings and targets netstandard2.0 under Visual Studio's MSBuild: explicit usings, netstandard2.0 APIs, no records.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DailyPlants.Build;

/// <summary>The platform mask an image is shown through.</summary>
public enum DevBadgeMask
{
    None,
    AndroidAdaptive,
    AndroidSplash,
    IosIcon,
    TvIcon,
}

/// <summary>A rounded rectangle.</summary>
public sealed class BadgeBox(double x, double y, double width, double height, double radius)
{
    public double X { get; } = x;

    public double Y { get; } = y;

    public double Width { get; } = width;

    public double Height { get; } = height;

    public double Radius { get; } = radius;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public BadgeBox Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height, Radius);

    /// <summary>Maps the box into a unit square whose top-left corner is (<paramref name="originX"/>, <paramref name="originY"/>).</summary>
    public BadgeBox Normalize(double originX, double originY, double size) =>
        new((X - originX) / size, (Y - originY) / size, Width / size, Height / size, Radius / size);
}

/// <summary>The badge's text, drawn as outlines, and its colours.</summary>
public sealed class BadgeLabel(string text, string outline, double advance, string fill, string textFill)
{
    // Outlines from Selawik Semibold 1.01 (© Microsoft, SIL Open Font License 1.1), so the badge never depends on the build
    // machine's fonts. Font units (2048 per em), baseline at y = 0, y pointing down.
    public static readonly BadgeLabel Dev = new("DEV", "M172 -1433H671Q1034 -1433 1219 -1261Q1404 -1089 1404 -734Q1404 -510 1311 -344Q1218 -179 1048 -90Q879 0 656 0L172 2ZM658 -225Q808 -225 914 -284Q1021 -343 1076 -456Q1132 -568 1132 -728Q1132 -971 1014 -1090Q897 -1210 667 -1209L437 -1207V-225ZM1657 -1434H2452V-1215H1922V-837H2390V-616H1922V-219H2484V0H1657ZM2569 -1433H2857L3191 -380Q3215 -308 3221 -253H3225Q3229 -281 3238 -316Q3248 -350 3259 -383L3603 -1434L3881 -1433L3373 1H3070Z", 3892, "#FFB900", "#141414");

    // CI builds (a staging deployment, say) are labelled in blue so they're never mistaken for a local build.
    public static readonly BadgeLabel CI = new("CI", "M80 -713Q80 -949 170 -1117Q260 -1285 432 -1372Q604 -1460 846 -1460Q996 -1460 1170 -1416L1101 -1173Q1054 -1189 976 -1199Q897 -1209 832 -1209Q682 -1209 576 -1151Q469 -1093 414 -984Q358 -874 358 -721Q358 -567 406 -457Q455 -347 548 -290Q642 -232 773 -232Q911 -232 1125 -301L1187 -58Q1089 -20 974 0Q860 20 750 20Q538 20 388 -66Q237 -152 158 -316Q80 -481 80 -713ZM1450 -1434H1714V0H1450Z", 1889, "#0078D4", "#FFFFFF");

    public string Text { get; } = text;

    public string Outline { get; } = outline;

    public double Advance { get; } = advance;

    public string Fill { get; } = fill;

    public string TextFill { get; } = textFill;

    /// <summary>"DEV" or "CI" in any case; null for anything else.</summary>
    public static BadgeLabel? Parse(string? text) => text?.Trim().ToUpperInvariant() switch
    {
        "DEV" => Dev,
        "CI" => CI,
        _ => null,
    };
}

/// <summary>Stamps the Dev-channel badge onto an SVG.</summary>
public static class DevBadge
{
    // The in-app DevChannelBadge (Padding 6,2, 10px SemiBold text, CornerRadius 4, ~17.3px tall) scaled to 28% of
    // the image. Fractions of the image's shorter side.
    public const double HeightRatio = 0.28;
    public const double CornerRadiusRatio = HeightRatio * 4 / InAppHeight;
    public const double PaddingRatio = HeightRatio * 6 / InAppHeight;
    public const double TextSizeRatio = HeightRatio * 10 / InAppHeight;
    const double InAppHeight = 17.3;

    // Visible regions in frame units: the square the image is fitted into, before ForegroundScale / Scale.
    const double AndroidAdaptiveSafeRadius = 33.0 / 108.0; // 66dp safe zone of the 108dp adaptive layer
    const double AndroidSplashRadius = 0.484;              // measured: Android 12+ crops the splash drawable to this radius
    const double IosCornerRatio = 0.2237;
    const double TvIconSafeHalfHeight = 111.0 / 240.0;     // parallax crops the icon: Apple's safe zone is 222 of its 240 height
    const double Margin = 0.01;
    const double PullStep = 0.0025;

    // Selawik Semibold metrics shared by every BadgeLabel.
    const double UnitsPerEm = 2048;
    const double CapHeight = 1434;

    static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>
    /// Returns the badge in viewBox units: flush bottom-centre, then lifted towards the centre until the mask shows all of it.
    /// </summary>
    public static BadgeBox Place(double viewBoxX, double viewBoxY, double viewBoxWidth, double viewBoxHeight, DevBadgeMask mask, double scale, BadgeLabel label, out bool fits)
    {
        double side = Math.Min(viewBoxWidth, viewBoxHeight);
        double width = (TextSizeRatio * label.Advance / UnitsPerEm + 2 * PaddingRatio) * side;
        double height = HeightRatio * side;
        BadgeBox flush = new(viewBoxX + (viewBoxWidth - width) / 2, viewBoxY + viewBoxHeight - height, width, height, CornerRadiusRatio * side);

        // Resizetizer fits the image into a square canvas and scales it about the centre, so masks live on that square.
        double frame = Math.Max(viewBoxWidth, viewBoxHeight);
        double frameX = viewBoxX + (viewBoxWidth - frame) / 2;
        double frameY = viewBoxY + (viewBoxHeight - frame) / 2;

        bool Visible(BadgeBox b) => IsVisible(b.Normalize(frameX, frameY, frame), mask, scale);

        // Centred on the axis, so heading for the centre is a straight lift, and a convex mask holds it wherever it can at all.
        double toCentreX = frameX + frame / 2 - (flush.X + flush.Right) / 2;
        double toCentreY = frameY + frame / 2 - (flush.Y + flush.Bottom) / 2;
        BadgeBox? placed = Pull(flush, toCentreX, toCentreY, Distance(toCentreX, toCentreY), PullStep * frame, Visible);

        fits = placed is not null;
        return placed ?? flush.Offset(toCentreX, toCentreY);
    }

    // Walks the badge along (dx, dy) in fixed steps, up to maxDistance, and returns the first position shown in full.
    static BadgeBox? Pull(BadgeBox start, double dx, double dy, double maxDistance, double step, Func<BadgeBox, bool> visible)
    {
        double length = Distance(dx, dy);
        for (int i = 0; i * step <= maxDistance + step / 2; i++)
        {
            double distance = length == 0 ? 0 : i * step / length;
            BadgeBox moved = start.Offset(dx * distance, dy * distance);
            if (visible(moved))
            {
                return moved;
            }

            if (length == 0)
            {
                break;
            }
        }

        return null;
    }

    /// <summary>Returns <paramref name="svg"/> with the badge drawn on top; the original drawing is nested unchanged.</summary>
    /// <exception cref="FormatException">The root isn't &lt;svg&gt; or has no usable viewBox, width or height.</exception>
    /// <exception cref="XmlException">The SVG isn't well-formed.</exception>
    public static string Compose(string svg, DevBadgeMask mask, double scale, BadgeLabel label, out bool fits)
    {
        XElement original = Load(svg);
        double[] viewBox = ReadViewBox(original);
        double x = viewBox[0];
        double y = viewBox[1];
        double width = viewBox[2];
        double height = viewBox[3];
        BadgeBox badge = Place(x, y, width, height, mask, scale, label, out fits);

        XElement wrapper = new(Svg + "svg", new XAttribute("viewBox", string.Join(" ", viewBox.Select(v => Format(v)))));
        foreach (string size in new[] { "width", "height" })
        {
            if (original.Attribute(size) is { } attribute)
            {
                wrapper.SetAttributeValue(size, attribute.Value);
            }
        }

        // With an identical viewBox the original maps 1:1 and keeps its own namespaces, defs and ids.
        original.SetAttributeValue("x", Format(x));
        original.SetAttributeValue("y", Format(y));
        original.SetAttributeValue("width", Format(width));
        original.SetAttributeValue("height", Format(height));
        wrapper.Add(original);
        wrapper.Add(BadgeElement(badge, Math.Min(width, height), label));
        return wrapper.ToString(SaveOptions.DisableFormatting);
    }

    static XElement BadgeElement(BadgeBox badge, double side, BadgeLabel label)
    {
        double glyphScale = TextSizeRatio * side / UnitsPerEm;
        double textX = badge.X + (badge.Width - label.Advance * glyphScale) / 2;
        double baseline = badge.Y + (badge.Height + CapHeight * glyphScale) / 2;
        return new XElement(Svg + "g",
            new XAttribute("id", "dev-badge"),
            new XElement(Svg + "rect",
                new XAttribute("x", Format(badge.X)),
                new XAttribute("y", Format(badge.Y)),
                new XAttribute("width", Format(badge.Width)),
                new XAttribute("height", Format(badge.Height)),
                new XAttribute("rx", Format(badge.Radius)),
                new XAttribute("fill", label.Fill)),
            new XElement(Svg + "path",
                new XAttribute("transform", $"translate({Format(textX)} {Format(baseline)}) scale({Format(glyphScale, "0.#########")})"),
                new XAttribute("fill", label.TextFill),
                new XAttribute("d", label.Outline)));
    }

    static XElement Load(string svg)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using XmlReader reader = XmlReader.Create(new StringReader(svg), settings);
        XElement root = XDocument.Load(reader).Root ?? throw new FormatException("The SVG has no root element.");
        if (root.Name.LocalName != "svg")
        {
            throw new FormatException($"The root element is <{root.Name.LocalName}>, not <svg>.");
        }

        // Without this, a nested SVG that omits xmlns serialises as xmlns="" and renderers skip it.
        foreach (XElement element in root.DescendantsAndSelf().Where(e => e.Name.Namespace == XNamespace.None).ToList())
        {
            element.Name = Svg + element.Name.LocalName;
        }

        return root;
    }

    static double[] ReadViewBox(XElement root)
    {
        string? viewBox = (string?)root.Attribute("viewBox");
        if (!string.IsNullOrWhiteSpace(viewBox))
        {
            double[] parts = viewBox!.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseNumber).ToArray();
            if (parts.Length == 4 && parts[2] > 0 && parts[3] > 0)
            {
                return parts;
            }

            throw new FormatException($"The viewBox '{viewBox}' is not four numbers with a positive size.");
        }

        return [0, 0, ReadLength(root, "width"), ReadLength(root, "height")];
    }

    static double ReadLength(XElement root, string name)
    {
        string raw = ((string?)root.Attribute(name) ?? "").Trim();
        if (raw.EndsWith("px", StringComparison.Ordinal))
        {
            raw = raw.Substring(0, raw.Length - 2);
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0)
        {
            return value;
        }

        throw new FormatException($"The SVG has no viewBox and its {name} '{raw}' is not a plain number.");
    }

    static double ParseNumber(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    static string Format(double value, string pattern = "0.####") => value.ToString(pattern, CultureInfo.InvariantCulture);

    /// <summary>Whether a badge, in frame units (0..1), is fully visible through the mask.</summary>
    public static bool IsVisible(BadgeBox badge, DevBadgeMask mask, double scale) => mask switch
    {
        DevBadgeMask.AndroidAdaptive => InsideCircle(badge, AndroidAdaptiveSafeRadius / scale - Margin),
        DevBadgeMask.AndroidSplash => InsideCircle(badge, AndroidSplashRadius / scale - Margin),
        DevBadgeMask.IosIcon => InsideRoundedSquare(badge, 0.5 / scale - Margin, Math.Max(0, IosCornerRatio / scale - Margin)),
        DevBadgeMask.TvIcon => InsideRoundedSquare(badge, TvIconSafeHalfHeight / scale - Margin, 0),
        _ => true,
    };

    // Every mask is convex, so the rounded badge is inside it exactly when its four corner discs are.
    static bool InsideCircle(BadgeBox badge, double radius) =>
        CornerCentres(badge).All(c => Distance(c.X - 0.5, c.Y - 0.5) + badge.Radius <= radius);

    static bool InsideRoundedSquare(BadgeBox badge, double halfSide, double cornerRadius)
    {
        // A disc fits a rounded square when its centre fits the square shrunk by the disc's radius.
        double half = halfSide - badge.Radius;
        double corner = Math.Max(0, cornerRadius - badge.Radius);
        double straight = half - corner;
        return CornerCentres(badge).All(c =>
        {
            double dx = Math.Abs(c.X - 0.5);
            double dy = Math.Abs(c.Y - 0.5);
            return dx <= half && dy <= half && (dx <= straight || dy <= straight || Distance(dx - straight, dy - straight) <= corner);
        });
    }

    static IEnumerable<(double X, double Y)> CornerCentres(BadgeBox badge)
    {
        yield return (badge.X + badge.Radius, badge.Y + badge.Radius);
        yield return (badge.Right - badge.Radius, badge.Y + badge.Radius);
        yield return (badge.X + badge.Radius, badge.Bottom - badge.Radius);
        yield return (badge.Right - badge.Radius, badge.Bottom - badge.Radius);
    }

    static double Distance(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);
}

/// <summary>Which mask and scale an asset is rendered with, resolved the way Uno.Resizetizer reads them.</summary>
public sealed class DevAssetPlacement(DevBadgeMask mask, double scale)
{
    public DevBadgeMask Mask { get; } = mask;

    public double Scale { get; } = scale;

    public static DevAssetPlacement Resolve(bool isIcon, string targetPlatform, ITaskItem asset)
    {
        string platform = targetPlatform.ToLowerInvariant();
        string metadata = isIcon ? "ForegroundScale" : "Scale";

        // A platform override (e.g. AndroidForegroundScale) wins, as in Resizetizer's ResizeImageInfo.
        double scale = ReadScale(asset, PlatformPrefix(platform) + metadata) ?? ReadScale(asset, metadata) ?? 1.0;
        DevBadgeMask mask = (platform, isIcon) switch
        {
            ("android", true) => DevBadgeMask.AndroidAdaptive,
            ("android", false) => DevBadgeMask.AndroidSplash,
            ("ios", true) => DevBadgeMask.IosIcon,
            ("tvos", true) => DevBadgeMask.TvIcon,
            _ => DevBadgeMask.None,
        };
        return new DevAssetPlacement(mask, scale);
    }

    static string PlatformPrefix(string platform) => platform switch
    {
        "android" => "Android",
        "ios" => "IOS",
        "tvos" => "TvOS",
        "windows" => "Windows",
        "browserwasm" => "Wasm",
        _ => "Skia",
    };

    static double? ReadScale(ITaskItem asset, string name) =>
        double.TryParse(asset.GetMetadata(name), NumberStyles.Number, CultureInfo.InvariantCulture, out double value) && value > 0 ? value : null;
}

// The base type is fully qualified: the test project's implicit usings also bring System.Threading.Tasks.Task into scope.

/// <summary>
/// Replaces each UnoIcon foreground (Kind=Icon) or UnoSplashScreen image (Kind=Splash) with a badged copy at
/// OutputRoot/&lt;content hash&gt;/&lt;original file name&gt;, so every generated resource name stays the same.
/// </summary>
public sealed class ComposeDevAsset : Microsoft.Build.Utilities.Task
{
    static readonly UTF8Encoding Utf8 = new(false);

    [Required]
    public ITaskItem[] Assets { get; set; } = [];

    /// <summary>"Icon" rewrites ForegroundFile; "Splash" rewrites the item spec.</summary>
    [Required]
    public string Kind { get; set; } = "";

    public string TargetPlatform { get; set; } = "";

    [Required]
    public string ProjectDirectory { get; set; } = "";

    [Required]
    public string OutputRoot { get; set; } = "";

    /// <summary>"DEV" (default) or "CI".</summary>
    public string Label { get; set; } = "DEV";

    [Output]
    public ITaskItem[] Result { get; set; } = [];

    public override bool Execute()
    {
        bool isIcon = string.Equals(Kind, "Icon", StringComparison.OrdinalIgnoreCase);
        string root = Path.GetFullPath(Path.Combine(ProjectDirectory, OutputRoot));
        BadgeLabel? label = BadgeLabel.Parse(Label);
        if (label is null)
        {
            Log.LogWarning(null, "DEVASSETS003", null, null, 0, 0, 0, 0, "Unknown Dev badge label '{0}'; use DEV or CI. Drawing DEV.", Label);
            label = BadgeLabel.Dev;
        }

        Result = Assets.Select(asset => Compose(asset, isIcon, root, label)).ToArray();
        return !Log.HasLoggedErrors;
    }

    ITaskItem Compose(ITaskItem asset, bool isIcon, string root, BadgeLabel label)
    {
        string file = isIcon ? asset.GetMetadata("ForegroundFile") : asset.ItemSpec;
        if (file.Length == 0)
        {
            return asset;
        }

        string source = Path.GetFullPath(Path.Combine(ProjectDirectory, file));
        if (source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return asset;
        }

        DevAssetPlacement placement = DevAssetPlacement.Resolve(isIcon, TargetPlatform, asset);
        string composed;
        bool fits;
        try
        {
            composed = DevBadge.Compose(File.ReadAllText(source), placement.Mask, placement.Scale, label, out fits);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or FormatException)
        {
            Log.LogWarning(null, "DEVASSETS001", null, source, 0, 0, 0, 0, "Dev badge skipped, the original artwork is used instead: {0}", ex.Message);
            return asset;
        }

        if (!fits)
        {
            Log.LogWarning(null, "DEVASSETS002", null, source, 0, 0, 0, 0, "The Dev badge doesn't fit inside the {0} mask at scale {1} and may be clipped.", placement.Mask, placement.Scale.ToString(CultureInfo.InvariantCulture));
        }

        string badged = Publish(composed, root, Path.GetFileName(source));
        if (isIcon)
        {
            TaskItem icon = new(asset);
            icon.SetMetadata("ForegroundFile", badged);
            return icon;
        }

        return new TaskItem(badged, asset.CloneCustomMetadata());
    }

    static string Publish(string content, string root, string fileName)
    {
        byte[] bytes = Utf8.GetBytes(content);
        string hash;
        using (SHA256 sha = SHA256.Create())
        {
            hash = string.Concat(sha.ComputeHash(bytes).Take(6).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        string directory = Path.Combine(root, hash);
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path))
        {
            return path;
        }

        // Android's inner builds compose the same asset from several project instances at once, so publish atomically.
        Directory.CreateDirectory(directory);
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temp, bytes);
        try
        {
            File.Move(temp, path);
        }
        catch (IOException)
        {
            // Another instance won the race with identical content.
            File.Delete(temp);
        }

        return path;
    }
}
