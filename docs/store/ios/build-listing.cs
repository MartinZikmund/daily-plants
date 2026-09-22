using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Builds the metadata and screenshots folders for `fastlane deliver` from listing/<locale>.md and images/.
// Usage: dotnet run build-listing.cs [-- --check-only] [-- --out <dir>]
string root = Path.GetDirectoryName(ScriptPath())!;
string outDir = Path.Combine(root, "../../../artifacts/store/ios");
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

// App Store Connect's limits, in characters.
Dictionary<string, (string File, int Limit, bool Required)> textFields = new()
{
    ["Name"] = ("name.txt", 30, true),
    ["Subtitle"] = ("subtitle.txt", 30, false),
    ["PromotionalText"] = ("promotional_text.txt", 170, false),
    ["Description"] = ("description.txt", 4000, true),
    ["ReleaseNotes"] = ("release_notes.txt", 4000, false),
};
const int KeywordsLimit = 100;

// The same in every language.
Dictionary<string, string> perLocale = new()
{
    ["support_url.txt"] = "https://github.com/MartinZikmund/daily-plants",
    ["marketing_url.txt"] = "https://github.com/MartinZikmund/daily-plants",
    ["privacy_url.txt"] = "https://mzikmund.dev/apps/dailyplants/privacypolicy",
};
Dictionary<string, string> shared = new()
{
    ["copyright.txt"] = $"{DateTime.Now.Year} Martin Zikmund",
    ["primary_category.txt"] = "HEALTH_AND_FITNESS",
    ["secondary_category.txt"] = "FOOD_AND_DRINK",
};

// Every screenshot goes to the primary language, and the other languages fall back to it.
const string PrimaryLocale = "en-US";
string[] devices = ["iphone", "ipad"];

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
        errors.Add($"{locale}: '## {unknown}' isn't an App Store field.");
    }
}

if (!listings.ContainsKey(PrimaryLocale))
{
    errors.Add($"listing/{PrimaryLocale}.md is missing.");
}

if (errors.Count > 0)
{
    errors.ForEach(e => Console.Error.WriteLine(e));
    return 1;
}
Console.WriteLine($"{listings.Count} listings are within the App Store's limits.");
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
        File.WriteAllText(Path.Combine(dir, file), listing.GetValueOrDefault(field, ""));
    }
    File.WriteAllText(Path.Combine(dir, "keywords.txt"), Keywords(listing));
    foreach ((string file, string value) in perLocale)
    {
        File.WriteAllText(Path.Combine(dir, file), value);
    }
}

// fastlane tells the devices apart by the image size and uploads them in file name order.
string localeScreenshots = Directory.CreateDirectory(Path.Combine(screenshotsDir, PrimaryLocale)).FullName;
int screenshots = 0;
foreach (string device in devices)
{
    string[] images = Directory.Exists(Path.Combine(root, "images", device))
        ? Directory.GetFiles(Path.Combine(root, "images", device), "*.png").Order().ToArray()
        : [];
    for (int i = 0; i < images.Length; i++)
    {
        File.Copy(images[i], Path.Combine(localeScreenshots, $"{i + 1}_{device}.png"));
        screenshots++;
    }
}

Console.WriteLine($"Wrote {listings.Count} languages and {screenshots} screenshots to {Path.GetFullPath(outDir)}");
return 0;

static string Keywords(Dictionary<string, string> listing) =>
    string.Join(",", listing.GetValueOrDefault("Keywords", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimStart('-', ' ').Trim())
        .Where(k => k.Length > 0));

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
