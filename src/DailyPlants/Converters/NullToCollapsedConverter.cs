using Microsoft.UI.Xaml.Data;

namespace DailyPlants.Converters;

/// <summary>
/// Converts a null value to Collapsed, non-null to Visible.
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
