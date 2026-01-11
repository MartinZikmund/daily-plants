using DailyDozen.Models;
using DailyDozen.Services;

namespace DailyDozen.ViewModels;

/// <summary>
/// ViewModel for the Statistics page.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IDataService _dataService;

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

    public StatisticsViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadStatisticsAsync()
    {
        IsLoading = true;

        try
        {
            var settings = await _dataService.GetSettingsAsync();
            var enabledItems = GetEnabledItems(settings);

            if (enabledItems.Count == 0)
            {
                // No checklists enabled
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

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

    private static List<ChecklistItem> GetEnabledItems(UserSettings settings)
    {
        var items = new List<ChecklistItem>();

        if (settings.DailyDozenEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen));
        }

        if (settings.TwentyOneTweaksEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks));
        }

        if (settings.AntiAgingEightEnabled)
        {
            items.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.AntiAgingEight));
        }

        // Remove duplicates (smart merge)
        return items.GroupBy(i => i.Id).Select(g => g.First()).ToList();
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
