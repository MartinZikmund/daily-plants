using DailyDozen.Models;
using DailyDozen.Services;

namespace DailyDozen.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private UserSettings _settings = new();

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
    private string _goalWeightText = "";

    [ObservableProperty]
    private string _heightText = "";

    public List<string> ThemeOptions { get; } = ["System", "Light", "Dark"];

    public string WeightUnit => UseMetricUnits ? "kg" : "lb";
    public string HeightUnit => UseMetricUnits ? "cm" : "in";

    public SettingsViewModel(IDataService dataService)
    {
        _dataService = dataService;
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
}
