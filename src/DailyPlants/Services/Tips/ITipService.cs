namespace DailyPlants.Services.Tips;

/// <summary>
/// Tracks which teaching tips a user has already been shown.
/// </summary>
public interface ITipService
{
    /// <summary>Whether <paramref name="tip"/> has yet to be shown.</summary>
    bool ShouldShow(TipId tip);

    /// <summary>Records <paramref name="tips"/> as shown. Repeat calls are harmless.</summary>
    void MarkSeen(params TipId[] tips);

    /// <summary>Forgets every tip, so the whole flow runs again.</summary>
    void Reset();
}
