namespace DailyPlants.Services.Settings;

/// <summary>
/// Extension methods for per-item enable/disable in preferences.
/// </summary>
public static class AppPreferencesExtensions
{
    public static HashSet<string> GetDisabledItemIdSet(this IAppPreferences prefs)
    {
        var raw = prefs.DisabledItemIds;
        if (string.IsNullOrEmpty(raw)) return [];
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    public static void SetItemDisabled(this IAppPreferences prefs, string itemId, bool disabled)
    {
        var set = prefs.GetDisabledItemIdSet();
        if (disabled) set.Add(itemId);
        else set.Remove(itemId);

        // Prune any stale IDs (items removed in future app versions)
        var validIds = ChecklistDefinitions.AllItems.Select(i => i.Id).ToHashSet();
        set.IntersectWith(validIds);

        prefs.DisabledItemIds = string.Join(',', set);
    }

    public static bool IsItemDisabled(this IAppPreferences prefs, string itemId)
        => prefs.GetDisabledItemIdSet().Contains(itemId);
}
