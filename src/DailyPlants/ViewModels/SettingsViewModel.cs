using DailyPlants.Services;
using DailyPlants.Services.Settings;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppPreferences _appPreferences;
    private readonly IExportService _exportService;
    private readonly ILocalizationService _localizationService;
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

    public SettingsViewModel(IAppPreferences appPreferences, IExportService exportService, ILocalizationService localizationService)
    {
        _appPreferences = appPreferences;
        _exportService = exportService;
        _localizationService = localizationService;
    }

    public Task LoadSettingsAsync()
    {
        IsLoading = true;

        try
        {
            DailyDozenEnabled = _appPreferences.DailyDozenEnabled;
            TwentyOneTweaksEnabled = _appPreferences.TwentyOneTweaksEnabled;
            AntiAgingEightEnabled = _appPreferences.AntiAgingEightEnabled;
            WeightTrackingEnabled = _appPreferences.WeightTrackingEnabled;
            UseMetricUnits = _appPreferences.UseMetricUnits;
            SelectedThemeIndex = _appPreferences.ThemePreference;

            GoalWeightText = _appPreferences.GoalWeight?.ToString("F1") ?? "";
            HeightText = _appPreferences.HeightCm?.ToString("F0") ?? "";
            OnPropertyChanged(nameof(WeightUnit));
            OnPropertyChanged(nameof(HeightUnit));

            _initialLanguage = _localizationService.CurrentLanguage;
            SelectedLanguageIndex = _initialLanguage == "cs" ? 1 : 0;
            ShowRestartMessage = false;
        }
        finally
        {
            IsLoading = false;
        }

        return Task.CompletedTask;
    }

    partial void OnDailyDozenEnabledChanged(bool value)
    {
        _appPreferences.DailyDozenEnabled = value;
    }

    partial void OnTwentyOneTweaksEnabledChanged(bool value)
    {
        _appPreferences.TwentyOneTweaksEnabled = value;
    }

    partial void OnAntiAgingEightEnabledChanged(bool value)
    {
        _appPreferences.AntiAgingEightEnabled = value;
    }

    partial void OnWeightTrackingEnabledChanged(bool value)
    {
        _appPreferences.WeightTrackingEnabled = value;
    }

    partial void OnUseMetricUnitsChanged(bool value)
    {
        _appPreferences.UseMetricUnits = value;
        OnPropertyChanged(nameof(WeightUnit));
        OnPropertyChanged(nameof(HeightUnit));
    }

    partial void OnGoalWeightTextChanged(string value)
    {
        if (double.TryParse(value, out var weight) && weight > 0)
        {
            _appPreferences.GoalWeight = weight;
        }
        else
        {
            _appPreferences.GoalWeight = null;
        }
    }

    partial void OnHeightTextChanged(string value)
    {
        if (double.TryParse(value, out var height) && height > 0)
        {
            _appPreferences.HeightCm = height;
        }
        else
        {
            _appPreferences.HeightCm = null;
        }
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _appPreferences.ThemePreference = value;
        ApplyTheme(value);
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (IsLoading) return;

        var languageCode = value == 1 ? "cs" : "en";
        _ = _localizationService.SetLanguageAsync(languageCode);

        ShowRestartMessage = languageCode != _initialLanguage;
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
                await LoadSettingsAsync();
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
