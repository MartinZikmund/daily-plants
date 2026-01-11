namespace DailyDozen.Models;

/// <summary>
/// Represents a weight measurement entry.
/// </summary>
public class WeightEntry
{
    /// <summary>
    /// Database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The date of this weight entry.
    /// </summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// Weight value in the user's preferred unit.
    /// </summary>
    public required double Weight { get; set; }

    /// <summary>
    /// Optional notes for this entry.
    /// </summary>
    public string? Notes { get; set; }
}
