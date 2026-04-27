using DailyPlants.Helpers;
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
    private readonly ICustomItemService? _customItemService;
    private readonly IDataService? _dataService;
    private string _initialLanguage = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _dailyDozenEnabled;

    [ObservableProperty]
    private bool _twentyOneTweaksEnabled;

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

    public ObservableCollection<ChecklistItemToggleViewModel> DailyDozenItems { get; } = [];
    public ObservableCollection<ChecklistItemToggleViewModel> TwentyOneTweaksItems { get; } = [];

    public ObservableCollection<CustomItemListItemViewModel> CustomItems { get; } = [];

    public bool HasCustomItems => CustomItems.Count > 0;

    public bool IsCustomItemsEmpty => CustomItems.Count == 0;

    public List<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public List<string> LanguageOptions { get; private set; } = [];

    public string WeightUnit => UseMetricUnits ? "kg" : "lb";
    public string HeightUnit => UseMetricUnits ? "cm" : "in";

    /// <summary>
    /// Raised when the view should present a dialog backed by the supplied editor view-model.
    /// Returns true after Save completed, false if the user cancelled.
    /// </summary>
    public event Func<CustomItemEditorViewModel, Task<bool>>? CustomItemDialogRequested;

    /// <summary>
    /// Raised when the view should ask the user how to handle a custom-item delete.
    /// Returns the chosen action (KeepHistory / Cascade / Cancel).
    /// </summary>
    public event Func<CustomItem, Task<CustomItemDeleteChoice>>? CustomItemDeletePromptRequested;

    public SettingsViewModel(
        IAppPreferences appPreferences,
        IExportService exportService,
        ILocalizationService localizationService,
        ICustomItemService? customItemService = null,
        IDataService? dataService = null)
    {
        _appPreferences = appPreferences;
        _exportService = exportService;
        _localizationService = localizationService;
        _customItemService = customItemService;
        _dataService = dataService;
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

            GoalWeightText = _appPreferences.GoalWeight?.ToString("F1") ?? "";
            HeightText = _appPreferences.HeightCm?.ToString("F0") ?? "";
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

        return RefreshCustomItemsAsync();
    }

    private async Task RefreshCustomItemsAsync()
    {
        if (_customItemService is null) return;

        var items = await _customItemService.GetAllAsync();
        CustomItems.Clear();
        foreach (var item in items)
        {
            CustomItems.Add(new CustomItemListItemViewModel(item));
        }
        OnPropertyChanged(nameof(HasCustomItems));
        OnPropertyChanged(nameof(IsCustomItemsEmpty));
    }

    [RelayCommand]
    private async Task AddCustomItemAsync()
    {
        if (_customItemService is null || CustomItemDialogRequested is null) return;

        var existing = await _customItemService.GetAllAsync();
        var editorVm = new CustomItemEditorViewModel(_customItemService, existing);

        var saved = await CustomItemDialogRequested.Invoke(editorVm);
        if (saved)
        {
            await RefreshCustomItemsAsync();
        }
    }

    [RelayCommand]
    private async Task EditCustomItemAsync(CustomItemListItemViewModel? row)
    {
        if (row is null || _customItemService is null || CustomItemDialogRequested is null) return;

        var existing = await _customItemService.GetAllAsync();
        var editorVm = new CustomItemEditorViewModel(_customItemService, existing, row.Item);

        var saved = await CustomItemDialogRequested.Invoke(editorVm);
        if (saved)
        {
            await RefreshCustomItemsAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteCustomItemAsync(CustomItemListItemViewModel? row)
    {
        if (row is null || _customItemService is null) return;

        var hasEntries = await HasAnyEntriesAsync(row.Item.Id);

        var choice = CustomItemDeleteChoice.Cascade;
        if (hasEntries && CustomItemDeletePromptRequested is not null)
        {
            choice = await CustomItemDeletePromptRequested.Invoke(row.Item);
        }

        switch (choice)
        {
            case CustomItemDeleteChoice.KeepHistory:
                await _customItemService.DeleteAsync(row.Item.Id, cascadeEntries: false);
                break;
            case CustomItemDeleteChoice.Cascade:
                await _customItemService.DeleteAsync(row.Item.Id, cascadeEntries: true);
                break;
            case CustomItemDeleteChoice.Cancel:
                return;
        }

        await RefreshCustomItemsAsync();
    }

    private async Task<bool> HasAnyEntriesAsync(string customItemId)
    {
        if (_dataService is null) return false;

        var entries = await _dataService.GetCustomItemEntriesInRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        return entries.Any(e => e.CustomItemId == customItemId);
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

            var result = await _exportService.ImportFromJsonAsync(json);
            if (result.Success)
            {
                var summary =
                    $"Imported {result.EntriesImported} entries, {result.WeightEntriesImported} weight records, " +
                    $"{result.CustomItemsImported} custom items, {result.CustomItemEntriesImported} custom entries.";
                if (result.Warnings.Count > 0)
                {
                    summary += "\n\nWarnings:\n" + string.Join("\n", result.Warnings);
                }
                await ShowSuccessAsync(summary);
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
    private bool _isEnabled;

    public ChecklistItemToggleViewModel(IAppPreferences appPreferences, ChecklistItem item)
    {
        _appPreferences = appPreferences;
        ItemId = item.Id;
        ItemName = item.Name;
        IconPath = item.IconPath;
        _isEnabled = !appPreferences.IsItemDisabled(item.Id);
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _appPreferences.SetItemDisabled(ItemId, !value);
    }
}

/// <summary>
/// Row view-model for a CustomItem in the Settings list.
/// </summary>
public class CustomItemListItemViewModel
{
    public CustomItem Item { get; }

    public CustomItemListItemViewModel(CustomItem item)
    {
        Item = item;
    }

    public string Name => Item.Name;
    public int RecommendedServings => Item.RecommendedServings;

    public Microsoft.UI.Xaml.Controls.IconSource IconSource =>
        CustomItemIconSourceFactory.Create(Item.IconType, Item.IconValue);
}

/// <summary>
/// User's response to the delete-with-history prompt.
/// </summary>
public enum CustomItemDeleteChoice
{
    Cancel,
    KeepHistory,
    Cascade,
}
