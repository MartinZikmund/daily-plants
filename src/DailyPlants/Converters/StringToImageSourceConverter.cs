using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DailyPlants.Converters;

/// <summary>
/// Converts an absolute URL string to an <see cref="ImageSource"/> for Image.Source.
/// </summary>
/// <remarks>
/// x:Bind casts a converter result straight to the target type, so the Uri that
/// <see cref="StringToUriConverter"/> returns works for BitmapIcon.UriSource but not here.
/// </remarks>
public class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language) =>
        value is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new BitmapImage(uri)
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
