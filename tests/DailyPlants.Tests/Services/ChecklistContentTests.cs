using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Pins the wording that matters against NutritionFacts.org and the official Daily Dozen apps,
/// in every language: a wrong number here is health advice, not a typo.
/// </summary>
[TestClass]
public partial class ChecklistContentTests
{
    private const string Topics = "https://nutritionfacts.org/topics/";

    private static readonly string[] CautionKeys =
    [
        "DD_VitaminB12_Caution", "TT_PreloadWater_Caution", "TT_GreenTea_Caution", "TT_Vinegar_Caution",
        "TT_Hydrated_Caution", "TT_ExerciseTiming_Caution", "TT_Trendelenburg_Caution"
    ];

    private static string StringsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DailyPlants.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");
        return Path.Combine(directory!.FullName, "src", "DailyPlants", "Strings");
    }

    private static Dictionary<string, string> Load(string language) =>
        XDocument.Load(Path.Combine(StringsDirectory(), language, "Resources.resw")).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

    private static IEnumerable<string> Languages() =>
        Directory.GetDirectories(StringsDirectory()).Select(path => Path.GetFileName(path)!);

    private static List<string> Numbers(string text)
    {
        var western = new string(text.Select(c => char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c).ToArray());
        return NumberPattern().Matches(western).Select(m => m.Value).Order().ToList();
    }

    [GeneratedRegex(@"\d+(?:/\d+)?")]
    private static partial Regex NumberPattern();

    [TestMethod]
    public void EveryLanguage_HasExactlyTheEnglishKeys()
    {
        var english = Load("en").Keys.ToHashSet();

        foreach (var language in Languages())
        {
            Load(language).Keys.Should().BeEquivalentTo(english, $"{language} must not fall back or carry dead keys");
        }
    }

    [TestMethod]
    public void EveryLanguage_TranslatesEachCautionWithTheSameNumbers()
    {
        var english = Load("en");

        foreach (var language in Languages().Where(l => l != "en"))
        {
            var strings = Load(language);
            foreach (var key in CautionKeys)
            {
                strings[key].Should().NotBe(english[key], $"{language} {key} is safety text");
                Numbers(strings[key]).Should().Equal(Numbers(english[key]), $"{language} {key}");
            }
        }
    }

    [TestMethod]
    public void EveryLanguage_MetricServingsMatchEnglishNumbers()
    {
        var english = Load("en");
        var keys = english.Keys.Where(k => k.StartsWith("DD_", StringComparison.Ordinal) && k.EndsWith("_Metric", StringComparison.Ordinal)).ToList();

        foreach (var language in Languages().Where(l => l != "en"))
        {
            var strings = Load(language);
            foreach (var key in keys)
            {
                Numbers(strings[key]).Should().Equal(Numbers(english[key]), $"{language} {key}");
            }
        }
    }

    [TestMethod]
    public void NoLanguage_UsesMetricUnitsInImperialServings()
    {
        foreach (var language in Languages())
        {
            var strings = Load(language);
            foreach (var (key, value) in strings.Where(s => s.Key.StartsWith("DD_", StringComparison.Ordinal) && s.Key.EndsWith("_Imperial", StringComparison.Ordinal)))
            {
                value.Should().NotMatchRegex(@"\d\s*(g|ml|г|мл)(\s|$)", $"{language} {key}");
            }
        }
    }

    [TestMethod]
    [DataRow("DD_VitaminB12_Serving", "At least")]
    [DataRow("DD_VitaminB12_Desc", "yanocobalamin")]
    [DataRow("DD_VitaminB12_Caution", "1000 mcg")]
    [DataRow("DD_Cruciferous_Serving_Metric", "90 g")]
    [DataRow("TT_Ginger_Desc", "cayenne")]
    [DataRow("TT_Vinegar_Caution", "Never drink")]
    [DataRow("TT_Hydrated_Desc", "pale yellow")]
    [DataRow("TT_Hydrated_Caution", "heart or kidney")]
    [DataRow("TT_PreloadWater_Caution", "3 cups")]
    [DataRow("TT_GreenTea_LongDesc", "between meals")]
    [DataRow("TT_ExerciseTiming_Caution", "diabetes")]
    [DataRow("TT_Trendelenburg_Caution", "glaucoma")]
    [DataRow("TT_TimeRestricted_Desc", "12 hours")]
    public void English_CarriesTheOfficialGuidance(string key, string expected) =>
        Load("en")[key].Should().Contain(expected);

    [TestMethod]
    [DataRow("TT_ExerciseTiming_Desc", "afternoon")]
    [DataRow("TT_Garlic_LongDesc", "supplement")]
    [DataRow("DD_Exercise_Benefits", "Boosts NAD+ levels")]
    [DataRow("TT_Intentions_Benefits", "doubles the likelihood")]
    [DataRow("Settings_DailyDozenDesc", "food groups")]
    [DataRow("About_Description", "How Not to Age")]
    public void English_DropsWhatTheSourcesDoNotSay(string key, string dropped) =>
        Load("en")[key].Should().NotContain(dropped);

    [TestMethod]
    public void Cautions_AreOnTheItemsThatNeedThem()
    {
        var withCaution = ChecklistDefinitions.AllItems.Where(i => i.Caution is not null).Select(i => i.Id).ToList();

        withCaution.Should().BeEquivalentTo(
            "vitamin_b12", "preload_water", "green_tea", "vinegar", "stay_hydrated", "exercise_timing", "nightly_trendelenburg");
    }

    /// <summary>Checked by hand: a dead link only shows up once someone taps it.</summary>
    [TestMethod]
    public void TweakLinks_AreTheCheckedOnes()
    {
        Dictionary<string, string?> expected = new()
        {
            ["preload_water"] = Topics + "water/",
            ["negative_calorie_preload"] = Topics + "weight-loss/",
            ["vinegar"] = Topics + "vinegar/",
            ["undistracted_meals"] = Topics + "mindfulness/",
            ["twenty_minute_rule"] = Topics + "weight-loss/",
            ["front_load_calories"] = Topics + "breakfast/",
            ["time_restricted_eating"] = Topics + "fasting/",
            ["deflour_diet"] = Topics + "flour/",
            ["black_cumin"] = Topics + "black-cumin/",
            ["garlic_powder"] = Topics + "garlic/",
            ["ground_ginger"] = Topics + "ginger/",
            ["nutritional_yeast"] = Topics + "nutritional-yeast/",
            ["cumin"] = Topics + "cumin/",
            ["green_tea"] = Topics + "green-tea/",
            ["stay_hydrated"] = Topics + "water/",
            ["exercise_timing"] = Topics + "exercise/",
            ["enough_sleep"] = Topics + "sleep/",
            ["weigh_twice"] = Topics + "weight-loss/",
            ["complete_intentions"] = "https://nutritionfacts.org/video/how-to-form-good-habits/",
            ["nightly_fast"] = Topics + "chronobiology/",
            ["nightly_trendelenburg"] = "https://nutritionfacts.org/video/the-risks-and-benefits-of-mild-trendelenburg-position-to-reduce-body-fat/",
        };

        var actual = ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks)
            .ToDictionary(i => i.Id, i => i.MoreInfoUrl);

        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void ServingCounts_MatchTheOfficialApps()
    {
        Dictionary<string, int> expected = new()
        {
            ["beans"] = 3,
            ["berries"] = 1,
            ["other_fruits"] = 3,
            ["greens"] = 2,
            ["cruciferous"] = 1,
            ["other_vegetables"] = 2,
            ["flaxseed"] = 1,
            ["nuts"] = 1,
            ["herbs_spices"] = 1,
            ["whole_grains"] = 3,
            ["beverages"] = 5,
            ["exercise"] = 1,
            ["vitamin_b12"] = 1,
            ["preload_water"] = 3,
            ["negative_calorie_preload"] = 3,
            ["vinegar"] = 3,
            ["undistracted_meals"] = 3,
            ["twenty_minute_rule"] = 3,
            ["black_cumin"] = 1,
            ["garlic_powder"] = 1,
            ["ground_ginger"] = 1,
            ["nutritional_yeast"] = 1,
            ["cumin"] = 2,
            ["green_tea"] = 3,
            ["stay_hydrated"] = 1,
            ["deflour_diet"] = 1,
            ["front_load_calories"] = 1,
            ["time_restricted_eating"] = 1,
            ["exercise_timing"] = 1,
            ["weigh_twice"] = 2,
            ["complete_intentions"] = 3,
            ["nightly_fast"] = 1,
            ["enough_sleep"] = 1,
            ["nightly_trendelenburg"] = 1,
        };

        ChecklistDefinitions.AllItems.ToDictionary(i => i.Id, i => i.RecommendedServings).Should().BeEquivalentTo(expected);
    }
}
