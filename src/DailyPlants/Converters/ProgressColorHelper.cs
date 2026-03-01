namespace DailyPlants.Converters;

/// <summary>
/// Shared helper for calculating progress-based badge colors.
/// </summary>
internal static class ProgressColorHelper
{
    // Base gray colors for 0% progress
    private static readonly Windows.UI.Color DarkThemeGray = Windows.UI.Color.FromArgb(255, 88, 88, 88); // #585858
    private static readonly Windows.UI.Color LightThemeGray = Windows.UI.Color.FromArgb(255, 220, 220, 220); // #DCDCDC - very light gray

    /// <summary>
    /// Calculates the interpolated color between gray and accent green based on progress.
    /// </summary>
    public static Windows.UI.Color GetProgressColor(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);

        var isLightTheme = Application.Current.RequestedTheme == ApplicationTheme.Light;
        var grayColor = isLightTheme ? LightThemeGray : DarkThemeGray;

        // Get the accent green color from resources
        Windows.UI.Color accentGreen;
        if (Application.Current.Resources["AccentFillColorDefaultBrush"] is SolidColorBrush accentBrush)
        {
            accentGreen = accentBrush.Color;
        }
        else
        {
            // Fallback accent green (different for light/dark)
            accentGreen = isLightTheme
                ? Windows.UI.Color.FromArgb(255, 76, 175, 80)   // #4CAF50 for light
                : Windows.UI.Color.FromArgb(255, 102, 187, 106); // #66BB6A for dark
        }

        // Interpolate between gray and accent green based on progress
        var r = (byte)(grayColor.R + (accentGreen.R - grayColor.R) * progress);
        var g = (byte)(grayColor.G + (accentGreen.G - grayColor.G) * progress);
        var b = (byte)(grayColor.B + (accentGreen.B - grayColor.B) * progress);

        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    /// <summary>
    /// Calculates the relative luminance of a color (ITU-R BT.709).
    /// Returns a value between 0 (black) and 1 (white).
    /// </summary>
    public static double GetLuminance(Windows.UI.Color color)
    {
        return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
    }
}
