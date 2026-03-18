using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services.Settings;

namespace DailyPlants.Services;

/// <summary>
/// Provides the definitions for all checklist items across all checklists.
/// </summary>
public static class ChecklistDefinitions
{
    private static IReadOnlyList<ChecklistItem>? _allItems;

    /// <summary>
    /// All checklist items with their definitions.
    /// Items that appear in multiple checklists are defined once with multiple ChecklistTypes.
    /// Lazily initialized so resource strings are resolved after localization is set up.
    /// </summary>
    public static IReadOnlyList<ChecklistItem> AllItems => _allItems ??= CreateAllItems();

    /// <summary>
    /// Gets items for a specific checklist type.
    /// </summary>
    public static IEnumerable<ChecklistItem> GetItemsForChecklist(ChecklistType checklist) =>
        AllItems.Where(item => item.Checklists.Contains(checklist));

    /// <summary>
    /// Gets an item by its ID.
    /// </summary>
    public static ChecklistItem? GetItemById(string id) =>
        AllItems.FirstOrDefault(item => item.Id == id);

    /// <summary>
    /// Gets the deduplicated list of enabled checklist items based on user preferences.
    /// </summary>
    public static List<ChecklistItem> GetEnabledItems(IAppPreferences preferences)
    {
        var items = new List<ChecklistItem>();

        if (preferences.DailyDozenEnabled)
        {
            items.AddRange(GetItemsForChecklist(ChecklistType.DailyDozen));
        }

        if (preferences.TwentyOneTweaksEnabled)
        {
            items.AddRange(GetItemsForChecklist(ChecklistType.TwentyOneTweaks));
        }

        if (preferences.AntiAgingEightEnabled)
        {
            items.AddRange(GetItemsForChecklist(ChecklistType.AntiAgingEight));
        }

        return items.GroupBy(i => i.Id).Select(g => g.First()).ToList();
    }

    /// <summary>
    /// Gets the deduplicated list of enabled checklist item IDs.
    /// </summary>
    public static List<string> GetEnabledItemIds(IAppPreferences preferences) =>
        GetEnabledItems(preferences).Select(i => i.Id).ToList();

    /// <summary>
    /// Gets a map of enabled item IDs to their required servings.
    /// </summary>
    public static Dictionary<string, int> GetRequiredServingsMap(IAppPreferences preferences) =>
        GetEnabledItems(preferences).ToDictionary(i => i.Id, i => i.RecommendedServings);

    private static List<ChecklistItem> CreateAllItems() =>
    [
        // ===== BEANS / LEGUMES =====
        new ChecklistItem
        {
            Id = "beans",
            Name = Localizer.GetString("DD_Beans"),
            Description = Localizer.GetString("DD_Beans_Desc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("DD_Beans_Serving"),
            HealthBenefits = Localizer.GetString("DD_Beans_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/beans/",
            IconPath = "ms-appx:///Assets/Icons/Items/beans.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== BERRIES =====
        new ChecklistItem
        {
            Id = "berries",
            Name = Localizer.GetString("DD_Berries"),
            Description = Localizer.GetString("DD_Berries_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Berries_Serving"),
            HealthBenefits = Localizer.GetString("DD_Berries_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/berries/",
            IconPath = "ms-appx:///Assets/Icons/Items/berries.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== OTHER FRUITS =====
        new ChecklistItem
        {
            Id = "other_fruits",
            Name = Localizer.GetString("DD_OtherFruits"),
            Description = Localizer.GetString("DD_OtherFruits_Desc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("DD_OtherFruits_Serving"),
            HealthBenefits = Localizer.GetString("DD_OtherFruits_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/fruit/",
            IconPath = "ms-appx:///Assets/Icons/Items/other_fruits.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== GREENS =====
        new ChecklistItem
        {
            Id = "greens",
            Name = Localizer.GetString("DD_Greens"),
            Description = Localizer.GetString("DD_Greens_Desc"),
            RecommendedServings = 2,
            ServingSizeExample = Localizer.GetString("DD_Greens_Serving"),
            HealthBenefits = Localizer.GetString("DD_Greens_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/greens/",
            IconPath = "ms-appx:///Assets/Icons/Items/greens.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== CRUCIFEROUS VEGETABLES =====
        new ChecklistItem
        {
            Id = "cruciferous",
            Name = Localizer.GetString("DD_Cruciferous"),
            Description = Localizer.GetString("DD_Cruciferous_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Cruciferous_Serving"),
            HealthBenefits = Localizer.GetString("DD_Cruciferous_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/cruciferous-vegetables/",
            IconPath = "ms-appx:///Assets/Icons/Items/cruciferous.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== OTHER VEGETABLES =====
        new ChecklistItem
        {
            Id = "other_vegetables",
            Name = Localizer.GetString("DD_OtherVegetables"),
            Description = Localizer.GetString("DD_OtherVegetables_Desc"),
            RecommendedServings = 2,
            ServingSizeExample = Localizer.GetString("DD_OtherVegetables_Serving"),
            HealthBenefits = Localizer.GetString("DD_OtherVegetables_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/vegetables/",
            IconPath = "ms-appx:///Assets/Icons/Items/other_vegetables.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== FLAXSEED =====
        new ChecklistItem
        {
            Id = "flaxseed",
            Name = Localizer.GetString("DD_Flaxseed"),
            Description = Localizer.GetString("DD_Flaxseed_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Flaxseed_Serving"),
            HealthBenefits = Localizer.GetString("DD_Flaxseed_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/flax-seeds/",
            IconPath = "ms-appx:///Assets/Icons/Items/flaxseed.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== NUTS AND SEEDS =====
        new ChecklistItem
        {
            Id = "nuts",
            Name = Localizer.GetString("DD_Nuts"),
            Description = Localizer.GetString("DD_Nuts_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Nuts_Serving"),
            HealthBenefits = Localizer.GetString("DD_Nuts_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/nuts/",
            IconPath = "ms-appx:///Assets/Icons/Items/nuts.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== HERBS AND SPICES =====
        new ChecklistItem
        {
            Id = "herbs_spices",
            Name = Localizer.GetString("DD_Herbs"),
            Description = Localizer.GetString("DD_Herbs_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Herbs_Serving"),
            HealthBenefits = Localizer.GetString("DD_Herbs_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/spices/",
            IconPath = "ms-appx:///Assets/Icons/Items/herbs_spices.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== WHOLE GRAINS =====
        new ChecklistItem
        {
            Id = "whole_grains",
            Name = Localizer.GetString("DD_WholeGrains"),
            Description = Localizer.GetString("DD_WholeGrains_Desc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("DD_WholeGrains_Serving"),
            HealthBenefits = Localizer.GetString("DD_WholeGrains_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/whole-grains/",
            IconPath = "ms-appx:///Assets/Icons/Items/whole_grains.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== BEVERAGES =====
        new ChecklistItem
        {
            Id = "beverages",
            Name = Localizer.GetString("DD_Beverages"),
            Description = Localizer.GetString("DD_Beverages_Desc"),
            RecommendedServings = 5,
            ServingSizeExample = Localizer.GetString("DD_Beverages_Serving"),
            HealthBenefits = Localizer.GetString("DD_Beverages_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/beverages/",
            IconPath = "ms-appx:///Assets/Icons/Items/beverages.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== EXERCISE =====
        new ChecklistItem
        {
            Id = "exercise",
            Name = Localizer.GetString("DD_Exercise"),
            Description = Localizer.GetString("DD_Exercise_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_Exercise_Serving"),
            HealthBenefits = Localizer.GetString("DD_Exercise_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/exercise/",
            IconPath = "ms-appx:///Assets/Icons/Items/exercise.png",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== VITAMIN B12 =====
        new ChecklistItem
        {
            Id = "vitamin_b12",
            Name = Localizer.GetString("DD_VitaminB12"),
            Description = Localizer.GetString("DD_VitaminB12_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("DD_VitaminB12_Serving"),
            HealthBenefits = Localizer.GetString("DD_VitaminB12_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/vitamin-b12/",
            IconPath = "ms-appx:///Assets/Icons/Items/vitamin_b12.png",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== 21 TWEAKS - Weight Loss Accelerators =====

        new ChecklistItem
        {
            Id = "preload_water",
            Name = Localizer.GetString("TT_PreloadWater"),
            Description = Localizer.GetString("TT_PreloadWater_LongDesc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("TT_PreloadWater_Desc"),
            HealthBenefits = Localizer.GetString("TT_PreloadWater_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/water/",
            IconPath = "ms-appx:///Assets/Icons/Items/preload_water.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "negative_calorie_preload",
            Name = Localizer.GetString("TT_NegativeCalorie"),
            Description = Localizer.GetString("TT_NegativeCalorie_LongDesc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("TT_NegativeCalorie_Desc"),
            HealthBenefits = Localizer.GetString("TT_NegativeCalorie_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/appetite/",
            IconPath = "ms-appx:///Assets/Icons/Items/negative_calorie_preload.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "vinegar",
            Name = Localizer.GetString("TT_Vinegar"),
            Description = Localizer.GetString("TT_Vinegar_LongDesc"),
            RecommendedServings = 2,
            ServingSizeExample = Localizer.GetString("TT_Vinegar_Desc"),
            HealthBenefits = Localizer.GetString("TT_Vinegar_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/vinegar/",
            IconPath = "ms-appx:///Assets/Icons/Items/vinegar.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "undistracted_meals",
            Name = Localizer.GetString("TT_Undistracted"),
            Description = Localizer.GetString("TT_Undistracted_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Undistracted_Desc"),
            HealthBenefits = Localizer.GetString("TT_Undistracted_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/mindful-eating/",
            IconPath = "ms-appx:///Assets/Icons/Items/undistracted_meals.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "twenty_minute_rule",
            Name = Localizer.GetString("TT_TwentyMinute"),
            Description = Localizer.GetString("TT_TwentyMinute_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_TwentyMinute_Desc"),
            HealthBenefits = Localizer.GetString("TT_TwentyMinute_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/eating-rate/",
            IconPath = "ms-appx:///Assets/Icons/Items/twenty_minute_rule.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "fat_free_dressings",
            Name = Localizer.GetString("TT_FatFree"),
            Description = Localizer.GetString("TT_FatFree_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_FatFree_Desc"),
            HealthBenefits = Localizer.GetString("TT_FatFree_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/fat_free_dressings.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "front_load_calories",
            Name = Localizer.GetString("TT_FrontLoad"),
            Description = Localizer.GetString("TT_FrontLoad_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_FrontLoad_Desc"),
            HealthBenefits = Localizer.GetString("TT_FrontLoad_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/front_load_calories.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "time_restricted_eating",
            Name = Localizer.GetString("TT_TimeRestricted"),
            Description = Localizer.GetString("TT_TimeRestricted_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_TimeRestricted_Desc"),
            HealthBenefits = Localizer.GetString("TT_TimeRestricted_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/intermittent-fasting/",
            IconPath = "ms-appx:///Assets/Icons/Items/time_restricted_eating.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_legumes",
            Name = Localizer.GetString("TT_Legumes"),
            Description = Localizer.GetString("TT_Legumes_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Legumes_Desc"),
            HealthBenefits = Localizer.GetString("TT_Legumes_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/more_legumes.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_greens",
            Name = Localizer.GetString("TT_Greens"),
            Description = Localizer.GetString("TT_Greens_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Greens_Desc"),
            HealthBenefits = Localizer.GetString("TT_Greens_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/more_greens.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_berries",
            Name = Localizer.GetString("TT_Berries"),
            Description = Localizer.GetString("TT_Berries_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Berries_Desc"),
            HealthBenefits = Localizer.GetString("TT_Berries_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/more_berries.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "deflour_diet",
            Name = Localizer.GetString("TT_Deflour"),
            Description = Localizer.GetString("TT_Deflour_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Deflour_Desc"),
            HealthBenefits = Localizer.GetString("TT_Deflour_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/deflour_diet.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "black_cumin",
            Name = Localizer.GetString("TT_BlackCumin"),
            Description = Localizer.GetString("TT_BlackCumin_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_BlackCumin_Desc"),
            HealthBenefits = Localizer.GetString("TT_BlackCumin_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/black-cumin/",
            IconPath = "ms-appx:///Assets/Icons/Items/black_cumin.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "garlic_powder",
            Name = Localizer.GetString("TT_Garlic"),
            Description = Localizer.GetString("TT_Garlic_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Garlic_Desc"),
            HealthBenefits = Localizer.GetString("TT_Garlic_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/garlic/",
            IconPath = "ms-appx:///Assets/Icons/Items/garlic_powder.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "ground_ginger",
            Name = Localizer.GetString("TT_Ginger"),
            Description = Localizer.GetString("TT_Ginger_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Ginger_Desc"),
            HealthBenefits = Localizer.GetString("TT_Ginger_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/ginger/",
            IconPath = "ms-appx:///Assets/Icons/Items/ground_ginger.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "nutritional_yeast",
            Name = Localizer.GetString("TT_NutritionalYeast"),
            Description = Localizer.GetString("TT_NutritionalYeast_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_NutritionalYeast_Desc"),
            HealthBenefits = Localizer.GetString("TT_NutritionalYeast_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/nutritional-yeast/",
            IconPath = "ms-appx:///Assets/Icons/Items/nutritional_yeast.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "cumin",
            Name = Localizer.GetString("TT_Cumin"),
            Description = Localizer.GetString("TT_Cumin_LongDesc"),
            RecommendedServings = 2,
            ServingSizeExample = Localizer.GetString("TT_Cumin_Desc"),
            HealthBenefits = Localizer.GetString("TT_Cumin_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/cumin/",
            IconPath = "ms-appx:///Assets/Icons/Items/cumin.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "green_tea",
            Name = Localizer.GetString("TT_GreenTea"),
            Description = Localizer.GetString("TT_GreenTea_LongDesc"),
            RecommendedServings = 3,
            ServingSizeExample = Localizer.GetString("TT_GreenTea_Desc"),
            HealthBenefits = Localizer.GetString("TT_GreenTea_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/green-tea/",
            IconPath = "ms-appx:///Assets/Icons/Items/green_tea.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "stay_hydrated",
            Name = Localizer.GetString("TT_Hydrated"),
            Description = Localizer.GetString("TT_Hydrated_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Hydrated_Desc"),
            HealthBenefits = Localizer.GetString("TT_Hydrated_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/stay_hydrated.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "exercise_timing",
            Name = Localizer.GetString("TT_ExerciseTiming"),
            Description = Localizer.GetString("TT_ExerciseTiming_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_ExerciseTiming_Desc"),
            HealthBenefits = Localizer.GetString("TT_ExerciseTiming_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/exercise_timing.png",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "enough_sleep",
            Name = Localizer.GetString("TT_Sleep"),
            Description = Localizer.GetString("TT_Sleep_LongDesc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("TT_Sleep_Desc"),
            HealthBenefits = Localizer.GetString("TT_Sleep_Benefits"),
            MoreInfoUrl = "https://nutritionfacts.org/topics/sleep/",
            IconPath = "ms-appx:///Assets/Icons/Items/enough_sleep.png",
            Checklists = [ChecklistType.TwentyOneTweaks, ChecklistType.AntiAgingEight]
        },

        // ===== ANTI-AGING EIGHT - Additional items =====

        new ChecklistItem
        {
            Id = "sun_protection",
            Name = Localizer.GetString("AA_SunProtection"),
            Description = Localizer.GetString("AA_SunProtection_Desc"),
            RecommendedServings = 1,
            ServingSizeExample = Localizer.GetString("AA_SunProtection_Serving"),
            HealthBenefits = Localizer.GetString("AA_SunProtection_Benefits"),
            IconPath = "ms-appx:///Assets/Icons/Items/sun_protection.png",
            Checklists = [ChecklistType.AntiAgingEight]
        }
    ];
}
