using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Provides the definitions for all checklist items across all checklists.
/// </summary>
public static class ChecklistDefinitions
{
    /// <summary>
    /// All checklist items with their definitions.
    /// Items that appear in multiple checklists are defined once with multiple ChecklistTypes.
    /// </summary>
    public static IReadOnlyList<ChecklistItem> AllItems { get; } = CreateAllItems();

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

    private static List<ChecklistItem> CreateAllItems() =>
    [
        // ===== BEANS / LEGUMES =====
        new ChecklistItem
        {
            Id = "beans",
            Name = "Beans",
            Description = "Legumes including beans, lentils, chickpeas, and hummus",
            RecommendedServings = 3,
            ServingSizeExample = "1/2 cup cooked beans, lentils, tofu, or tempeh; 1/4 cup hummus",
            HealthBenefits = "Excellent source of protein, fiber, and complex carbohydrates. Associated with longest lifespan gains according to Global Burden of Disease Study.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/beans/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== BERRIES =====
        new ChecklistItem
        {
            Id = "berries",
            Name = "Berries",
            Description = "All types of berries",
            RecommendedServings = 1,
            ServingSizeExample = "1/2 cup fresh or frozen; 1/4 cup dried",
            HealthBenefits = "Highest antioxidant content of all fruits. Associated with longer lifespan and cognitive benefits.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/berries/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== OTHER FRUITS =====
        new ChecklistItem
        {
            Id = "other_fruits",
            Name = "Other Fruits",
            Description = "Fruits other than berries",
            RecommendedServings = 3,
            ServingSizeExample = "1 medium fruit; 1/4 cup dried; 1/4 cup fruit juice",
            HealthBenefits = "Rich in vitamins, minerals, and fiber. Essential for overall health.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/fruit/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== GREENS =====
        new ChecklistItem
        {
            Id = "greens",
            Name = "Greens",
            Description = "Dark leafy green vegetables",
            RecommendedServings = 2,
            ServingSizeExample = "1 cup raw; 1/2 cup cooked",
            HealthBenefits = "Most associated with longer lifespan among vegetables. Rich in nitrates that improve muscle and artery function.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/greens/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== CRUCIFEROUS VEGETABLES =====
        new ChecklistItem
        {
            Id = "cruciferous",
            Name = "Cruciferous Vegetables",
            Description = "Broccoli, cauliflower, cabbage, kale, etc.",
            RecommendedServings = 1,
            ServingSizeExample = "1/2 cup chopped; 1/4 cup Brussels or broccoli sprouts; 1 tbsp horseradish",
            HealthBenefits = "Contains sulforaphane which boosts immune function and liver detox enzymes.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/cruciferous-vegetables/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== OTHER VEGETABLES =====
        new ChecklistItem
        {
            Id = "other_vegetables",
            Name = "Other Vegetables",
            Description = "Non-leafy, non-cruciferous vegetables",
            RecommendedServings = 2,
            ServingSizeExample = "1/2 cup raw or cooked non-leafy vegetables; 1/4 cup dried mushrooms",
            HealthBenefits = "Diverse nutrients and fiber. Essential for a balanced diet.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/vegetables/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== FLAXSEED =====
        new ChecklistItem
        {
            Id = "flaxseed",
            Name = "Flaxseed",
            Description = "Ground flaxseed",
            RecommendedServings = 1,
            ServingSizeExample = "1 tablespoon ground",
            HealthBenefits = "Richest source of lignans and omega-3 ALA. Benefits heart health and hormone balance.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/flax-seeds/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== NUTS AND SEEDS =====
        new ChecklistItem
        {
            Id = "nuts",
            Name = "Nuts and Seeds",
            Description = "All nuts and seeds (preferably walnuts)",
            RecommendedServings = 1,
            ServingSizeExample = "1/4 cup nuts or seeds; 2 tbsp nut or seed butter",
            HealthBenefits = "Associated with lowest risk of premature death of any food group. Walnuts are especially beneficial.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/nuts/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== HERBS AND SPICES =====
        new ChecklistItem
        {
            Id = "herbs_spices",
            Name = "Herbs and Spices",
            Description = "Especially turmeric",
            RecommendedServings = 1,
            ServingSizeExample = "1/4 tsp turmeric with other spices",
            HealthBenefits = "Powerful anti-inflammatory and antioxidant properties.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/spices/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== WHOLE GRAINS =====
        new ChecklistItem
        {
            Id = "whole_grains",
            Name = "Whole Grains",
            Description = "Intact whole grains preferred",
            RecommendedServings = 3,
            ServingSizeExample = "1/2 cup hot cereal or cooked grains; 1 slice bread; 1/2 bagel",
            HealthBenefits = "High fiber content supports gut health and stable blood sugar.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/whole-grains/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== BEVERAGES =====
        new ChecklistItem
        {
            Id = "beverages",
            Name = "Beverages",
            Description = "Water, tea, and other healthy drinks",
            RecommendedServings = 5,
            ServingSizeExample = "12 oz glass (5 glasses = 60 oz daily)",
            HealthBenefits = "Proper hydration is essential for all body functions.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/beverages/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== EXERCISE =====
        new ChecklistItem
        {
            Id = "exercise",
            Name = "Exercise",
            Description = "Daily physical activity",
            RecommendedServings = 1,
            ServingSizeExample = "90 min moderate activity or 40 min vigorous activity",
            HealthBenefits = "Boosts NAD+ levels by 127%. Essential for longevity and overall health.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/exercise/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen, ChecklistType.AntiAgingEight]
        },

        // ===== VITAMIN B12 =====
        new ChecklistItem
        {
            Id = "vitamin_b12",
            Name = "Vitamin B12",
            Description = "B12 supplementation",
            RecommendedServings = 1,
            ServingSizeExample = "2000 mcg weekly or 50 mcg daily",
            HealthBenefits = "Essential for nerve function and red blood cell formation. Required on plant-based diet.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/vitamin-b12/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.DailyDozen]
        },

        // ===== 21 TWEAKS - Weight Loss Accelerators =====

        new ChecklistItem
        {
            Id = "preload_water",
            Name = "Preload Water",
            Description = "Drink 2 cups of water before each meal",
            RecommendedServings = 3,
            ServingSizeExample = "2 cups (16 oz) cold water before each meal",
            HealthBenefits = "Boosts metabolism and helps feel full, reducing calorie intake.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/water/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "negative_calorie_preload",
            Name = "Negative Calorie Preload",
            Description = "Start each meal with apple, salad, or soup",
            RecommendedServings = 3,
            ServingSizeExample = "Apple, light soup, or salad before meals",
            HealthBenefits = "Reduces overall calorie consumption while adding nutrition.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/appetite/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "vinegar",
            Name = "Incorporate Vinegar",
            Description = "Add vinegar to meals",
            RecommendedServings = 2,
            ServingSizeExample = "2 tsp vinegar with each meal",
            HealthBenefits = "May help with blood sugar control and weight management.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/vinegar/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "undistracted_meals",
            Name = "Undistracted Meals",
            Description = "Eat without distractions",
            RecommendedServings = 1,
            ServingSizeExample = "At least one meal without TV, phone, or reading",
            HealthBenefits = "Mindful eating leads to better portion control and satisfaction.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/mindful-eating/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "twenty_minute_rule",
            Name = "20-Minute Rule",
            Description = "Take at least 20 minutes to eat",
            RecommendedServings = 1,
            ServingSizeExample = "Slow down eating, chew thoroughly",
            HealthBenefits = "Allows satiety signals to register, preventing overeating.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/eating-rate/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "fat_free_dressings",
            Name = "Fat-Free Dressings",
            Description = "Use fat-free seasonings",
            RecommendedServings = 1,
            ServingSizeExample = "Season with herbs, spices, vinegar instead of oil",
            HealthBenefits = "Reduces calorie density while maintaining flavor.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "front_load_calories",
            Name = "Front-Load Calories",
            Description = "Eat bigger breakfast, smaller dinner",
            RecommendedServings = 1,
            ServingSizeExample = "Make breakfast the largest meal of the day",
            HealthBenefits = "Aligns eating with circadian rhythm for better metabolism.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "time_restricted_eating",
            Name = "Time-Restricted Eating",
            Description = "Limit eating window",
            RecommendedServings = 1,
            ServingSizeExample = "Eat within a consistent daily window",
            HealthBenefits = "May improve metabolic health and weight management.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/intermittent-fasting/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_legumes",
            Name = "Eat More Legumes",
            Description = "Extra emphasis on beans and lentils",
            RecommendedServings = 1,
            ServingSizeExample = "Include legumes in multiple meals",
            HealthBenefits = "High protein and fiber promote satiety and gut health.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_greens",
            Name = "Eat More Greens",
            Description = "Extra emphasis on leafy greens",
            RecommendedServings = 1,
            ServingSizeExample = "Add greens to every meal",
            HealthBenefits = "Low calorie, high nutrient density supports weight loss.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "more_berries",
            Name = "Eat More Berries",
            Description = "Extra emphasis on berries",
            RecommendedServings = 1,
            ServingSizeExample = "Include berries daily",
            HealthBenefits = "Antioxidants and fiber support metabolic health.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "deflour_diet",
            Name = "Deflour Your Diet",
            Description = "Choose intact grains over flour products",
            RecommendedServings = 1,
            ServingSizeExample = "Steel-cut oats instead of bread, intact barley instead of pasta",
            HealthBenefits = "Intact grains have lower glycemic impact and better satiety.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "black_cumin",
            Name = "Black Cumin",
            Description = "Also known as Nigella sativa",
            RecommendedServings = 1,
            ServingSizeExample = "1/4 tsp black cumin (nigella seeds)",
            HealthBenefits = "May help with weight loss and blood sugar control.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/black-cumin/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "garlic_powder",
            Name = "Garlic Powder",
            Description = "Daily garlic supplementation",
            RecommendedServings = 1,
            ServingSizeExample = "1/4 tsp garlic powder",
            HealthBenefits = "Studies show it can reduce body fat.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/garlic/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "ground_ginger",
            Name = "Ground Ginger",
            Description = "Daily ginger",
            RecommendedServings = 1,
            ServingSizeExample = "1 tsp ground ginger or equivalent fresh",
            HealthBenefits = "May boost metabolism and reduce inflammation.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/ginger/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "nutritional_yeast",
            Name = "Nutritional Yeast",
            Description = "Beta-glucan fiber source",
            RecommendedServings = 1,
            ServingSizeExample = "2 tsp nutritional yeast",
            HealthBenefits = "Beta-glucan fiber can facilitate weight loss.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/nutritional-yeast/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "cumin",
            Name = "Cumin",
            Description = "Regular cumin spice",
            RecommendedServings = 2,
            ServingSizeExample = "1/2 tsp cumin with lunch and dinner",
            HealthBenefits = "May help with weight loss and improve cholesterol.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/cumin/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "green_tea",
            Name = "Green Tea",
            Description = "Daily green tea consumption",
            RecommendedServings = 3,
            ServingSizeExample = "3 cups green tea",
            HealthBenefits = "Contains catechins that may boost metabolism.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/green-tea/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "stay_hydrated",
            Name = "Stay Hydrated",
            Description = "Maintain proper hydration",
            RecommendedServings = 1,
            ServingSizeExample = "8+ cups of water throughout the day",
            HealthBenefits = "Proper hydration supports metabolism and reduces hunger.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "exercise_timing",
            Name = "Exercise Timing",
            Description = "Optimal timing for exercise",
            RecommendedServings = 1,
            ServingSizeExample = "Exercise fasted in morning or in afternoon",
            HealthBenefits = "May enhance fat burning and metabolic benefits.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks]
        },

        new ChecklistItem
        {
            Id = "enough_sleep",
            Name = "Get Enough Sleep",
            Description = "Quality sleep for weight management",
            RecommendedServings = 1,
            ServingSizeExample = "7+ hours of quality sleep",
            HealthBenefits = "Poor sleep disrupts hunger hormones and metabolism.",
            MoreInfoUrl = "https://nutritionfacts.org/topics/sleep/",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.TwentyOneTweaks, ChecklistType.AntiAgingEight]
        },

        // ===== ANTI-AGING EIGHT - Additional items =====

        new ChecklistItem
        {
            Id = "sun_protection",
            Name = "Sun Protection",
            Description = "Daily sun protection",
            RecommendedServings = 1,
            ServingSizeExample = "Sunscreen, hat, or protective clothing when outdoors",
            HealthBenefits = "Sun exposure accounts for 90% of visible skin aging.",
            IconGlyph = "\uE700",
            Checklists = [ChecklistType.AntiAgingEight]
        }
    ];
}
