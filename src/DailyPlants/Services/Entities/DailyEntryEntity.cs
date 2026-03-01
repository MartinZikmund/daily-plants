using SQLite;

namespace DailyPlants.Services.Entities;

[Table("DailyEntries")]
internal class DailyEntryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Date { get; set; } = "";

    public string ItemId { get; set; } = "";

    public int ServingsCompleted { get; set; }
}
