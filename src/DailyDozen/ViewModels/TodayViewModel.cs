using DailyDozen.Models;
using DailyDozen.Services;

namespace DailyDozen.ViewModels;

/// <summary>
/// ViewModel for the Today page, managing date navigation and checklist items.
/// </summary>
public partial class TodayViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IAchievementService? _achievementService;
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string _dateDisplayText = string.Empty;

    [ObservableProperty]
    private bool _canGoToNextDay;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public ObservableCollection<ChecklistItemViewModel> Items { get; } = [];

    public DateOnly CurrentDate => _currentDate;

    public event EventHandler<ChecklistItem>? ItemDetailRequested;

    public TodayViewModel(IDataService dataService, IAchievementService? achievementService = null)
    {
        _dataService = dataService;
        _achievementService = achievementService;
        UpdateDateDisplay();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            var settings = await _dataService.GetSettingsAsync();
            var entries = await _dataService.GetEntriesForDateAsync(_currentDate);

            // Get all enabled checklist items
            var enabledItems = new List<ChecklistItem>();

            if (settings.DailyDozenEnabled)
            {
                enabledItems.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.DailyDozen));
            }

            if (settings.TwentyOneTweaksEnabled)
            {
                enabledItems.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.TwentyOneTweaks));
            }

            if (settings.AntiAgingEightEnabled)
            {
                enabledItems.AddRange(ChecklistDefinitions.GetItemsForChecklist(ChecklistType.AntiAgingEight));
            }

            // Remove duplicates (smart merge) - keep first occurrence
            enabledItems = enabledItems
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .ToList();

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

    private void UpdateDateDisplay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (_currentDate == today)
        {
            DateDisplayText = $"{_currentDate:MMMM d, yyyy} (Today)";
        }
        else if (_currentDate == today.AddDays(-1))
        {
            DateDisplayText = $"{_currentDate:MMMM d, yyyy} (Yesterday)";
        }
        else
        {
            DateDisplayText = _currentDate.ToString("MMMM d, yyyy");
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

            // Check for new achievements
            if (_achievementService != null)
            {
                await _achievementService.CheckAndAwardAchievementsAsync();
            }
        }
    }

    private void OnItemDetailRequested(object? sender, ChecklistItem item)
    {
        ItemDetailRequested?.Invoke(this, item);
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

    [RelayCommand]
    private void IncrementServing()
    {
        if (ServingsCompleted < Item.RecommendedServings)
        {
            ServingsCompleted++;
            OnPropertyChanged(nameof(ServingsDisplayText));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(IsComplete));
            ServingsChanged?.Invoke(this, ServingsCompleted);
        }
    }

    [RelayCommand]
    private void DecrementServing()
    {
        if (ServingsCompleted > 0)
        {
            ServingsCompleted--;
            OnPropertyChanged(nameof(ServingsDisplayText));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(IsComplete));
            ServingsChanged?.Invoke(this, ServingsCompleted);
        }
    }

    [RelayCommand]
    private void ToggleServing()
    {
        // For single-serving items, toggle between 0 and 1
        // For multi-serving items, increment (wrapping to 0 after max)
        if (Item.RecommendedServings == 1)
        {
            ServingsCompleted = ServingsCompleted == 0 ? 1 : 0;
        }
        else
        {
            ServingsCompleted = (ServingsCompleted + 1) % (Item.RecommendedServings + 1);
        }

        OnPropertyChanged(nameof(ServingsDisplayText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsComplete));
        ServingsChanged?.Invoke(this, ServingsCompleted);
    }

    [RelayCommand]
    private void ShowItemDetail()
    {
        ItemDetailRequested?.Invoke(this, Item);
    }
}
