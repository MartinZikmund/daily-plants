namespace DailyPlants.Models;

/// <summary>
/// Represents a single item in a checklist (e.g., "Beans", "Berries").
/// This is the definition/template, not the user's daily entry.
/// </summary>
public partial record ChecklistItem
{
    /// <summary>
    /// Unique identifier for this item (e.g., "beans", "berries").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the item (e.g., "Beans", "Berries").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Short description of the item.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Number of servings recommended per day.
    /// </summary>
    public required int RecommendedServings { get; init; }

    /// <summary>
    /// Example of what counts as one serving.
    /// </summary>
    public required string ServingSizeExample { get; init; }

    /// <summary>
    /// Detailed health benefits information.
    /// </summary>
    public string? HealthBenefits { get; init; }

    /// <summary>
    /// URL to NutritionFacts.org for more information.
    /// </summary>
    public string? MoreInfoUrl { get; init; }

    /// <summary>
    /// Path to the icon asset for this item.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Which checklists this item belongs to.
    /// </summary>
    public required ChecklistType[] Checklists { get; init; }
}
