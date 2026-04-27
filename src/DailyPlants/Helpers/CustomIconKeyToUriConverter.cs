using DailyPlants.Services;

namespace DailyPlants.Helpers;

/// <summary>
/// Lightweight wrapper that pairs a custom-icon catalog key with the URI of its PNG asset.
/// Used by the icon picker GridView so XAML bindings can reference both fields directly.
/// </summary>
public class CustomIconCatalogEntry
{
    public required string Key { get; init; }
    public required Uri Uri { get; init; }

    public static IReadOnlyList<CustomIconCatalogEntry> CreateAll() =>
        CustomIconCatalog.AllKeys
            .Select(k => new CustomIconCatalogEntry
            {
                Key = k,
                Uri = new Uri(CustomIconCatalog.GetIconPath(k)),
            })
            .ToList();
}
