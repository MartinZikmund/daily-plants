using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.Services.Tips;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyPlants.ViewModels;

/// <summary>
/// Payload for <see cref="DiaryViewModel.DayCompleted"/>, consumed by the day-complete parade.
/// </summary>
public sealed record DayCompleteInfo(int TotalServings, string Headline, string Subhead);

/// <summary>
/// ViewModel for the Diary page, managing date navigation and checklist items.
/// </summary>
public partial class DiaryViewModel : ObservableObject
{
    /// <summary>
    /// Windowsill motion spec: a completed row holds its place before it moves groups.
    /// </summary>
    private static readonly TimeSpan GroupMoveHold = TimeSpan.FromMilliseconds(400);

    private readonly IDataService _dataService;
    private readonly IAppPreferences _appPreferences;
    private readonly IAchievementService? _achievementService;
    private readonly ITipService? _tipService;

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _achievementDebounce;

    /// <summary>Last count successfully written, per item, so a failed save can roll back to it.</summary>
    private readonly Dictionary<string, int> _lastSavedServings = [];

    /// <summary>Guards the rollback assignment from re-entering the change handler.</summary>
    private bool _isRevertingServings;
    private DateOnly _currentDate;
    private bool _dayCompleteAnnounced;

    /// <summary>
    /// True while the view is tracking "today" rather than a date the user picked.
    /// Only then may a date rollover move the view.
    /// </summary>
    private bool _followToday = true;

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

    /// <summary>
    /// Relative day and streak on one caption line, e.g. "Today · 12-day streak".
    /// </summary>
    [ObservableProperty]
    private string _dayContextText = string.Empty;

    [ObservableProperty]
    private bool _showDayContext;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowTally))]
    private bool _isLoading;

    [ObservableProperty]
    private double _overallProgress;

    /// <summary>
    /// The tally headline, e.g. "14 of 21".
    /// </summary>
    [ObservableProperty]
    private string _servingsTallyText = string.Empty;

    [ObservableProperty]
    private string _stillToGoCountText = "0";

    [ObservableProperty]
    private string _doneTodayCountText = "0";

    [ObservableProperty]
    private bool _showStillToGoGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDoneTodayRows))]
    private bool _showDoneTodayGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDoneTodayRows))]
    private bool _isDoneTodayExpanded = true;

    /// <summary>
    /// The teaching tip currently on screen, or null when none is. Only ever one at a time.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLogServingTip))]
    [NotifyPropertyChangedFor(nameof(ShowDayProgressTip))]
    [NotifyPropertyChangedFor(nameof(ShowPastDaysTip))]
    private TipId? _activeTip;

    /// <summary>
    /// The full, ordered set of rows for the current day. Groups are derived from it.
    /// </summary>
    public ObservableCollection<ChecklistItemViewModel> Items { get; } = [];

    public ObservableCollection<ChecklistItemViewModel> StillToGo { get; } = [];

    public ObservableCollection<ChecklistItemViewModel> DoneToday { get; } = [];

    /// <summary>
    /// Set to false by the view when the user has reduced motion enabled: rows then
    /// stay put and are regrouped on the next navigation instead of sliding across.
    /// </summary>
    public bool AnimateGroupChanges { get; set; } = true;

    public bool ShowEmptyState => !IsLoading && Items.Count == 0;

    public bool ShowTally => !IsLoading && Items.Count > 0;

    public bool ShowDoneTodayRows => ShowDoneTodayGroup && IsDoneTodayExpanded;

    public bool ShowLogServingTip => ActiveTip == TipId.DiaryLogServing;

    public bool ShowDayProgressTip => ActiveTip == TipId.DiaryDayProgress;

    public bool ShowPastDaysTip => ActiveTip == TipId.DiaryPastDays;

    /// <summary>
    /// Picks the tip to show for this page load, if any. The tour drains before any
    /// contextual tip fires, so a user who quit part way through it is not handed two
    /// unrelated lessons at once.
    /// </summary>
    /// <param name="canPointAtARow">
    /// Whether a checklist row is on screen for the first tour step to point at. The view
    /// knows; the ViewModel cannot see the visual tree.
    /// </param>
    public async Task EvaluateTipsAsync(bool canPointAtARow = true)
    {
        if (_tipService is null || ActiveTip is not null)
        {
            return;
        }

        foreach (var step in TipIdExtensions.TourSteps)
        {
            if (!_tipService.ShouldShow(step))
            {
                continue;
            }

            // With nothing to anchor it to, the tip would strand itself mid-screen. Sitting
            // this load out costs nothing; marking it seen would cost the lesson for good.
            if (step == TipId.DiaryLogServing && !canPointAtARow)
            {
                return;
            }

            ActiveTip = step;
            return;
        }

        if (_tipService.ShouldShow(TipId.DiaryPastDays) && await HasADayWorthGoingBackForAsync())
        {
            ActiveTip = TipId.DiaryPastDays;
        }
    }

    /// <summary>
    /// True when the last thing logged is older than yesterday - the tip is only worth
    /// showing to someone who actually has a day to go back and fill in.
    /// </summary>
    private async Task<bool> HasADayWorthGoingBackForAsync()
    {
        try
        {
            var dates = await _dataService.GetDatesWithEntriesAsync();
            return dates.Count > 0 && dates.Max() < Today.AddDays(-1);
        }
        catch (Exception ex)
        {
            // A tip nobody saw stays unseen, and is retried on the next load.
            _logger.LogWarning(ex, "Could not read entry dates while deciding on the past-days tip");
            return false;
        }
    }

    private static TipId? NextTourStep(TipId current)
    {
        var steps = TipIdExtensions.TourSteps;

        for (var i = 0; i < steps.Count - 1; i++)
        {
            if (steps[i] == current)
            {
                return steps[i + 1];
            }
        }

        return null;
    }

    [RelayCommand]
    private void TipNext()
    {
        if (_tipService is null || ActiveTip is not { } tip)
        {
            return;
        }

        _tipService.MarkSeen(tip);
        ActiveTip = NextTourStep(tip);
    }

    /// <summary>
    /// Ends the tour without touching the contextual tips: Skip means "not now", not
    /// "never tell me anything".
    /// </summary>
    [RelayCommand]
    private void TipSkip()
    {
        if (_tipService is null)
        {
            return;
        }

        _tipService.MarkSeen([.. TipIdExtensions.TourSteps]);
        ActiveTip = null;
    }

    [RelayCommand]
    private void TipDismissed()
    {
        if (_tipService is null || ActiveTip is not { } tip)
        {
            return;
        }

        _tipService.MarkSeen(tip);
        ActiveTip = null;
    }

    public string StillToGoHeaderText => Localizer.GetString("Diary_StillToGo");

    public string DoneTodayHeaderText => Localizer.GetString("Diary_DoneToday");

    public DateOnly CurrentDate => _currentDate;

    /// <summary>
    /// Maximum selectable date for the calendar picker (today).
    /// </summary>
    public DateTimeOffset MaxSelectableDate => _timeProvider.GetLocalNow();

    public event EventHandler<ChecklistItemViewModel>? ItemDetailRequested;

    /// <summary>
    /// Raised once when every serving for the shown day has just been completed.
    /// </summary>
    public event EventHandler<DayCompleteInfo>? DayCompleted;

    /// <summary>
    /// Raised whenever a different day starts loading, so the parade can reset.
    /// </summary>
    public event EventHandler? DayReset;

    /// <summary>
    /// Raised when a serving could not be persisted. The view surfaces this; the count
    /// shown to the user has already been rolled back by the time it fires.
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    public DiaryViewModel(
        IDataService dataService,
        IAppPreferences appPreferences,
        IAchievementService? achievementService = null,
        TimeProvider? timeProvider = null,
        ILogger<DiaryViewModel>? logger = null,
        ITipService? tipService = null)
    {
        _dataService = dataService;
        _appPreferences = appPreferences;
        _achievementService = achievementService;
        _tipService = tipService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DiaryViewModel>.Instance;
        _currentDate = Today;
        UpdateDateDisplay();
    }

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <summary>
    /// Re-synchronises the view with the real date. Called when the page loads, when the
    /// window is activated, and at local midnight, so a session left open overnight stops
    /// writing to yesterday while still calling it "Today".
    /// </summary>
    public async Task RefreshIfDateChangedAsync()
    {
        if (!_followToday) return;

        var today = Today;
        if (_currentDate == today) return;

        _currentDate = today;
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        DayReset?.Invoke(this, EventArgs.Empty);

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

            _lastSavedServings.Clear();

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
                _lastSavedServings[item.Id] = parentServings;
                itemVm.ServingsChanged += OnItemServingsChanged;
                itemVm.ItemDetailRequested += OnItemDetailRequested;
                Items.Add(itemVm);
            }

            RebuildGroups();
            UpdateProgress(announceCompletion: false);
            await LoadStreakAsync();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowTally));
        }
    }

    [RelayCommand]
    private async Task GoToPreviousDayAsync()
    {
        _currentDate = _currentDate.AddDays(-1);
        _followToday = _currentDate == Today;
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToNextDayAsync()
    {
        var today = Today;
        if (_currentDate < today)
        {
            _currentDate = _currentDate.AddDays(1);
            _followToday = _currentDate == today;
            UpdateDateDisplay();
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        _currentDate = Today;
        _followToday = true;
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    [RelayCommand]
    private void ToggleDoneToday() => IsDoneTodayExpanded = !IsDoneTodayExpanded;

    /// <summary>
    /// Navigate to a specific date (called from calendar picker).
    /// </summary>
    public async Task GoToDateAsync(DateOnly date)
    {
        var today = Today;
        // Don't allow future dates
        if (date > today)
        {
            date = today;
        }

        _currentDate = date;
        _followToday = date == today;
        UpdateDateDisplay();
        await LoadDataAsync();
    }

    private void UpdateDateDisplay()
    {
        var today = Today;

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
        _dayCompleteAnnounced = false;
        UpdateDayContext();
    }

    private async Task LoadStreakAsync()
    {
        CurrentStreak = await _dataService.GetCurrentStreakAsync();
        UpdateDayContext();
    }

    private void UpdateDayContext()
    {
        List<string> parts = new();

        if (ShowRelativeDay && !string.IsNullOrEmpty(RelativeDayText))
        {
            parts.Add(RelativeDayText);
        }

        if (CurrentStreak > 0)
        {
            parts.Add(string.Format(
                CultureInfo.CurrentCulture,
                Localizer.GetString("Diary_StreakDays"),
                CurrentStreak));
        }

        DayContextText = string.Join(" · ", parts);
        ShowDayContext = parts.Count > 0;
    }

    private async void OnItemServingsChanged(object? sender, int newServings)
    {
        if (_isRevertingServings)
            return;

        if (sender is not ChecklistItemViewModel itemVm)
            return;

        try
        {
            await SaveServingsAsync(itemVm, newServings);
            _lastSavedServings[itemVm.Item.Id] = newServings;

            UpdateProgress();

            // Runs on its own timeline: the row holds its place before it changes group.
            _ = ScheduleGroupSyncAsync(itemVm);

            // Debounce achievement check to avoid running on every tap
            ScheduleAchievementCheck();
        }
        catch (Exception ex)
        {
            // The most frequent action in the app runs through this handler, and it is
            // async void: an escaping exception would terminate the process with no trail.
            RevertServings(itemVm);
            UpdateProgress();
            SaveFailed?.Invoke(this, ex);
        }
    }

    private void RevertServings(ChecklistItemViewModel itemVm)
    {
        var lastGood = _lastSavedServings.GetValueOrDefault(itemVm.Item.Id, 0);

        _isRevertingServings = true;
        try
        {
            itemVm.ServingsCompleted = lastGood;
        }
        finally
        {
            _isRevertingServings = false;
        }
    }

    private async Task SaveServingsAsync(ChecklistItemViewModel itemVm, int newServings)
    {
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
            // One count is spread over several rows, so a failure between them would leave a
            // total on disk that nobody typed - and the rollback below only puts the display
            // back, not the rows.
            await _dataService.RunInTransactionAsync(async () =>
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
            });
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
        catch (Exception ex)
        {
            // Also async void: an achievement check must never take the app down. It is not
            // a persistence failure either, so it is logged rather than shown to the user.
            _logger.LogError(ex, "Achievement check failed");
        }
    }

    private void UpdateProgress(bool announceCompletion = true)
    {
        if (Items.Count == 0)
        {
            OverallProgress = 0;
            ServingsTallyText = FormatTally(0, 0);
            return;
        }

        var totalServings = Items.Sum(i => i.TotalRecommendedServings);
        var completedServings = Items.Sum(i => Math.Min(i.ServingsCompleted, i.TotalRecommendedServings));

        OverallProgress = totalServings > 0 ? (double)completedServings / totalServings : 0;
        ServingsTallyText = FormatTally(completedServings, totalServings);

        if (totalServings == 0 || completedServings < totalServings || _dayCompleteAnnounced)
        {
            return;
        }

        // Fires at most once per day; a reload of an already-complete day arms the
        // flag without announcing, so the parade never replays.
        _dayCompleteAnnounced = true;

        if (announceCompletion)
        {
            DayCompleted?.Invoke(this, new DayCompleteInfo(
                totalServings,
                Localizer.GetString("Diary_DayCompleteHeadline"),
                string.Format(
                    CultureInfo.CurrentCulture,
                    Localizer.GetString("Diary_DayCompleteSubhead"),
                    totalServings,
                    DateDisplayText)));
        }
    }

    private static string FormatTally(int completed, int total) => string.Format(
        CultureInfo.CurrentCulture,
        Localizer.GetString("Diary_ServingsTally"),
        completed,
        total);

    private void RebuildGroups()
    {
        StillToGo.Clear();
        DoneToday.Clear();

        foreach (var item in Items)
        {
            if (item.IsComplete)
            {
                DoneToday.Add(item);
            }
            else
            {
                StillToGo.Add(item);
            }
        }

        UpdateGroupCounts();
    }

    private async Task ScheduleGroupSyncAsync(ChecklistItemViewModel item)
    {
        if (IsInMatchingGroup(item))
        {
            return;
        }

        if (!AnimateGroupChanges)
        {
            // Reduced motion: leave the row where it is and regroup on next navigation.
            return;
        }

        await Task.Delay(GroupMoveHold);
        MoveToMatchingGroup(item);
    }

    private bool IsInMatchingGroup(ChecklistItemViewModel item) =>
        item.IsComplete ? DoneToday.Contains(item) : StillToGo.Contains(item);

    private void MoveToMatchingGroup(ChecklistItemViewModel item)
    {
        if (!Items.Contains(item) || IsInMatchingGroup(item))
        {
            return;
        }

        var target = item.IsComplete ? DoneToday : StillToGo;
        var source = item.IsComplete ? StillToGo : DoneToday;

        source.Remove(item);
        target.Insert(GroupInsertIndex(target, item), item);
        UpdateGroupCounts();
    }

    /// <summary>
    /// Keeps a group in the same order as <see cref="Items"/>.
    /// </summary>
    private int GroupInsertIndex(ObservableCollection<ChecklistItemViewModel> group, ChecklistItemViewModel item)
    {
        var order = Items.IndexOf(item);

        for (var i = 0; i < group.Count; i++)
        {
            if (Items.IndexOf(group[i]) > order)
            {
                return i;
            }
        }

        return group.Count;
    }

    private void UpdateGroupCounts()
    {
        StillToGoCountText = StillToGo.Count.ToString(CultureInfo.CurrentCulture);
        DoneTodayCountText = DoneToday.Count.ToString(CultureInfo.CurrentCulture);
        ShowStillToGoGroup = StillToGo.Count > 0;
        ShowDoneTodayGroup = DoneToday.Count > 0;
    }
}

/// <summary>
/// One serving dot in a row. Empty dots are outlined; earned dots are filled.
/// </summary>
public partial class ServingIndicator : ObservableObject
{
    [ObservableProperty]
    private double _fillOpacity;

    public ServingIndicator(bool isEarned) => _fillOpacity = isEarned ? 1 : 0;
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

    /// <summary>
    /// One dot per recommended serving, in order.
    /// </summary>
    public ObservableCollection<ServingIndicator> ServingIndicators { get; } = [];

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

        for (var i = 0; i < TotalRecommendedServings; i++)
        {
            ServingIndicators.Add(new ServingIndicator(i < servingsCompleted));
        }
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

    /// <summary>
    /// Whether the row has anything to remove. The remove button is hidden entirely
    /// until a first serving is added, so an untouched row shows only the add control.
    /// </summary>
    public bool CanDecrement => ServingsCompleted > 0;

    partial void OnServingsCompletedChanged(int value)
    {
        UpdateServingIndicators();
        OnPropertyChanged(nameof(ServingsDisplayText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(CanDecrement));
        ServingsChanged?.Invoke(this, value);
    }

    private void UpdateServingIndicators()
    {
        for (var i = 0; i < ServingIndicators.Count; i++)
        {
            ServingIndicators[i].FillOpacity = i < ServingsCompleted ? 1 : 0;
        }
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
