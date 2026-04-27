using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Service for managing user-defined CustomItem definitions. Wraps IDataService
/// with validation, ID generation, and SortOrder defaults.
/// </summary>
public interface ICustomItemService
{
    /// <summary>
    /// Gets all custom items, ordered by SortOrder.
    /// </summary>
    Task<IReadOnlyList<CustomItem>> GetAllAsync();

    /// <summary>
    /// Creates a new custom item. Generates a fresh GUID Id and assigns
    /// SortOrder = (max existing) + 100.
    /// </summary>
    Task<CustomItem> CreateAsync(string name, string description, int recommendedServings, CustomItemIconType iconType, string iconValue);

    /// <summary>
    /// Updates an existing custom item. Id and SortOrder are preserved unless
    /// the caller passes a different SortOrder.
    /// </summary>
    Task UpdateAsync(CustomItem item);

    /// <summary>
    /// Deletes a custom item. When cascadeEntries is true, also removes all
    /// CustomItemEntries rows for the item; otherwise the entries are kept as
    /// orphans (FR-004 keep-history).
    /// </summary>
    Task DeleteAsync(string id, bool cascadeEntries);
}
