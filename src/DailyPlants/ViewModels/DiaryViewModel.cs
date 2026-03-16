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

            // Get all enabled checklist items
            var enabledItems = ChecklistDefinitions.GetEnabledItems(_appPreferences);

            // Unsubscribe handlers from old items before clearing to prevent memory leaks
            foreach (var old in Items)
            {
                old.ServingsChanged -= OnItemServingsChanged;
                old.ItemDetailRequested -= OnItemDetailRequested;
            }

            // Create view models for each item
            Items.Clear();
            foreach (var item in enabledItems)
            {
                var entry = entries.FirstOrDefault(e => e.ItemId == item.Id);
                var itemVm = new ChecklistItemViewModel(item, _currentDate, entry?.ServingsCompleted ?? 0);
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
            RelativeDayText = "Today";
            ShowRelativeDay = true;
            ShowGoToToday = false;
        }
        else if (_currentDate == today.AddDays(-1))
        {
            RelativeDayText = "Yesterday";
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
        if (sender is ChecklistItemViewModel itemVm)
        {
            // Save to database
            var entry = new DailyEntry
            {
                Date = _currentDate,
                ItemId = itemVm.Item.Id,
                ServingsCompleted = newServings
            };

            await _dataService.SaveEntryAsync(entry);
            UpdateProgress();

            // Debounce achievement check to avoid running on every tap
            ScheduleAchievementCheck();
        }
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
            ProgressText = "No items enabled";
            return;
        }

        var totalServings = Items.Sum(i => i.Item.RecommendedServings);
        var completedServings = Items.Sum(i => Math.Min(i.ServingsCompleted, i.Item.RecommendedServings));

        OverallProgress = totalServings > 0 ? (double)completedServings / totalServings : 0;
        var percentage = (int)(OverallProgress * 100);
        ProgressText = $"{percentage}% complete";
    }
}

/// <summary>
/// ViewModel for a single checklist item.
/// </summary>
public partial class ChecklistItemViewModel : ObservableObject
{
    public ChecklistItem Item { get; }
    public DateOnly Date { get; }

    [ObservableProperty]
    private int _servingsCompleted;

    public event EventHandler<int>? ServingsChanged;
    public event EventHandler<ChecklistItem>? ItemDetailRequested;

    public ChecklistItemViewModel(ChecklistItem item, DateOnly date, int servingsCompleted)
    {
        Item = item;
        Date = date;
        _servingsCompleted = servingsCompleted;
    }

    public bool IsComplete => ServingsCompleted >= Item.RecommendedServings;

    public string ServingsDisplayText => $"{ServingsCompleted}/{Item.RecommendedServings}";

    public double Progress => Item.RecommendedServings > 0
        ? Math.Min(1.0, (double)ServingsCompleted / Item.RecommendedServings)
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
        if (ServingsCompleted < Item.RecommendedServings)
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
        if (ServingsCompleted < Item.RecommendedServings)
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
