using Microsoft.UI.Xaml.Data;

namespace DailyDozen.Converters;

/// <summary>
/// Converts a count greater than zero to Visible, Collapsed otherwise.
/// </summary>
public class GreaterThanZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
