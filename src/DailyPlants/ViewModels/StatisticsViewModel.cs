using System.Globalization;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Statistics page: four panels, each backed by real data
/// from <see cref="IDataService"/> - daily completion, streaks, what gets
/// missed most, and (optionally) weight trend.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IAppPreferences _appPreferences;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasAnyData = true;

    /// <summary>Gates panels 1-3, which need at least one enabled checklist item.</summary>
    [ObservableProperty]
    private bool _hasChecklistData;

    // ===== Panel 1: Daily completion (last 30 days) =====

    public ObservableCollection<DailyCompletionPoint> DailyCompletion { get; } = [];

    [ObservableProperty]
    private string _dailyFirstDateLabel = "";

    [ObservableProperty]
    private string _dailyMiddleDateLabel = "";

    [ObservableProperty]
    private string _dailyTodayLabel = "";

    [ObservableProperty]
    private string _dailyCompletionAutomationName = "";

    // ===== Panel 2: Streaks =====

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private int _longestStreak;

    [ObservableProperty]
    private string _currentStreakText = "0 days";

    [ObservableProperty]
    private string _longestStreakText = "0 days";

    [ObservableProperty]
    private string _currentStreakCaption = "";

    [ObservableProperty]
    private string _longestStreakCaption = "";

    // ===== Panel 3: What you miss most (last 30 days, worst first) =====

    public ObservableCollection<ItemMissViewModel> MissedItems { get; } = [];

    // ===== Panel 4: Weight trend =====

    [ObservableProperty]
    private bool _weightTrackingEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightUnit))]
    private bool _useMetricUnits;

    [ObservableProperty]
    private string _weightInputText = "";

    [ObservableProperty]
    private double? _todayWeight;

    /// <summary>The weight loaded for today, in kilograms, before any display rounding.</summary>
    private double? _todayWeightKg;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoalWeightForChart))]
    private double? _goalWeight;

    [ObservableProperty]
    private string _weightChangeText = "";

    [ObservableProperty]
    private IReadOnlyList<WeightDataPoint> _weightHistory = [];

    [ObservableProperty]
    private string _weightFirstDateLabel = "";

    [ObservableProperty]
    private string _weightLastDateLabel = "";

    [ObservableProperty]
    private string _weightLastDateWithChangeLabel = "";

    [ObservableProperty]
    private string _weightChartAutomationName = "";

    public string WeightUnit => UseMetricUnits ? "kg" : "lb";

    /// <summary>NaN when no goal is set - the chart control treats NaN as "no goal line".</summary>
    public double GoalWeightForChart => GoalWeight ?? double.NaN;

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
            var enabledItems = ChecklistDefinitions.GetEnabledItems(_appPreferences);
            var today = DateOnly.FromDateTime(DateTime.Today);

            WeightTrackingEnabled = _appPreferences.WeightTrackingEnabled;
            UseMetricUnits = _appPreferences.UseMetricUnits;
            GoalWeight = _appPreferences.GoalWeight is { } goalKg ? ToDisplayWeight(goalKg) : null;

            HasChecklistData = enabledItems.Count > 0;
            HasAnyData = HasChecklistData || WeightTrackingEnabled;

            if (HasChecklistData)
            {
                await CalculateDailyCompletionAsync(today, enabledItems);
                await CalculateStreaksAsync(today, enabledItems);
                await CalculateMissedItemsAsync(today, enabledItems);
            }

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

    private async Task CalculateDailyCompletionAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        DailyCompletion.Clear();

        var startDate = today.AddDays(-29);
        var entriesByDate = await GetEntriesByDateAsync(startDate, today);

        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            var (completed, total) = CalculateProgress(entriesByDate.GetValueOrDefault(date, []), enabledItems);
            var share = total > 0 ? (double)completed / total : 0;
            DailyCompletion.Add(new DailyCompletionPoint(date, share, date == today));
        }

        DailyFirstDateLabel = startDate.ToString("MMM d", CultureInfo.CurrentCulture);
        DailyMiddleDateLabel = startDate.AddDays(14).ToString("MMM d", CultureInfo.CurrentCulture);

        var todayShare = DailyCompletion.Count > 0 ? DailyCompletion[^1].Share : 0;
        DailyTodayLabel = $"Today · {(int)Math.Round(todayShare * 100)}%";
        DailyCompletionAutomationName = $"Daily completion, last 30 days. {DailyTodayLabel}.";
    }

    private async Task CalculateStreaksAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        CurrentStreak = await _dataService.GetCurrentStreakAsync();
        LongestStreak = await _dataService.GetLongestStreakAsync();

        CurrentStreakText = CurrentStreak == 1 ? "1 day" : $"{CurrentStreak} days";
        LongestStreakText = LongestStreak == 1 ? "1 day" : $"{LongestStreak} days";

        if (CurrentStreak == 0)
        {
            CurrentStreakCaption = "Get today to 100% to start one.";
        }
        else
        {
            // The streak count alone doesn't say where it started - work that
            // out from whether today itself is already complete.
            var todayEntries = await _dataService.GetEntriesForDateAsync(today);
            var endDate = IsDayComplete(todayEntries, enabledItems) ? today : today.AddDays(-1);
            var startDate = endDate.AddDays(-(CurrentStreak - 1));
            CurrentStreakCaption = $"Since {startDate:MMM d}.";
        }

        if (LongestStreak == 0)
        {
            LongestStreakCaption = "No streak yet.";
        }
        else
        {
            var (start, end) = await FindLongestStreakRangeAsync(enabledItems, today);
            LongestStreakCaption = start.HasValue && end.HasValue
                ? $"{start:MMM d}–{end:MMM d}."
                : "Your best run.";
        }
    }

    /// <summary>
    /// Re-walks the whole tracked history to find the date range of the
    /// longest streak - the streak count alone doesn't carry it.
    /// </summary>
    private async Task<(DateOnly? Start, DateOnly? End)> FindLongestStreakRangeAsync(List<ChecklistItem> enabledItems, DateOnly today)
    {
        var trackedDates = await _dataService.GetDatesWithEntriesAsync();
        if (trackedDates.Count == 0)
        {
            return (null, null);
        }

        var earliest = trackedDates.Min();
        var entriesByDate = await GetEntriesByDateAsync(earliest, today);

        DateOnly? bestStart = null;
        DateOnly? bestEnd = null;
        DateOnly? runStart = null;
        var bestLength = 0;
        var runLength = 0;

        for (var date = earliest; date <= today; date = date.AddDays(1))
        {
            if (IsDayComplete(entriesByDate.GetValueOrDefault(date, []), enabledItems))
            {
                runStart ??= date;
                runLength++;

                if (runLength > bestLength)
                {
                    bestLength = runLength;
                    bestStart = runStart;
                    bestEnd = date;
                }
            }
            else
            {
                runLength = 0;
                runStart = null;
            }
        }

        return (bestStart, bestEnd);
    }

    private async Task CalculateMissedItemsAsync(DateOnly today, List<ChecklistItem> enabledItems)
    {
        MissedItems.Clear();

        var startDate = today.AddDays(-29);
        var entries = await _dataService.GetEntriesInRangeAsync(startDate, today);

        var rates = new List<(ChecklistItem Item, double Rate)>();
        foreach (var item in enabledItems)
        {
            var itemEntries = entries.Where(e => e.ItemId == item.Id).ToList();
            var daysCompleted = 0;
            var totalDays = 0;

            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                totalDays++;
                var entry = itemEntries.FirstOrDefault(e => e.Date == date);
                if (entry != null && entry.ServingsCompleted >= item.RecommendedServings)
                {
                    daysCompleted++;
                }
            }

            rates.Add((item, totalDays > 0 ? (double)daysCompleted / totalDays : 0));
        }

        foreach (var (item, rate) in rates.OrderBy(r => r.Rate))
        {
            MissedItems.Add(new ItemMissViewModel(item.Name, rate));
        }
    }

    private async Task LoadWeightDataAsync(DateOnly today)
    {
        var todayEntry = await _dataService.GetWeightEntryAsync(today);
        _todayWeightKg = todayEntry?.Weight;
        TodayWeight = todayEntry is not null ? ToDisplayWeight(todayEntry.Weight) : null;
        WeightInputText = TodayWeight?.ToString("F1") ?? "";

        var startDate = today.AddDays(-29);
        var entries = await _dataService.GetWeightEntriesInRangeAsync(startDate, today);

        WeightHistory = entries.Select(e => new WeightDataPoint(e.Date, ToDisplayWeight(e.Weight))).ToList();

        if (WeightHistory.Count > 0)
        {
            WeightFirstDateLabel = WeightHistory[0].Date.ToString("MMM d", CultureInfo.CurrentCulture);
            WeightLastDateLabel = WeightHistory[^1].Date.ToString("MMM d", CultureInfo.CurrentCulture);
        }
        else
        {
            WeightFirstDateLabel = "";
            WeightLastDateLabel = "";
        }

        CalculateWeightChange(entries);

        WeightLastDateWithChangeLabel = WeightChangeText.Length > 0
            ? $"{WeightLastDateLabel} · {WeightChangeText}"
            : WeightLastDateLabel;

        WeightChartAutomationName = WeightHistory.Count > 0
            ? $"Weight trend, {WeightFirstDateLabel} to {WeightLastDateLabel}. Latest {WeightHistory[^1].Weight:F1} {WeightUnit}. {WeightChangeText}"
            : "Weight trend. No entries yet.";
    }

    private void CalculateWeightChange(IReadOnlyList<WeightEntry> entries)
    {
        if (entries.Count < 2)
        {
            WeightChangeText = "";
            return;
        }

        var change = ToDisplayWeight(entries[^1].Weight) - ToDisplayWeight(entries[0].Weight);
        var unit = WeightUnit;

        WeightChangeText = Math.Abs(change) < 0.1
            ? "No change"
            : change > 0 ? $"+{change:F1} {unit}" : $"{change:F1} {unit}";
    }

    /// <summary>Converts a stored (canonical kilogram) weight into the user's display unit.</summary>
    private double ToDisplayWeight(double weightKg) =>
        UnitConverter.KilogramsToDisplay(weightKg, UseMetricUnits);

    /// <summary>
    /// The kilograms to store for what the input box currently holds. Saving is a button
    /// press rather than a text change, so it also fires when the user only wanted to
    /// confirm what was already there - and the box shows one decimal, so converting that
    /// back would shave 14 g off an 80 kg weight every time. Text that still renders the
    /// loaded weight is not an edit, and the loaded weight is the more precise answer.
    /// </summary>
    private double ResolveWeightToStore(double displayWeight) =>
        _todayWeightKg is { } loaded && ToDisplayWeight(loaded).ToString("F1") == WeightInputText
            ? loaded
            : UnitConverter.DisplayToKilograms(displayWeight, UseMetricUnits);

    [RelayCommand]
    private async Task SaveTodayWeightAsync()
    {
        if (!double.TryParse(WeightInputText, out var weight) || weight <= 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var entry = new WeightEntry
        {
            Date = today,
            Weight = ResolveWeightToStore(weight)
        };

        await _dataService.SaveWeightEntryAsync(entry);
        await LoadWeightDataAsync(today);
    }

    [RelayCommand]
    private async Task DeleteTodayWeightAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await _dataService.DeleteWeightEntryAsync(today);
        await LoadWeightDataAsync(today);
    }

    private async Task<Dictionary<DateOnly, IReadOnlyList<DailyEntry>>> GetEntriesByDateAsync(DateOnly startDate, DateOnly endDate)
    {
        var entries = await _dataService.GetEntriesInRangeAsync(startDate, endDate);
        return entries.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<DailyEntry>)g.ToList());
    }

    private static bool IsDayComplete(IReadOnlyList<DailyEntry> dayEntries, List<ChecklistItem> enabledItems)
    {
        var (completed, total) = CalculateProgress(dayEntries, enabledItems);
        return total > 0 && completed == total;
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
}

/// <summary>One column of the 30-day daily-completion chart.</summary>
public sealed record DailyCompletionPoint(DateOnly Date, double Share, bool IsToday);

/// <summary>One point on the weight trend line.</summary>
public sealed record WeightDataPoint(DateOnly Date, double Weight);

/// <summary>One row of the "what you miss most" panel.</summary>
public sealed class ItemMissViewModel
{
    public ItemMissViewModel(string itemName, double completionRate)
    {
        ItemName = itemName;
        CompletionRate = completionRate;
        CompletionText = $"{(int)Math.Round(completionRate * 100)}%";
        IsBelowHalf = completionRate < 0.5;
    }

    public string ItemName { get; }
    public double CompletionRate { get; }
    public string CompletionText { get; }
    public bool IsBelowHalf { get; }
    public Visibility NormalVisibility => IsBelowHalf ? Visibility.Collapsed : Visibility.Visible;
    public Visibility BelowHalfVisibility => IsBelowHalf ? Visibility.Visible : Visibility.Collapsed;
}
