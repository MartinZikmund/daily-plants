namespace DailyPlants.Models;

/// <summary>
/// Represents a user's entry for a specific item on a specific date.
/// </summary>
public class DailyEntry
{
    /// <summary>
    /// Database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date of this entry (stored as yyyy-MM-dd string in SQLite).
    /// </summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// The checklist item ID this entry is for.
    /// </summary>
    public required string ItemId { get; set; }

    /// <summary>
    /// Number of servings completed for this item on this date.
    /// </summary>
    public int ServingsCompleted { get; set; }
}
