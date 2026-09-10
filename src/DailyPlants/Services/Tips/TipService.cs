using DailyPlants.Services.Settings;

namespace DailyPlants.Services.Tips;

public sealed class TipService : ITipService
{
    private readonly IAppPreferences _preferences;

    public TipService(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public bool ShouldShow(TipId tip) => !ReadSeen().Contains(tip.ToStorageId());

    public void MarkSeen(params TipId[] tips)
    {
        var seen = ReadSeen();

        foreach (var tip in tips)
        {
            var id = tip.ToStorageId();
            if (!seen.Contains(id))
            {
                seen.Add(id);
            }
        }

        _preferences.SeenTips = string.Join(',', seen);
    }

    public void Reset() => _preferences.SeenTips = string.Empty;

    /// <summary>
    /// Ids this build does not recognise are read back and written out again: after a
    /// downgrade, replaying a tip the newer build already showed would be worse than
    /// carrying a value we cannot interpret.
    /// </summary>
    private List<string> ReadSeen() =>
    [
        .. _preferences.SeenTips.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ];
}
