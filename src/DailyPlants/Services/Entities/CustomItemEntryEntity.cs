using SQLite;

namespace DailyPlants.Services.Entities;

[Table("CustomItemEntries")]
internal class CustomItemEntryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Date { get; set; } = "";

    public string CustomItemId { get; set; } = "";

    public int ServingsCompleted { get; set; }

    public string UpdatedAt { get; set; } = "";
}
