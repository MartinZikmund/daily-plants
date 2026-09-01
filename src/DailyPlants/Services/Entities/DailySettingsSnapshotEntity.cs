using SQLite;

namespace DailyPlants.Services.Entities;

/// <summary>
/// The checklist requirements that were in effect on a given date, captured when the
/// user first logged something that day. Completion for a past day is judged against
/// this snapshot so that changing checklists or disabling items later cannot rewrite
/// streaks and perfect days the user already earned (or hand them ones they did not).
/// </summary>
[Table("DailySettingsSnapshots")]
internal class DailySettingsSnapshotEntity
{
    [PrimaryKey]
    public string Date { get; set; } = "";

    /// <summary>
    /// Required servings as "itemId:servings" pairs joined by commas, e.g. "beans:3,berries:1".
    /// Stored resolved rather than as a settings copy so that changing an item's recommended
    /// servings in a later app version does not rewrite history either.
    /// </summary>
    public string RequiredItems { get; set; } = "";
}
