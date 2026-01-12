using Microsoft.UI.Xaml.Markup;
using Windows.ApplicationModel.Resources;

namespace DailyDozen.Helpers;

/// <summary>
/// A markup extension that provides localized strings from resource files.
/// Usage: Text="{helpers:Localize Key=MyResourceKey}"
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class LocalizeExtension : MarkupExtension
{
    private static ResourceLoader? _resourceLoader;
    private static readonly object _lock = new();

    /// <summary>
    /// The resource key to look up.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ResourceLoader instance, creating it if necessary.
    /// </summary>
    private static ResourceLoader ResourceLoader
    {
        get
        {
            if (_resourceLoader == null)
            {
                lock (_lock)
                {
                    _resourceLoader ??= ResourceLoader.GetForViewIndependentUse();
                }
            }
            return _resourceLoader;
        }
    }

    /// <summary>
    /// Resets the ResourceLoader to force reloading resources.
    /// Call this when the language changes.
    /// </summary>
    public static void ResetResourceLoader()
    {
        lock (_lock)
        {
            _resourceLoader = null;
        }
    }

    protected override object ProvideValue()
    {
        if (string.IsNullOrEmpty(Key))
        {
            return string.Empty;
        }

        try
        {
            var value = ResourceLoader.GetString(Key);
            return string.IsNullOrEmpty(value) ? $"[{Key}]" : value;
        }
        catch
        {
            return $"[{Key}]";
        }
    }
}

/// <summary>
/// Static helper class for accessing localized strings from code.
/// </summary>
public static class Localizer
{
    private static ResourceLoader? _resourceLoader;
    private static readonly object _lock = new();

    private static ResourceLoader ResourceLoader
    {
        get
        {
            if (_resourceLoader == null)
            {
                lock (_lock)
                {
                    _resourceLoader ??= ResourceLoader.GetForViewIndependentUse();
                }
            }
            return _resourceLoader;
        }
    }

    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    public static string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        try
        {
            var value = ResourceLoader.GetString(key);
            return string.IsNullOrEmpty(value) ? $"[{key}]" : value;
        }
        catch
        {
            return $"[{key}]";
        }
    }

    /// <summary>
    /// Resets the ResourceLoader to force reloading resources.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _resourceLoader = null;
        }
        LocalizeExtension.ResetResourceLoader();
    }
}
