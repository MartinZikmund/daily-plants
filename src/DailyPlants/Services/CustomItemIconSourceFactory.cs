using DailyPlants.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DailyPlants.Services;

/// <summary>
/// Builds an <see cref="IconSource"/> for a custom-item icon binding.
/// </summary>
public static class CustomItemIconSourceFactory
{
    private const string EmojiFontFamily = "Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji, Segoe UI Symbol";

    public static IconSource Create(CustomItemIconType type, string value)
    {
        if (type == CustomItemIconType.Emoji && !string.IsNullOrEmpty(value))
        {
            return new FontIconSource
            {
                Glyph = value,
                FontFamily = new FontFamily(EmojiFontFamily),
            };
        }

        return new BitmapIconSource
        {
            UriSource = new Uri(CustomIconCatalog.GetIconPath(value)),
            ShowAsMonochrome = false,
        };
    }
}
