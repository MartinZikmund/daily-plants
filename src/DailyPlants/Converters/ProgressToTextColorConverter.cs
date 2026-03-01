using Microsoft.UI.Xaml.Data;

namespace DailyPlants.Converters;

/// <summary>
/// Converts a progress value to a text color (black or white) based on the
/// background luminance for optimal contrast.
/// </summary>
public class ProgressToTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double progress = value is double d ? d : 0;
        var bgColor = ProgressColorHelper.GetProgressColor(progress);
        double luminance = ProgressColorHelper.GetLuminance(bgColor);

        // Use black text for light backgrounds, white text for dark backgrounds
        if (luminance > 0.5)
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)); // Black
        }
        else
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)); // White
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
