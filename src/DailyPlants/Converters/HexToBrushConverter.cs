using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace DailyPlants.Converters;

/// <summary>
/// Converts a hex color string to a SolidColorBrush.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(255, r, g, b));
                }
            }
            catch
            {
                // Fall through to default
            }
        }
        return new SolidColorBrush(Color.FromArgb(255, 136, 136, 136));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
