using System.Globalization;

namespace DailyPlants.Services;

/// <summary>
/// ISO-8601 date conversion that ignores <see cref="CultureInfo.CurrentCulture"/>.
/// The app switches the current culture at runtime and ships locales whose default
/// calendar is not Gregorian (fa), so stored and exported dates must never use it —
/// otherwise entries written in one language become unreadable in another.
/// </summary>
internal static class IsoDate
{
    public const string Format = "yyyy-MM-dd";

    public static string ToStorage(DateOnly date) => date.ToString(Format, CultureInfo.InvariantCulture);

    public static DateOnly Parse(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string TimestampToStorage(DateTime timestamp) =>
        timestamp.ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
