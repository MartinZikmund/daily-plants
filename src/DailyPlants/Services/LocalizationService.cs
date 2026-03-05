using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Services.Settings;
using Windows.Globalization;

namespace DailyPlants.Services;

/// <summary>
/// Service for managing app localization and language switching.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the list of supported languages.
    /// </summary>
    IReadOnlyList<LanguageOption> SupportedLanguages { get; }

    /// <summary>
    /// Gets or sets the current language code (e.g., "en", "cs").
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Sets the app language. Requires app restart to take full effect.
    /// </summary>
    Task SetLanguageAsync(string languageCode);

    /// <summary>
    /// Initializes the localization service with the saved language preference.
    /// Should be called on app startup.
    /// </summary>
    Task InitializeAsync();
}

/// <summary>
/// Represents a language option for the UI.
/// </summary>
public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
}

/// <summary>
/// Implementation of ILocalizationService.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IAppPreferences _appPreferences;
    private string _currentLanguage = "en";

    private static readonly List<LanguageOption> _supportedLanguages =
    [
        new LanguageOption { Code = "en", DisplayName = "English", NativeName = "English" },
        new LanguageOption { Code = "cs", DisplayName = "Czech", NativeName = "Cestina" }
    ];

    public LocalizationService(IAppPreferences appPreferences)
    {
        _appPreferences = appPreferences;
    }

    public IReadOnlyList<LanguageOption> SupportedLanguages => _supportedLanguages;

    public string CurrentLanguage => _currentLanguage;

    public Task InitializeAsync()
    {
        var language = _appPreferences.Language;

        if (!string.IsNullOrEmpty(language))
        {
            _currentLanguage = language;
        }
        else
        {
            // Use system language if supported, otherwise default to English
            var systemLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            _currentLanguage = _supportedLanguages.Any(l => l.Code == systemLanguage)
                ? systemLanguage
                : "en";
        }

        ApplyLanguage(_currentLanguage);
        return Task.CompletedTask;
    }

    public Task SetLanguageAsync(string languageCode)
    {
        if (!_supportedLanguages.Any(l => l.Code == languageCode))
        {
            return Task.CompletedTask;
        }

        _currentLanguage = languageCode;

        // Save to preferences
        _appPreferences.Language = languageCode;

        // Apply the language
        ApplyLanguage(languageCode);

        // Reset the resource loader to pick up new resources
        Localizer.Reset();

        return Task.CompletedTask;
    }

    private static void ApplyLanguage(string languageCode)
    {
        // Set the primary language override
        ApplicationLanguages.PrimaryLanguageOverride = languageCode;

        // Set the current culture
        var culture = new CultureInfo(languageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
