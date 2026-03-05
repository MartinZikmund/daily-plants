using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Statistics page.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IAppPreferences _appPreferences;

    [ObservableProperty]
    private bool _isLoading;

    // Overview Stats
    [ObservableProperty]
    private double _todayProgress;

    [ObservableProperty]
    private string _todayProgressText = "0%";

    [ObservableProperty]
    private double _weekProgress;

    [ObservableProperty]
    private string _weekProgressText = "0%";

    [ObservableProperty]
    private double _monthProgress;

    [ObservableProperty]
    private string _monthProgressText = "0%";

    // Streaks
    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private int _longestStreak;

    [ObservableProperty]
    private string _currentStreakText = "0 days";

    [ObservableProperty]
    private string _longestStreakText = "0 days";

    // Item Stats
    public ObservableCollection<ItemStatViewModel> ItemStats { get; } = [];

    // Weekly Chart Data
    public ObservableCollection<DayProgressViewModel> WeeklyProgress { get; } = [];

    // Weight Tracking
    [ObservableProperty]
    private bool _weightTrackingEnabled;

    [ObservableProperty]
    private bool _useMetricUnits;

    [ObservableProperty]
    private string _weightInputText = "";

    [ObservableProperty]
    private double? _todayWeight;

    [ObservableProperty]
    private string _todayWeightText = "No entry";

    [ObservableProperty]
    private double? _goalWeight;

    [ObservableProperty]
    private string _goalWeightText = "Not set";

    [ObservableProperty]
    private string _weightChangeText = "";

    [ObservableProperty]
    private string _bmiText = "";

    [ObservableProperty]
    private double? _heightCm;

    public string WeightUnit => UseMetricUnits ? "kg" : "lb";

    public ObservableCollection<WeightDataPoint> WeightHistory { get; } = [];

    public StatisticsViewModel(IDataService dataService, IAppPreferences appPreferences)
    {
        _dataService = dataService;
        _appPreferences = appPreferences;
    }

    public async Task LoadStatisticsAsync()
    {
        IsLoading = true;

        try
        {
            var enabledItems = GetEnabledItems(_appPreferences);

            // Load weight settings
            WeightTrackingEnabled = _appPreferences.WeightTrackingEnabled;
            UseMetricUnits = _appPreferences.UseMetricUnits;
            GoalWeight = _appPreferences.GoalWeight;
            HeightCm = _appPreferences.HeightCm;
            OnPropertyChanged(nameof(WeightUnit));

            if (enabledItems.Count == 0 && !WeightTrackingEnabled)
            {
                // Nothing to show
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            if (enabledItems.Count > 0)
            {
                // Calculate today's progress
                await CalculateTodayProgressAsync(today, enabledItems);

                // Calculate week progress
                await CalculateWeekProgressAsync(today, enabledItems);

                // Calculate month progress
                await CalculateMonthProgressAsync(today, enabledItems);

                // Calculate streaks
                await CalculateStreaksAsync();

                // Calculate per-item stats
                await CalculateItemStatsAsync(today, enabledItems);

                // Calculate weekly chart data
                await CalculateWeeklyChartAsync(today, enabledItems);
            }

            // Load weight data if enabled
            if (WeightTrackingEnabled)
            {
                await LoadWeightDataAsync(today);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CalculateTodayProgressAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        var entries = await _dataService.GetEntriesForDateAsync(today);
        var (completed, total) = CalculateProgress(entries, enabledItems);

        TodayProgress = total > 0 ? (double)completed / total : 0;
        TodayProgressText = $"{(int)(TodayProgress * 100)}%";
    }

    private async Task CalculateWeekProgressAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var entries = await _dataService.GetEntriesInRangeAsync(weekStart, today);

        var totalCompleted = 0;
        var totalPossible = 0;
        var daysCount = (today.DayNumber - weekStart.DayNumber) + 1;

        for (var date = weekStart; date <= today; date = date.AddDays(1))
        {
            var dayEntries = entries.Where(e => e.Date == date).ToList();
            var (completed, total) = CalculateProgress(dayEntries, enabledItems);
            totalCompleted += completed;
            totalPossible += total;
        }

        WeekProgress = totalPossible > 0 ? (double)totalCompleted / totalPossible : 0;
        WeekProgressText = $"{(int)(WeekProgress * 100)}%";
    }

    private async Task CalculateMonthProgressAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var entries = await _dataService.GetEntriesInRangeAsync(monthStart, today);

        var totalCompleted = 0;
        var totalPossible = 0;

        for (var date = monthStart; date <= today; date = date.AddDays(1))
        {
            var dayEntries = entries.Where(e => e.Date == date).ToList();
            var (completed, total) = CalculateProgress(dayEntries, enabledItems);
            totalCompleted += completed;
            totalPossible += total;
        }

        MonthProgress = totalPossible > 0 ? (double)totalCompleted / totalPossible : 0;
        MonthProgressText = $"{(int)(MonthProgress * 100)}%";
    }

    private async Task CalculateStreaksAsync()
    {
        CurrentStreak = await _dataService.GetCurrentStreakAsync();
        LongestStreak = await _dataService.GetLongestStreakAsync();

        CurrentStreakText = CurrentStreak == 1 ? "1 day" : $"{CurrentStreak} days";
        LongestStreakText = LongestStreak == 1 ? "1 day" : $"{LongestStreak} days";
    }

    private async Task CalculateItemStatsAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        ItemStats.Clear();

        // Get last 30 days of data
        var startDate = today.AddDays(-29);
        var entries = await _dataService.GetEntriesInRangeAsync(startDate, today);

        foreach (var item in enabledItems.Take(10)) // Show top 10 items
        {
            var itemEntries = entries.Where(e => e.ItemId == item.Id).ToList();
            var daysCompleted = 0;
            var totalDays = 30;

            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                var entry = itemEntries.FirstOrDefault(e => e.Date == date);
                if (entry != null && entry.ServingsCompleted >= item.RecommendedServings)
                {
                    daysCompleted++;
                }
            }

            var completionRate = (double)daysCompleted / totalDays;

            ItemStats.Add(new ItemStatViewModel
            {
                ItemName = item.Name,
                CompletionRate = completionRate,
                CompletionText = $"{(int)(completionRate * 100)}%",
                DaysCompleted = daysCompleted,
                TotalDays = totalDays
            });
        }
    }

    private async Task CalculateWeeklyChartAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        WeeklyProgress.Clear();

        // Get last 7 days
        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var entries = await _dataService.GetEntriesForDateAsync(date);
            var (completed, total) = CalculateProgress(entries, enabledItems);
            var progress = total > 0 ? (double)completed / total : 0;

            WeeklyProgress.Add(new DayProgressViewModel
            {
                Date = date,
                DayName = date == today ? "Today" : date.ToString("ddd"),
                Progress = progress,
                ProgressText = $"{(int)(progress * 100)}%",
                IsToday = date == today
            });
        }
    }

    private static (int completed, int total) CalculateProgress(IEnumerable<DailyEntry> entries, List<ChecklistItem> enabledItems)
    {
        var totalServings = enabledItems.Sum(i => i.RecommendedServings);
        var completedServings = 0;

        foreach (var item in enabledItems)
        {
            var entry = entries.FirstOrDefault(e => e.ItemId == item.Id);
            if (entry != null)
            {
                completedServings += Math.Min(entry.ServingsCompleted, item.RecommendedServings);
            }
        }

        return (completedServings, totalServings);
    }

    private static List<ChecklistItem> GetEnabledItems(IAppPreferences prefs)
    {
        var items = new List<ChecklistItem>();

        if (prefs.DailyDozenEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen));
        }

        if (prefs.TwentyOneTweaksEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks));
        }

        if (prefs.AntiAgingEightEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.AntiAgingEight));
        }

        // Remove duplicates (smart merge)
        return items.GroupBy(i => i.Id).Select(g => g.First()).ToList();
    }

    // ===== Weight Tracking Methods =====

    private async Task LoadWeightDataAsync(DateOnly today)
    {
        // Load today's weight
        var todayEntry = await _dataService.GetWeightEntryAsync(today);
        if (todayEntry != null)
        {
            TodayWeight = todayEntry.Weight;
            WeightInputText = todayEntry.Weight.ToString("F1");
            TodayWeightText = FormatWeight(todayEntry.Weight);
        }
        else
        {
            TodayWeight = null;
            WeightInputText = "";
            TodayWeightText = "No entry";
        }

        // Format goal weight
        if (GoalWeight.HasValue)
        {
            GoalWeightText = FormatWeight(GoalWeight.Value);
        }
        else
        {
            GoalWeightText = "Not set";
        }

        // Load weight history (last 30 days)
        var startDate = today.AddDays(-29);
        var entries = await _dataService.GetWeightEntriesInRangeAsync(startDate, today);

        WeightHistory.Clear();
        foreach (var entry in entries)
        {
            WeightHistory.Add(new WeightDataPoint
            {
                Date = entry.Date,
                Weight = entry.Weight,
                DateText = entry.Date.ToString("M/d"),
                WeightText = FormatWeight(entry.Weight)
            });
        }

        // Calculate weight change
        CalculateWeightChange(entries);

        // Calculate BMI if height is set
        CalculateBmi();
    }

    private void CalculateWeightChange(IReadOnlyList<WeightEntry> entries)
    {
        if (entries.Count < 2)
        {
            WeightChangeText = "";
            return;
        }

        var oldest = entries.First();
        var newest = entries.Last();
        var change = newest.Weight - oldest.Weight;
        var unit = WeightUnit;

        if (Math.Abs(change) < 0.1)
        {
            WeightChangeText = "No change";
        }
        else if (change > 0)
        {
            WeightChangeText = $"+{change:F1} {unit}";
        }
        else
        {
            WeightChangeText = $"{change:F1} {unit}";
        }
    }

    private void CalculateBmi()
    {
        if (!TodayWeight.HasValue || !HeightCm.HasValue || HeightCm.Value <= 0)
        {
            BmiText = "";
            return;
        }

        // Convert weight to kg if in imperial
        var weightKg = UseMetricUnits ? TodayWeight.Value : TodayWeight.Value * 0.453592;
        var heightM = HeightCm.Value / 100.0;
        var bmi = weightKg / (heightM * heightM);

        var category = bmi switch
        {
            < 18.5 => "Underweight",
            < 25 => "Normal",
            < 30 => "Overweight",
            _ => "Obese"
        };

        BmiText = $"BMI: {bmi:F1} ({category})";
    }

    private string FormatWeight(double weight)
    {
        return $"{weight:F1} {WeightUnit}";
    }

    [RelayCommand]
    private async Task SaveTodayWeightAsync()
    {
        if (string.IsNullOrWhiteSpace(WeightInputText))
        {
            return;
        }

        if (!double.TryParse(WeightInputText, out var weight) || weight <= 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var entry = new WeightEntry
        {
            Date = today,
            Weight = weight
        };

        await _dataService.SaveWeightEntryAsync(entry);

        // Reload weight data
        await LoadWeightDataAsync(today);
    }

    [RelayCommand]
    private async Task DeleteTodayWeightAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await _dataService.DeleteWeightEntryAsync(today);

        // Reload weight data
        await LoadWeightDataAsync(today);
    }
}

/// <summary>
/// ViewModel for per-item statistics.
/// </summary>
public class ItemStatViewModel
{
    public string ItemName { get; set; } = string.Empty;
    public double CompletionRate { get; set; }
    public string CompletionText { get; set; } = "0%";
    public int DaysCompleted { get; set; }
    public int TotalDays { get; set; }
}

/// <summary>
/// ViewModel for daily progress in the weekly chart.
/// </summary>
public class DayProgressViewModel
{
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string ProgressText { get; set; } = "0%";
    public bool IsToday { get; set; }
}

/// <summary>
/// Data point for weight chart.
/// </summary>
public class WeightDataPoint
{
    public DateOnly Date { get; set; }
    public double Weight { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string WeightText { get; set; } = string.Empty;
}
