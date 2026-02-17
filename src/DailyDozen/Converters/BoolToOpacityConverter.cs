using Microsoft.UI.Xaml.Data;

namespace DailyDozen.Converters;

/// <summary>
/// Converts a boolean to opacity - 1.0 if true, 0.0 if false.
/// Used for showing/hiding elements while preserving layout space.
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 1.0 : 0.0;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
