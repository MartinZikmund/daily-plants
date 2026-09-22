using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Builds the metadata folder for `fastlane supply` and the release notes for CI from listing/<locale>.md and images/.
// Usage: dotnet run build-listing.cs [-- --check-only] [-- --out <dir>]
string root = Path.GetDirectoryName(ScriptPath())!;
string outDir = Path.Combine(root, "../../../artifacts/store/android");
bool checkOnly = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--check-only")
    {
        checkOnly = true;
    }
    else if (args[i] == "--out" && i + 1 < args.Length)
    {
        outDir = args[++i];
    }
}

// Google Play's limits, in characters.
Dictionary<string, (string File, int Limit)> textFields = new()
{
    ["Title"] = ("title.txt", 30),
    ["ShortDescription"] = ("short_description.txt", 80),
    ["FullDescription"] = ("full_description.txt", 4000),
};
const string ReleaseNotesField = "ReleaseNotes";
const int ReleaseNotesLimit = 500;

// Every image goes to the primary language, and the other languages fall back to it.
const string PrimaryLocale = "en-US";
// The folder in images/, the fastlane supply folders it goes to, and the aspect ratio (width:height) and image count Play wants.
(string Source, string[] Targets, int RatioW, int RatioH, int MinCount)[] screenshotSets =
[
    ("phone", ["phoneScreenshots"], 9, 16, 4),
    ("tablet", ["sevenInchScreenshots", "tenInchScreenshots"], 16, 9, 4),
];
(string Source, string Target, int Width, int Height, bool Alpha, long MaxBytes)[] graphics =
[
    ("feature-graphic.png", "featureGraphic.png", 1024, 500, false, 15 * 1024 * 1024),
    ("icon.png", "icon.png", 512, 512, true, 1024 * 1024),
];
const long ScreenshotMaxBytes = 8 * 1024 * 1024;

List<string> errors = new();
Dictionary<string, Dictionary<string, string>> listings = new();
foreach (string path in Directory.GetFiles(Path.Combine(root, "listing"), "*.md").Order())
{
    string locale = Path.GetFileNameWithoutExtension(path);
    Dictionary<string, string> listing = ReadListing(path);
    listings[locale] = listing;

    foreach ((string field, int limit) in textFields.Select(f => (f.Key, f.Value.Limit)).Append((ReleaseNotesField, ReleaseNotesLimit)))
    {
        if (!listing.TryGetValue(field, out string? value) || value.Length == 0)
        {
            errors.Add($"{locale}: '## {field}' is missing.");
            continue;
        }
        int length = new StringInfo(value).LengthInTextElements;
        if (length > limit)
        {
            errors.Add($"{locale}: {field} is {length} characters, the limit is {limit}.");
        }
    }

    foreach (string unknown in listing.Keys.Except(textFields.Keys).Except([ReleaseNotesField]))
    {
        errors.Add($"{locale}: '## {unknown}' isn't a Google Play field.");
    }
}

if (!listings.ContainsKey(PrimaryLocale))
{
    errors.Add($"listing/{PrimaryLocale}.md is missing.");
}

Dictionary<string, string[]> screenshots = new();
foreach ((string source, _, int ratioW, int ratioH, int minCount) in screenshotSets)
{
    string dir = Path.Combine(root, "images", source);
    string[] images = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Order().ToArray() : [];
    screenshots[source] = images;
    if (images.Length < minCount || images.Length > 8)
    {
        errors.Add($"images/{source} has {images.Length} screenshots, Play wants {minCount} to 8.");
    }
    foreach (string image in images)
    {
        (int w, int h, bool alpha) = ReadPng(image);
        string name = $"images/{source}/{Path.GetFileName(image)}";
        if (w * ratioH != h * ratioW || Math.Min(w, h) < 1080)
        {
            errors.Add($"{name} is {w}x{h}, Play wants {ratioW}:{ratioH} with at least 1080 pixels on the short side.");
        }
        if (alpha)
        {
            errors.Add($"{name} has an alpha channel, which Play rejects.");
        }
        if (new FileInfo(image).Length > ScreenshotMaxBytes)
        {
            errors.Add($"{name} is over 8 MB.");
        }
    }
}

foreach ((string source, _, int width, int height, bool wantsAlpha, long maxBytes) in graphics)
{
    string path = Path.Combine(root, "images", source);
    if (!File.Exists(path))
    {
        errors.Add($"images/{source} is missing.");
        continue;
    }
    (int w, int h, bool alpha) = ReadPng(path);
    if (w != width || h != height || alpha != wantsAlpha || new FileInfo(path).Length > maxBytes)
    {
        errors.Add($"images/{source} is {w}x{h}{(alpha ? " with alpha" : "")}, Play wants {width}x{height}{(wantsAlpha ? " with alpha" : " without alpha")} and at most {maxBytes / 1024} KB.");
    }
}

if (errors.Count > 0)
{
    errors.ForEach(e => Console.Error.WriteLine(e));
    return 1;
}
Console.WriteLine($"{listings.Count} listings and the images are within Google Play's limits.");
if (checkOnly)
{
    return 0;
}

string metadataDir = Path.Combine(outDir, "metadata");
string whatsNewDir = Path.Combine(outDir, "whatsnew");
foreach (string dir in new[] { metadataDir, whatsNewDir })
{
    if (Directory.Exists(dir))
    {
        Directory.Delete(dir, recursive: true);
    }
    Directory.CreateDirectory(dir);
}

foreach ((string locale, Dictionary<string, string> listing) in listings)
{
    string dir = Directory.CreateDirectory(Path.Combine(metadataDir, locale)).FullName;
    foreach ((string field, (string file, _)) in textFields)
    {
        File.WriteAllText(Path.Combine(dir, file), listing[field]);
    }
    // The file names upload-google-play's whatsNewDirectory expects.
    File.WriteAllText(Path.Combine(whatsNewDir, $"whatsnew-{locale}"), listing[ReleaseNotesField]);
}

// fastlane supply uploads the screenshots in file name order and replaces the ones in Play.
string imagesDir = Path.Combine(metadataDir, PrimaryLocale, "images");
int copied = 0;
foreach ((string source, string[] targets, _, _, _) in screenshotSets)
{
    foreach (string target in targets)
    {
        string dir = Directory.CreateDirectory(Path.Combine(imagesDir, target)).FullName;
        string[] images = screenshots[source];
        for (int i = 0; i < images.Length; i++)
        {
            File.Copy(images[i], Path.Combine(dir, $"{i + 1}.png"));
            copied++;
        }
    }
}
foreach ((string source, string target, _, _, _, _) in graphics)
{
    File.Copy(Path.Combine(root, "images", source), Path.Combine(imagesDir, target));
    copied++;
}

Console.WriteLine($"Wrote {listings.Count} languages and {copied} images to {Path.GetFullPath(metadataDir)}");
Console.WriteLine($"Wrote the release notes to {Path.GetFullPath(whatsNewDir)}");
return 0;

// Width, height and whether the color type carries alpha, from the IHDR chunk.
static (int Width, int Height, bool Alpha) ReadPng(string path)
{
    Span<byte> header = stackalloc byte[26];
    using (FileStream stream = File.OpenRead(path))
    {
        stream.ReadExactly(header);
    }
    byte colorType = header[25];
    return (BinaryPrimitives.ReadInt32BigEndian(header[16..]), BinaryPrimitives.ReadInt32BigEndian(header[20..]), colorType is 4 or 6);
}

static Dictionary<string, string> ReadListing(string path)
{
    Dictionary<string, StringBuilder> sections = new();
    StringBuilder? current = null;
    foreach (string line in File.ReadLines(path, Encoding.UTF8))
    {
        Match heading = Regex.Match(line, @"^##\s+(\S+)\s*$");
        if (heading.Success)
        {
            current = new();
            if (!sections.TryAdd(heading.Groups[1].Value, current))
            {
                throw new InvalidOperationException($"{path}: '## {heading.Groups[1].Value}' appears twice.");
            }
        }
        else
        {
            current?.AppendLine(line.TrimEnd());
        }
    }
    return sections.ToDictionary(s => s.Key, s => s.Value.ToString().Trim().ReplaceLineEndings("\n"));
}

static string ScriptPath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
