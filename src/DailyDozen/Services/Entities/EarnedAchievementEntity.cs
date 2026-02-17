using SQLite;

namespace DailyDozen.Services.Entities;

[Table("EarnedAchievements")]
internal class EarnedAchievementEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string AchievementId { get; set; } = "";

    public string EarnedAt { get; set; } = "";

    public int HasBeenSeen { get; set; }
}
