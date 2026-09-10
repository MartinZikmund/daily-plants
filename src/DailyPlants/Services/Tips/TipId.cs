namespace DailyPlants.Services.Tips;

/// <summary>
/// The teaching tips the app can show.
/// </summary>
public enum TipId
{
    /// <summary>Tour step 1: tapping a row logs a serving.</summary>
    DiaryLogServing,

    /// <summary>Tour step 2: the tally across the top is the day's progress.</summary>
    DiaryDayProgress,

    /// <summary>Contextual: the date header moves, so a missed day can still be filled in.</summary>
    DiaryPastDays,
}

public static class TipIdExtensions
{
    /// <summary>
    /// The id written to storage. Enum ordinals are never persisted, so inserting a member
    /// cannot shuffle which tips a user has already been shown.
    /// </summary>
    public static string ToStorageId(this TipId tip) => tip switch
    {
        TipId.DiaryLogServing => "diary-log-serving",
        TipId.DiaryDayProgress => "diary-day-progress",
        TipId.DiaryPastDays => "diary-past-days",
        _ => throw new ArgumentOutOfRangeException(nameof(tip), tip, "Unknown tip"),
    };

    /// <summary>The tour steps, in the order they are shown.</summary>
    public static IReadOnlyList<TipId> TourSteps { get; } =
        [TipId.DiaryLogServing, TipId.DiaryDayProgress];
}
