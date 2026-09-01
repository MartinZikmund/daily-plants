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

    /// <summary>
    /// The receiver is the value being converted; the method name says which way it goes.
    /// A metric user stores what they type, so every conversion is a no-op for them.
    /// </summary>
    extension(double value)
    {
        public double KilogramsToDisplay(bool useMetric) =>
            useMetric ? value : value * PoundsPerKilogram;

        public double DisplayToKilograms(bool useMetric) =>
            useMetric ? value : value / PoundsPerKilogram;

        public double CentimetresToDisplay(bool useMetric) =>
            useMetric ? value : value / CentimetresPerInch;

        public double DisplayToCentimetres(bool useMetric) =>
            useMetric ? value : value * CentimetresPerInch;
    }
}
