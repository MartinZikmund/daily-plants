namespace DailyPlants.Services;

/// <summary>
/// Built-in catalog of glyph keys for user-defined custom checklist items.
/// Each key maps to a PNG asset under Assets/Icons/CustomItems/.
/// </summary>
public static class CustomIconCatalog
{
    public const string DefaultKey = "default";

    private static readonly string[] _allKeys =
    [
        "pill",
        "walk",
        "run",
        "water",
        "sleep",
        "meditate",
        "yoga",
        "book",
        "journal",
        "sun",
        "bike",
        "dumbbell",
        "apple",
        "tea",
        "heart",
        "star",
    ];

    private static readonly HashSet<string> _knownKeys =
    [
        .. _allKeys,
        DefaultKey,
    ];

    /// <summary>
    /// All user-pickable catalog keys (excludes the default fallback).
    /// </summary>
    public static IReadOnlyList<string> AllKeys => _allKeys;

    /// <summary>
    /// Returns true when the key is a user-pickable catalog key OR the default fallback.
    /// </summary>
    public static bool IsKnown(string key) => !string.IsNullOrEmpty(key) && _knownKeys.Contains(key);

    /// <summary>
    /// Resolves a catalog key to an ms-appx URI. Unknown keys fall back to the default glyph
    /// without rewriting the caller's stored value.
    /// </summary>
    public static string GetIconPath(string key)
    {
        var resolved = IsKnown(key) ? key : DefaultKey;
        return $"ms-appx:///Assets/Icons/CustomItems/{resolved}.png";
    }
}
