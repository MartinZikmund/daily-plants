using DailyPlants.Models;
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
    private bool _suppressPreferenceWrites;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool DailyDozenEnabled { get; set; }

    [ObservableProperty]
    public partial bool TwentyOneTweaksEnabled { get; set; }

    [ObservableProperty]
    public partial bool WeightTrackingEnabled { get; set; }

    [ObservableProperty]
    public partial bool UseMetricUnits { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool ShowRestartMessage { get; set; }

    [ObservableProperty]
    public partial string GoalWeightText { get; set; } = "";

    [ObservableProperty]
    public partial string HeightText { get; set; } = "";
    public ObservableCollection<ChecklistItemToggleViewModel> DailyDozenItems { get; } = [];
    public ObservableCollection<ChecklistItemToggleViewModel> TwentyOneTweaksItems { get; } = [];

    public List<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public List<string> LanguageOptions { get; private set; } = [];

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
            WeightTrackingEnabled = _appPreferences.WeightTrackingEnabled;
            UseMetricUnits = _appPreferences.UseMetricUnits;
            SelectedThemeIndex = _appPreferences.ThemePreference;

            _suppressPreferenceWrites = true;
            GoalWeightText = FormatGoalWeight(_appPreferences.GoalWeight);
            HeightText = FormatHeight(_appPreferences.HeightCm);
            _suppressPreferenceWrites = false;
            OnPropertyChanged(nameof(WeightUnit));
            OnPropertyChanged(nameof(HeightUnit));

            LanguageOptions = _localizationService.SupportedLanguages
                .Select(l => l.NativeName)
                .ToList();
            OnPropertyChanged(nameof(LanguageOptions));

            _initialLanguage = _localizationService.CurrentLanguage;
            SelectedLanguageIndex = _localizationService.SupportedLanguages
                .Select((l, i) => (l, i))
                .FirstOrDefault(x => x.l.Code == _initialLanguage).i;
            ShowRestartMessage = false;

            PopulateItemToggles();
        }
        finally
        {
            IsLoading = false;
        }

        return Task.CompletedTask;
    }

    private void PopulateItemToggles()
    {
        // Build a dictionary of shared toggle instances keyed by item ID
        // so that items appearing in multiple checklists stay synced.
        var togglesByItemId = new Dictionary<string, ChecklistItemToggleViewModel>();

        DailyDozenItems.Clear();
        TwentyOneTweaksItems.Clear();

        PopulateChecklistItems(ChecklistType.DailyDozen, DailyDozenItems, togglesByItemId);
        PopulateChecklistItems(ChecklistType.TwentyOneTweaks, TwentyOneTweaksItems, togglesByItemId);
    }

    private void PopulateChecklistItems(
        ChecklistType checklist,
        ObservableCollection<ChecklistItemToggleViewModel> collection,
        Dictionary<string, ChecklistItemToggleViewModel> togglesByItemId)
    {
        foreach (var item in ChecklistDefinitions.GetItemsForChecklist(checklist).OrderBy(i => i.SortOrder))
        {
            if (togglesByItemId.TryGetValue(item.Id, out var existing))
            {
                // Reuse the same instance so toggling syncs across checklists
                collection.Add(existing);
            }
            else
            {
                var toggle = new ChecklistItemToggleViewModel(_appPreferences, item);
                togglesByItemId[item.Id] = toggle;
                collection.Add(toggle);
            }
        }
    }

    partial void OnDailyDozenEnabledChanged(bool value)
    {
        _appPreferences.DailyDozenEnabled = value;
    }

    partial void OnTwentyOneTweaksEnabledChanged(bool value)
    {
        _appPreferences.TwentyOneTweaksEnabled = value;
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

        // Re-render the stored (canonical) values in the newly selected unit.
        // Writes are suppressed so a round trip through the toggle cannot drift
        // the stored value through repeated display rounding.
        _suppressPreferenceWrites = true;
        GoalWeightText = FormatGoalWeight(_appPreferences.GoalWeight);
        HeightText = FormatHeight(_appPreferences.HeightCm);
        _suppressPreferenceWrites = false;
    }

    private string FormatGoalWeight(double? goalWeightKg) => goalWeightKg.HasValue
        ? goalWeightKg.Value.KilogramsToDisplay(UseMetricUnits).ToString("F1")
        : "";

    private string FormatHeight(double? heightCm) => heightCm.HasValue
        ? heightCm.Value.CentimetresToDisplay(UseMetricUnits).ToString("F1")
        : "";

    partial void OnGoalWeightTextChanged(string value)
    {
        if (_suppressPreferenceWrites) return;

        if (double.TryParse(value, out var weight) && weight > 0)
        {
            _appPreferences.GoalWeight = weight.DisplayToKilograms(UseMetricUnits);
        }
        else
        {
            _appPreferences.GoalWeight = null;
        }
    }

    partial void OnHeightTextChanged(string value)
    {
        if (_suppressPreferenceWrites) return;

        if (double.TryParse(value, out var height) && height > 0)
        {
            _appPreferences.HeightCm = height.DisplayToCentimetres(UseMetricUnits);
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

        var languages = _localizationService.SupportedLanguages;
        if (value < 0 || value >= languages.Count) return;

        var languageCode = languages[value].Code;
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
            await SaveFileAsync("daily-plants-export.json", json, ".json", "JSON files");
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
            await SaveFileAsync("daily-plants-export.csv", csv, ".csv", "CSV files");
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

            if (!await ConfirmImportAsync()) return;

            var result = await _exportService.ImportFromJsonAsync(json);
            if (result.Success)
            {
                await ShowSuccessAsync(DescribeImport(result));
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

            if (!await ConfirmImportAsync()) return;

            var result = await _exportService.ImportFromCsvAsync(csv);
            if (result.Success)
            {
                await ShowSuccessAsync(DescribeImport(result));
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

    private static async Task SaveFileAsync(string suggestedName, string content, string extension, string fileTypeDescription)
    {
        var fileSavePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName
        };
        fileSavePicker.FileTypeChoices.Add(fileTypeDescription, [extension]);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);

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

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null)
        {
            return await FileIO.ReadTextAsync(file);
        }
        return null;
    }

    /// <summary>
    /// Import upserts over existing entries, so the user gets a chance to back out first.
    /// </summary>
    private static async Task<bool> ConfirmImportAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Import data",
            Content = "Entries for dates in this file will replace the ones already saved. "
                + "Export your current data first if you want a backup. Continue?",
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Current.MainWindow?.Content?.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string DescribeImport(ImportResult result)
    {
        var message = $"Imported {result.EntriesImported} entries, {result.WeightEntriesImported} weight records "
            + $"and {result.AchievementsImported} achievements.";

        // Never report a partial import as a clean one.
        return result.EntriesSkipped > 0
            ? message + $" {result.EntriesSkipped} rows were skipped because the app could not read them."
            : message;
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

/// <summary>
/// ViewModel for a single checklist item toggle in settings.
/// Shared instances are reused across checklists so toggling syncs automatically.
/// </summary>
public partial class ChecklistItemToggleViewModel : ObservableObject
{
    private readonly IAppPreferences _appPreferences;

    public string ItemId { get; }
    public string ItemName { get; }
    public string? IconPath { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>Set while the constructor seeds the toggle, which is not the user toggling it.</summary>
    private bool _loading;

    public ChecklistItemToggleViewModel(IAppPreferences appPreferences, ChecklistItem item)
    {
        _appPreferences = appPreferences;
        ItemId = item.Id;
        ItemName = item.Name;
        IconPath = item.IconPath;

        _loading = true;
        IsEnabled = !appPreferences.IsItemDisabled(item.Id);
        _loading = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_loading) return;

        _appPreferences.SetItemDisabled(ItemId, !value);
    }
}
