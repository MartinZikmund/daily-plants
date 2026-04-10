using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;

namespace DailyPlants.ViewModels;

/// <summary>
/// ViewModel for the Diary page, managing date navigation and checklist items.
/// </summary>
public partial class DiaryViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IAppPreferences _appPreferences;
    private readonly IAchievementService? _achievementService;
    private CancellationTokenSource? _achievementDebounce;
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string _dateDisplayText = string.Empty;

    [ObservableProperty]
    private string _relativeDayText = string.Empty;

    [ObservableProperty]
    private bool _showRelativeDay;

    [ObservableProperty]
    private bool _showGoToToday;

    [ObservableProperty]
    private bool _canGoToNextDay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public ObservableCollection<ChecklistItemViewModel> Items { get; } = [];

    public bool ShowEmptyState => !IsLoading && Items.Count == 0;

    public DateOnly CurrentDate => _currentDate;

    /// <summary>
    /// Maximum selectable date for the calendar picker (today).
    /// </summary>
    public DateTimeOffset MaxSelectableDate => DateTimeOffset.Now;

    public event EventHandler<ChecklistItemViewModel>? ItemDetailRequested;

    public DiaryViewModel(IDataService dataService, IAppPreferences appPreferences, IAchievementService? achievementService = null)
    {
        _dataService = dataService;
        _appPreferences = appPreferences;
        _achievementService = achievementService;
        UpdateDateDisplay();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            var entries = await _dataService.GetEntriesForDateAsync(_currentDate);

            // Get all enabled checklist items (sorted by SortOrder)
            var enabledItems = ChecklistDefinitions.GetEnabledItems(_appPreferences);

            // Compute active merges (only when both parent and child are enabled)
            var enabledIds = enabledItems.Select(i => i.Id).ToHashSet();
            var activeMerges = MergeRules.GetActiveMerges(enabledIds);
            var childIds = activeMerges.Select(m => m.ChildId).ToHashSet();
            var parentToChildren = activeMerges
                .GroupBy(m => m.ParentId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ChecklistItem>)g
                        .Select(m => enabledItems.First(i => i.Id == m.ChildId))
                        .ToList());

            // Unsubscribe handlers from old items before clearing to prevent memory leaks
            foreach (var old in Items)
            {
                old.ServingsChanged -= OnItemServingsChanged;
                old.ItemDetailRequested -= OnItemDetailRequested;
            }

            // Create view models, skipping child items (they're absorbed into their parent)
            Items.Clear();
            foreach (var item in enabledItems)
            {
                if (childIds.Contains(item.Id))
                    continue;

                var parentServings = entries.FirstOrDefault(e => e.ItemId == item.Id)?.ServingsCompleted ?? 0;
                List<ChecklistItem>? children = null;

                if (parentToChildren.TryGetValue(item.Id, out var mergedChildren))
                {
                    children = [.. mergedChildren];
                    parentServings += mergedChildren.Sum(c =>
                        entries.FirstOrDefault(e => e.ItemId == c.Id)?.ServingsCompleted ?? 0);
                }

                var itemVm = new ChecklistItemViewModel(item, _currentDate, parentServings, _appPreferences.UseMetricUnits, children);
                itemVm.ServingsChanged += OnItemServingsChanged;
                itemVm.ItemDetailRequested += OnItemDetailRequested;
                Items.Add(itemVm);
            }

            UpdateProgress();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private async Task GoToPreviousDayAsync()
    {
        _currentDate = _currentDate.AddDays(-1);
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToNextDayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_currentDate < today)
        {
            _currentDate = _currentDate.AddDays(1);
            UpdateDateDisplay();
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        _currentDate = DateOnly.FromDateTime(DateTime.Today);
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    /// <summary>
    /// Navigate to a specific date (called from calendar picker).
    /// </summary>
    public async Task GoToDateAsync(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Don't allow future dates
        if (date > today)
        {
            date = today;
        }

        _currentDate = date;
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    private void UpdateDateDisplay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        DateDisplayText = _currentDate.ToString("MMMM d, yyyy");

        if (_currentDate == today)
        {
            RelativeDayText = Localizer.GetString("Diary_Today");
            ShowRelativeDay = true;
            ShowGoToToday = false;
        }
        else if (_currentDate == today.AddDays(-1))
        {
            RelativeDayText = Localizer.GetString("Diary_Yesterday");
            ShowRelativeDay = true;
            ShowGoToToday = false;
        }
        else
        {
            RelativeDayText = string.Empty;
            ShowRelativeDay = false;
            ShowGoToToday = true;
        }

        CanGoToNextDay = _currentDate < today;
    }

    private async void OnItemServingsChanged(object? sender, int newServings)
    {
        if (sender is not ChecklistItemViewModel itemVm)
            return;

        if (!itemVm.HasMergedChildren)
        {
            // Simple case: no merge, save directly
            await _dataService.SaveEntryAsync(new DailyEntry
            {
                Date = _currentDate,
                ItemId = itemVm.Item.Id,
                ServingsCompleted = newServings
            });
        }
        else
        {
            // Distribute across parent and children: parent fills first
            var remaining = newServings;

            var parentServings = Math.Min(remaining, itemVm.Item.RecommendedServings);
            remaining -= parentServings;

            await _dataService.SaveEntryAsync(new DailyEntry
            {
                Date = _currentDate,
                ItemId = itemVm.Item.Id,
                ServingsCompleted = parentServings
            });

            foreach (var child in itemVm.MergedChildren)
            {
                var childServings = Math.Min(remaining, child.RecommendedServings);
                remaining -= childServings;

                await _dataService.SaveEntryAsync(new DailyEntry
                {
                    Date = _currentDate,
                    ItemId = child.Id,
                    ServingsCompleted = childServings
                });
            }
        }

        UpdateProgress();

        // Debounce achievement check to avoid running on every tap
        ScheduleAchievementCheck();
    }

    private void OnItemDetailRequested(object? sender, ChecklistItem item)
    {
        if (sender is ChecklistItemViewModel itemVm)
        {
            ItemDetailRequested?.Invoke(this, itemVm);
        }
    }

    private async void ScheduleAchievementCheck()
    {
        if (_achievementService == null) return;

        _achievementDebounce?.Cancel();
        _achievementDebounce = new CancellationTokenSource();
        var token = _achievementDebounce.Token;

        try
        {
            await Task.Delay(2000, token);
            if (!token.IsCancellationRequested)
            {
                await _achievementService.CheckAndAwardAchievementsAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled — expected
        }
    }

    private void UpdateProgress()
    {
        if (Items.Count == 0)
        {
            OverallProgress = 0;
            ProgressText = Localizer.GetString("Diary_NoItemsEnabled");
            return;
        }

        var totalServings = Items.Sum(i => i.TotalRecommendedServings);
        var completedServings = Items.Sum(i => Math.Min(i.ServingsCompleted, i.TotalRecommendedServings));

        OverallProgress = totalServings > 0 ? (double)completedServings / totalServings : 0;
        var percentage = (int)(OverallProgress * 100);
        ProgressText = string.Format(Localizer.GetString("Diary_PercentComplete"), percentage);
    }
}

/// <summary>
/// ViewModel for a single checklist item.
/// Supports merging child items (e.g., "More Legumes" into "Beans") when
/// both parent and child checklists are enabled.
/// </summary>
public partial class ChecklistItemViewModel : ObservableObject
{
    private readonly bool _useMetricUnits;

    public ChecklistItem Item { get; }
    public DateOnly Date { get; }

    /// <summary>
    /// Child items merged into this parent item (empty if no merge is active).
    /// </summary>
    public IReadOnlyList<ChecklistItem> MergedChildren { get; }

    /// <summary>
    /// The effective recommended servings (base + merged children).
    /// </summary>
    public int TotalRecommendedServings { get; }

    public bool HasMergedChildren => MergedChildren.Count > 0;

    [ObservableProperty]
    private int _servingsCompleted;

    public event EventHandler<int>? ServingsChanged;
    public event EventHandler<ChecklistItem>? ItemDetailRequested;

    public ChecklistItemViewModel(ChecklistItem item, DateOnly date, int servingsCompleted, bool useMetricUnits, IReadOnlyList<ChecklistItem>? mergedChildren = null)
    {
        _useMetricUnits = useMetricUnits;
        Item = item;
        Date = date;
        MergedChildren = mergedChildren ?? [];
        TotalRecommendedServings = item.RecommendedServings + MergedChildren.Sum(c => c.RecommendedServings);
        _servingsCompleted = servingsCompleted;
    }

    /// <summary>
    /// The serving size description appropriate for the current unit system.
    /// </summary>
    public string ServingSizeDisplay => _useMetricUnits
        ? Item.ServingSizeMetric
        : Item.ServingSizeImperial;

    /// <summary>
    /// Gets the serving size display text for a given item using the current unit system.
    /// Used for merged children display.
    /// </summary>
    public string GetServingSizeDisplay(ChecklistItem item) => _useMetricUnits
        ? item.ServingSizeMetric
        : item.ServingSizeImperial;

    public bool IsComplete => ServingsCompleted >= TotalRecommendedServings;

    public string ServingsDisplayText => $"{ServingsCompleted}/{TotalRecommendedServings}";

    public double Progress => TotalRecommendedServings > 0
        ? Math.Min(1.0, (double)ServingsCompleted / TotalRecommendedServings)
        : 0;

    partial void OnServingsCompletedChanged(int value)
    {
        OnPropertyChanged(nameof(ServingsDisplayText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsComplete));
        ServingsChanged?.Invoke(this, value);
    }

    [RelayCommand]
    private void IncrementServing()
    {
        if (ServingsCompleted < TotalRecommendedServings)
        {
            ServingsCompleted++;
        }
    }

    [RelayCommand]
    private void DecrementServing()
    {
        if (ServingsCompleted > 0)
        {
            ServingsCompleted--;
        }
    }

    [RelayCommand]
    private void ToggleServing()
    {
        // Increment up to max value only (no wrapping)
        // To decrease, user must use the minus button
        if (ServingsCompleted < TotalRecommendedServings)
        {
            ServingsCompleted++;
        }
    }

    [RelayCommand]
    private void ShowItemDetail()
    {
        ItemDetailRequested?.Invoke(this, Item);
    }
}
