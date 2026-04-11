namespace DailyPlants.Services;

/// <summary>
/// Defines a merge relationship where a child item's servings are folded
/// into a parent item for UI display purposes.
/// When both parent and child are enabled, they appear as a single merged
/// item with combined recommended servings.
/// </summary>
/// <param name="ChildId">The item that gets hidden (merged into parent).</param>
/// <param name="ParentId">The item that stays visible with combined servings.</param>
public record MergeRule(string ChildId, string ParentId);

public static class MergeRules
{
    public static IReadOnlyList<MergeRule> All { get; } =
    [
        new("more_legumes", "beans"),
        new("more_berries", "berries"),
        new("more_greens", "greens"),
        new("stay_hydrated", "beverages"),
    ];

    /// <summary>
    /// Computes the active merge pairs among a set of enabled item IDs.
    /// A merge is active only when BOTH parent and child are in the enabled set.
    /// </summary>
    public static IReadOnlyList<MergeRule> GetActiveMerges(IReadOnlySet<string> enabledIds) =>
        All.Where(r => enabledIds.Contains(r.ChildId) && enabledIds.Contains(r.ParentId)).ToList();
}
