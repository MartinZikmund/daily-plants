using SQLite;

namespace DailyPlants.Services.Entities;

[Table("CustomItems")]
internal class CustomItemEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int RecommendedServings { get; set; }

    public int IconType { get; set; } = 0;

    public string IconValue { get; set; } = "default";

    public int SortOrder { get; set; }

    public string UpdatedAt { get; set; } = "";
}
