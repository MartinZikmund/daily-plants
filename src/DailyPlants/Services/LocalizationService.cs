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
        new LanguageOption { Code = "bg", DisplayName = "Bulgarian", NativeName = "Български" },
        new LanguageOption { Code = "ca", DisplayName = "Catalan", NativeName = "Català" },
        new LanguageOption { Code = "cs", DisplayName = "Czech", NativeName = "Čeština" },
        new LanguageOption { Code = "de", DisplayName = "German", NativeName = "Deutsch" },
        new LanguageOption { Code = "el", DisplayName = "Greek", NativeName = "Ελληνικά" },
        new LanguageOption { Code = "es", DisplayName = "Spanish", NativeName = "Español" },
        new LanguageOption { Code = "fr", DisplayName = "French", NativeName = "Français" },
        new LanguageOption { Code = "he", DisplayName = "Hebrew", NativeName = "עברית" },
        new LanguageOption { Code = "hu", DisplayName = "Hungarian", NativeName = "Magyar" },
        new LanguageOption { Code = "it", DisplayName = "Italian", NativeName = "Italiano" },
        new LanguageOption { Code = "fa", DisplayName = "Persian", NativeName = "فارسی" },
        new LanguageOption { Code = "pl", DisplayName = "Polish", NativeName = "Polski" },
        new LanguageOption { Code = "pt-BR", DisplayName = "Portuguese (Brazil)", NativeName = "Português (Brasil)" },
        new LanguageOption { Code = "pt-PT", DisplayName = "Portuguese (Portugal)", NativeName = "Português (Portugal)" },
        new LanguageOption { Code = "ro", DisplayName = "Romanian", NativeName = "Română" },
        new LanguageOption { Code = "ru", DisplayName = "Russian", NativeName = "Русский" },
        new LanguageOption { Code = "sk", DisplayName = "Slovak", NativeName = "Slovenčina" },
        new LanguageOption { Code = "uk", DisplayName = "Ukrainian", NativeName = "Українська" },
        new LanguageOption { Code = "zh-Hans", DisplayName = "Chinese (Simplified)", NativeName = "简体中文" },
        new LanguageOption { Code = "zh-Hant", DisplayName = "Chinese (Traditional)", NativeName = "繁體中文" }
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
