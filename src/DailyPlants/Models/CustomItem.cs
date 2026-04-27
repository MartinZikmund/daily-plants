namespace DailyPlants.Models;

/// <summary>
/// User-defined daily-tracked item. Lives in its own table; never participates
/// in achievement / streak / perfect-day calculations.
/// </summary>
public partial record CustomItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = "";

    public required int RecommendedServings { get; init; }

    public required CustomItemIconType IconType { get; init; }

    public required string IconValue { get; init; }

    public required int SortOrder { get; init; }

    public required DateTime UpdatedAt { get; init; }
}

public enum CustomItemIconType
{
    Catalog = 0,
    Emoji = 1,
}
