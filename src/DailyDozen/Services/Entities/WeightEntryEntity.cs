using SQLite;

namespace DailyDozen.Services.Entities;

[Table("WeightEntries")]
internal class WeightEntryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Date { get; set; } = "";

    public double Weight { get; set; }

    public string? Notes { get; set; }
}
