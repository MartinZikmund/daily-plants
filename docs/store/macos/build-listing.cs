using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Builds the metadata and screenshots folders for `fastlane deliver --platform osx` from listing/<locale>.md and the
// slides screenshots/render-slides.sh renders into artifacts/store/macos/images.
// Usage: dotnet run build-listing.cs [-- --check-only] [-- --out <dir>]
string root = Path.GetDirectoryName(ScriptPath())!;
string outDir = Path.Combine(root, "../../../artifacts/store/macos");
string imagesDir = Path.Combine(root, "../../../artifacts/store/macos/images");
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

// Only the fields each platform has its own copy of. The name, subtitle, privacy URL and categories belong to the
// whole app, so they stay as the iOS listing sets them.
Dictionary<string, (string File, int Limit, bool Required)> textFields = new()
{
    ["PromotionalText"] = ("promotional_text.txt", 170, false),
    ["Description"] = ("description.txt", 4000, true),
    ["ReleaseNotes"] = ("release_notes.txt", 4000, false),
};
const int KeywordsLimit = 100;

Dictionary<string, string> perLocale = new()
{
    ["support_url.txt"] = "https://github.com/MartinZikmund/daily-plants",
    ["marketing_url.txt"] = "https://github.com/MartinZikmund/daily-plants",
};
Dictionary<string, string> shared = new()
{
    ["copyright.txt"] = $"{DateTime.Now.Year} Martin Zikmund",
};

// Every screenshot goes to the primary language, and the other languages fall back to it.
const string PrimaryLocale = "en-US";
// The Mac App Store takes 16:10 screenshots of 1280x800, 1440x900, 2560x1600 or 2880x1800.
(int Width, int Height)[] macSizes = [(1280, 800), (1440, 900), (2560, 1600), (2880, 1800)];

List<string> errors = new();
Dictionary<string, Dictionary<string, string>> listings = new();
foreach (string path in Directory.GetFiles(Path.Combine(root, "listing"), "*.md").Order())
{
    string locale = Path.GetFileNameWithoutExtension(path);
    Dictionary<string, string> listing = ReadListing(path);
    listings[locale] = listing;

    foreach ((string field, (_, int limit, bool required)) in textFields)
    {
        if (!listing.TryGetValue(field, out string? value) || value.Length == 0)
        {
            if (required)
            {
                errors.Add($"{locale}: '## {field}' is missing.");
            }
            continue;
        }
        int length = new StringInfo(value).LengthInTextElements;
        if (length > limit)
        {
            errors.Add($"{locale}: {field} is {length} characters, the limit is {limit}.");
        }
    }

    string keywords = Keywords(listing);
    if (new StringInfo(keywords).LengthInTextElements > KeywordsLimit)
    {
        errors.Add($"{locale}: Keywords are {keywords.Length} characters with the commas, the limit is {KeywordsLimit}.");
    }

    foreach (string unknown in listing.Keys.Except(textFields.Keys).Except(["Keywords"]))
    {
        errors.Add($"{locale}: '## {unknown}' isn't a field the Mac listing sets.");
    }
}

if (!listings.ContainsKey(PrimaryLocale))
{
    errors.Add($"listing/{PrimaryLocale}.md is missing.");
}

string[] images = Directory.Exists(imagesDir)
    ? Directory.GetFiles(imagesDir, "*.png").Order().ToArray()
    : [];
foreach (string image in images)
{
    (int width, int height) = PngSize(image);
    if (!macSizes.Contains((width, height)))
    {
        errors.Add($"{Path.GetFileName(image)} is {width}x{height}, which isn't a Mac App Store screenshot size.");
    }
}
// A text-only check doesn't need the slides rendered.
if (images.Length == 0 && !checkOnly)
{
    errors.Add($"No slides in {Path.GetFullPath(imagesDir)}. Run screenshots/render-slides.sh first.");
}

if (errors.Count > 0)
{
    errors.ForEach(e => Console.Error.WriteLine(e));
    return 1;
}
Console.WriteLine($"{listings.Count} listings and {images.Length} screenshots are within the Mac App Store's limits.");
if (checkOnly)
{
    return 0;
}

string metadataDir = Path.Combine(outDir, "metadata");
string screenshotsDir = Path.Combine(outDir, "screenshots");
foreach (string dir in new[] { metadataDir, screenshotsDir })
{
    if (Directory.Exists(dir))
    {
        Directory.Delete(dir, recursive: true);
    }
    Directory.CreateDirectory(dir);
}

foreach ((string file, string value) in shared)
{
    File.WriteAllText(Path.Combine(metadataDir, file), value);
}

foreach ((string locale, Dictionary<string, string> listing) in listings)
{
    string dir = Directory.CreateDirectory(Path.Combine(metadataDir, locale)).FullName;
    foreach ((string field, (string file, _, _)) in textFields)
    {
        // An empty file would make fastlane clear the field, and the first Mac version can't have release notes at all.
        if (listing.TryGetValue(field, out string? value) && value.Length > 0)
        {
            File.WriteAllText(Path.Combine(dir, file), value);
        }
    }
    File.WriteAllText(Path.Combine(dir, "keywords.txt"), Keywords(listing));
    foreach ((string file, string value) in perLocale)
    {
        File.WriteAllText(Path.Combine(dir, file), value);
    }
}

// fastlane tells the devices apart by the image size and uploads them in file name order.
string localeScreenshots = Directory.CreateDirectory(Path.Combine(screenshotsDir, PrimaryLocale)).FullName;
for (int i = 0; i < images.Length; i++)
{
    File.Copy(images[i], Path.Combine(localeScreenshots, $"{i + 1}_mac.png"));
}

Console.WriteLine($"Wrote {listings.Count} languages and {images.Length} screenshots to {Path.GetFullPath(outDir)}");
return 0;

static string Keywords(Dictionary<string, string> listing) =>
    string.Join(",", listing.GetValueOrDefault("Keywords", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimStart('-', ' ').Trim())
        .Where(k => k.Length > 0));

static (int Width, int Height) PngSize(string path)
{
    using FileStream stream = File.OpenRead(path);
    byte[] header = new byte[24];
    stream.ReadExactly(header);
    return (ReadBigEndian(header, 16), ReadBigEndian(header, 20));
}

static int ReadBigEndian(byte[] bytes, int offset) =>
    bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

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
