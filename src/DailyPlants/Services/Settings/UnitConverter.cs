namespace DailyPlants.Services.Settings;

/// <summary>
/// Converts between the units the user types and the units the app stores.
/// Weight is always persisted in kilograms and height in centimetres, so that
/// switching between metric and imperial re-displays history rather than
/// reinterpreting it.
/// </summary>
public static class UnitConverter
{
    public const double PoundsPerKilogram = 2.20462262185;
    public const double CentimetresPerInch = 2.54;

    public static double KilogramsToDisplay(double kilograms, bool useMetric) =>
        useMetric ? kilograms : kilograms * PoundsPerKilogram;

    public static double DisplayToKilograms(double value, bool useMetric) =>
        useMetric ? value : value / PoundsPerKilogram;

    public static double CentimetresToDisplay(double centimetres, bool useMetric) =>
        useMetric ? centimetres : centimetres / CentimetresPerInch;

    public static double DisplayToCentimetres(double value, bool useMetric) =>
        useMetric ? value : value * CentimetresPerInch;
}
