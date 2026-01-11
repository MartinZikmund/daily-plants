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
    }

    private async Task SaveSettingsAsync()
    {
        await _dataService.SaveSettingsAsync(_settings);
    }
}
