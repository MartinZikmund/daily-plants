using System.Globalization;
using DailyPlants.Helpers;
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
    private readonly IDataService _dataService;
    private string _currentLanguage = "en";

    private static readonly List<LanguageOption> _supportedLanguages =
    [
        new LanguageOption { Code = "en", DisplayName = "English", NativeName = "English" },
        new LanguageOption { Code = "cs", DisplayName = "Czech", NativeName = "Cestina" }
    ];

    public LocalizationService(IDataService dataService)
    {
        _dataService = dataService;
    }

    public IReadOnlyList<LanguageOption> SupportedLanguages => _supportedLanguages;

    public string CurrentLanguage => _currentLanguage;

    public async Task InitializeAsync()
    {
        var settings = await _dataService.GetSettingsAsync();

        if (!string.IsNullOrEmpty(settings.Language))
        {
            _currentLanguage = settings.Language;
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
    }

    public async Task SetLanguageAsync(string languageCode)
    {
        if (!_supportedLanguages.Any(l => l.Code == languageCode))
        {
            return;
        }

        _currentLanguage = languageCode;

        // Save to settings
        var settings = await _dataService.GetSettingsAsync();
        settings.Language = languageCode;
        await _dataService.SaveSettingsAsync(settings);

        // Apply the language
        ApplyLanguage(languageCode);

        // Reset the resource loader to pick up new resources
        Localizer.Reset();
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
