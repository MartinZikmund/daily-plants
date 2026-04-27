namespace DailyPlants.Models;

/// <summary>
/// A user's progress on a CustomItem for a single calendar day. Mirrors DailyEntry
/// but lives in its own table so achievement queries never see it.
/// </summary>
public class CustomItemEntry
{
    public int Id { get; set; }

    public required DateOnly Date { get; set; }

    public required string CustomItemId { get; set; }

    public int ServingsCompleted { get; set; }

    public required DateTime UpdatedAt { get; set; }
}
