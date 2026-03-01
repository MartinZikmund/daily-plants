using DailyPlants.Models;
using DailyPlants.Services;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IExportService _exportService;
    private readonly ILocalizationService _localizationService;
    private UserSettings _settings = new();
    private string _initialLanguage = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _dailyDozenEnabled;

    [ObservableProperty]
    private bool _twentyOneTweaksEnabled;

    [ObservableProperty]
    private bool _antiAgingEightEnabled;

    [ObservableProperty]
    private bool _weightTrackingEnabled;

    [ObservableProperty]
    private bool _useMetricUnits;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private bool _showRestartMessage;

    [ObservableProperty]
    private string _goalWeightText = "";

    [ObservableProperty]
    private string _heightText = "";

    public List<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public List<string> LanguageOptions { get; } = ["English", "Cestina"];

    public string WeightUnit => UseMetricUnits ? "kg" : "lb";
    public string HeightUnit => UseMetricUnits ? "cm" : "in";

    public SettingsViewModel(IDataService dataService, IExportService exportService, ILocalizationService localizationService)
    {
        _dataService = dataService;
        _exportService = exportService;
        _localizationService = localizationService;
    }

    public async Task LoadSettingsAsync()
    {
        IsLoading = true;

        try
        {
            _settings = await _dataService.GetSettingsAsync();
            DailyDozenEnabled = _settings.DailyDozenEnabled;
            TwentyOneTweaksEnabled = _settings.TwentyOneTweaksEnabled;
            AntiAgingEightEnabled = _settings.AntiAgingEightEnabled;
            WeightTrackingEnabled = _settings.WeightTrackingEnabled;
            UseMetricUnits = _settings.UseMetricUnits;
            SelectedThemeIndex = _settings.ThemePreference;

            // Load weight-related settings
            GoalWeightText = _settings.GoalWeight?.ToString("F1") ?? "";
            HeightText = _settings.HeightCm?.ToString("F0") ?? "";
            OnPropertyChanged(nameof(WeightUnit));
            OnPropertyChanged(nameof(HeightUnit));

            // Load language setting
            _initialLanguage = _localizationService.CurrentLanguage;
            SelectedLanguageIndex = _initialLanguage == "cs" ? 1 : 0;
            ShowRestartMessage = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnDailyDozenEnabledChanged(bool value)
    {
        _settings.DailyDozenEnabled = value;
        _ = SaveSettingsAsync();
    }

    partial void OnTwentyOneTweaksEnabledChanged(bool value)
    {
        _settings.TwentyOneTweaksEnabled = value;
        _ = SaveSettingsAsync();
    }

    partial void OnAntiAgingEightEnabledChanged(bool value)
    {
        _settings.AntiAgingEightEnabled = value;
        _ = SaveSettingsAsync();
    }

    partial void OnWeightTrackingEnabledChanged(bool value)
    {
        _settings.WeightTrackingEnabled = value;
        _ = SaveSettingsAsync();
    }

    partial void OnUseMetricUnitsChanged(bool value)
    {
        _settings.UseMetricUnits = value;
        _ = SaveSettingsAsync();
        OnPropertyChanged(nameof(WeightUnit));
        OnPropertyChanged(nameof(HeightUnit));
    }

    partial void OnGoalWeightTextChanged(string value)
    {
        if (double.TryParse(value, out var weight) && weight > 0)
        {
            _settings.GoalWeight = weight;
        }
        else
        {
            _settings.GoalWeight = null;
        }
        _ = SaveSettingsAsync();
    }

    partial void OnHeightTextChanged(string value)
    {
        if (double.TryParse(value, out var height) && height > 0)
        {
            _settings.HeightCm = height;
        }
        else
        {
            _settings.HeightCm = null;
        }
        _ = SaveSettingsAsync();
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _settings.ThemePreference = value;
        _ = SaveSettingsAsync();
        ApplyTheme(value);
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (IsLoading) return;

        var languageCode = value == 1 ? "cs" : "en";
        _ = _localizationService.SetLanguageAsync(languageCode);

        // Show restart message if language changed
        ShowRestartMessage = languageCode != _initialLanguage;
    }

    private async Task SaveSettingsAsync()
    {
        await _dataService.SaveSettingsAsync(_settings);
    }

    public static void ApplyTheme(int themeIndex)
    {
        if (App.Current.MainWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = themeIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    [RelayCommand]
    private async Task ExportToJsonAsync()
    {
        try
        {
            var json = await _exportService.ExportToJsonAsync();
            await SaveFileAsync("daily-dozen-export.json", json, ".json", "JSON files");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        try
        {
            var csv = await _exportService.ExportToCsvAsync();
            await SaveFileAsync("daily-dozen-export.csv", csv, ".csv", "CSV files");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportFromJsonAsync()
    {
        try
        {
            var json = await OpenFileAsync(".json", "JSON files");
            if (string.IsNullOrEmpty(json)) return;

            var result = await _exportService.ImportFromJsonAsync(json);
            if (result.Success)
            {
                await ShowSuccessAsync($"Imported {result.EntriesImported} entries and {result.WeightEntriesImported} weight records.");
                await LoadSettingsAsync(); // Refresh settings in case they were imported
            }
            else
            {
                await ShowErrorAsync(result.ErrorMessage ?? "Import failed");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportFromCsvAsync()
    {
        try
        {
            var csv = await OpenFileAsync(".csv", "CSV files");
            if (string.IsNullOrEmpty(csv)) return;

            var result = await _exportService.ImportFromCsvAsync(csv);
            if (result.Success)
            {
                await ShowSuccessAsync($"Imported {result.EntriesImported} entries.");
            }
            else
            {
                await ShowErrorAsync(result.ErrorMessage ?? "Import failed");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Import failed: {ex.Message}");
        }
    }

    private static async Task SaveFileAsync(string suggestedName, string content, string extension, string fileTypeDescription)
    {
        var fileSavePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName
        };
        fileSavePicker.FileTypeChoices.Add(fileTypeDescription, [extension]);

#if WINDOWS
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);
#endif

        var file = await fileSavePicker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
        }
    }

    private static async Task<string?> OpenFileAsync(string extension, string fileTypeDescription)
    {
        var fileOpenPicker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        fileOpenPicker.FileTypeFilter.Add(extension);

#if WINDOWS
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);
#endif

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null)
        {
            return await FileIO.ReadTextAsync(file);
        }
        return null;
    }

    private static async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = App.Current.MainWindow?.Content?.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static async Task ShowSuccessAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Success",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = App.Current.MainWindow?.Content?.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
