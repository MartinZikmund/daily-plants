using Microsoft.UI.Xaml.Data;

namespace DailyPlants.Converters;

/// <summary>
/// Converts a progress value (0.0-1.0) to a color brush for the serving badge.
/// Interpolates between gray (0%) and the accent green (100%) based on progress.
/// </summary>
public class ProgressToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double progress = value is double d ? d : 0;
        var color = ProgressColorHelper.GetProgressColor(progress);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
