using SQLite;

namespace DailyPlants.Services.Entities;

[Table("UserSettings")]
internal class UserSettingsEntity
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public string SettingsJson { get; set; } = "";
}
