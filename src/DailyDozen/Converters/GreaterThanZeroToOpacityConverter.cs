using Microsoft.UI.Xaml.Data;

namespace DailyDozen.Converters;

/// <summary>
/// Converts a number to opacity - 1.0 if greater than zero, 0.3 if zero.
/// Useful for dimming buttons when they can't be used.
/// </summary>
public class GreaterThanZeroToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            return intValue > 0 ? 1.0 : 0.3;
        }
        if (value is double doubleValue)
        {
            return doubleValue > 0 ? 1.0 : 0.3;
        }
        return 0.3;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
