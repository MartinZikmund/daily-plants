using Microsoft.UI.Xaml.Data;

namespace DailyPlants.Converters;

/// <summary>
/// Converts a string path to a Uri for use with BitmapIcon.UriSource.
/// </summary>
public class StringToUriConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            return new Uri(path);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
