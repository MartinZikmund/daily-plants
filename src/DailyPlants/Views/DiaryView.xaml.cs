using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.Services.Tips;
using Microsoft.UI.Dispatching;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace DailyPlants.Views;

public sealed partial class DiaryView : Page
{
    private readonly ILogger _logger =
        App.Current.Services!.GetRequiredService<ILoggerFactory>().CreateLogger<DiaryView>();

    private const string TwoColumnStateName = "TwoColumnState";

    /// <summary>
    /// Wide enough that a 900px canvas still splits into two readable columns.
    /// </summary>
    private const double TwoColumnMinItemWidth = 380;

    /// <summary>Row hover and pressed wash, matching DpFeedCardButtonStyle.</summary>
    private const double RowHoverWashOpacity = 0.05;
    private const double RowPressedWashOpacity = 0.11;

    /// <summary>The item dialog's icon, big enough to read as the item's portrait.</summary>
    private const double DialogIconSize = 48;

    private static readonly TimeSpan RowArrivalDuration = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Rows that changed group since the last layout pass and should animate in.
    /// </summary>
    private readonly HashSet<ChecklistItemViewModel> _pendingArrivals = [];

    private readonly bool _animationsEnabled = AreAnimationsEnabled();

    public DiaryViewModel ViewModel { get; }

    private DispatcherTimer? _midnightTimer;

    private bool _tipsEvaluated;

    public DiaryView()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var appPreferences = App.Current.Services!.GetRequiredService<IAppPreferences>();
        var achievementService = App.Current.Services!.GetService<IAchievementService>();
        ViewModel = new DiaryViewModel(
            dataService,
            appPreferences,
            achievementService,
            logger: App.Current.Services!.GetRequiredService<ILogger<DiaryViewModel>>(),
            tipService: App.Current.Services!.GetService<ITipService>())
        {
            AnimateGroupChanges = _animationsEnabled
        };
        ViewModel.ItemDetailRequested += ViewModel_ItemDetailRequested;
        ViewModel.SaveFailed += ViewModel_SaveFailed;

        this.InitializeComponent();
        this.DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DayCompleted += ViewModel_DayCompleted;
        ViewModel.DayReset += ViewModel_DayReset;
        ViewModel.StillToGo.CollectionChanged += Group_CollectionChanged;
        ViewModel.DoneToday.CollectionChanged += Group_CollectionChanged;

        this.Loaded += DiaryView_Loaded;
        this.Unloaded += DiaryView_Unloaded;
    }

    /// <summary>
    /// Chevron for the collapsible "Done today" header.
    /// </summary>
    public string ChevronGlyph(bool isExpanded) => isExpanded ? "\uE70E" : "\uE70D";

    private static bool AreAnimationsEnabled()
    {
        try
        {
            return new UISettings().AnimationsEnabled;
        }
        catch
        {
            // Not every head exposes UISettings; assume motion is welcome.
            return true;
        }
    }

    private async void DiaryView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyColumnLayout(WidthStates.CurrentState?.Name == TwoColumnStateName);

        try
        {
            if (App.Current.MainWindow is { } window)
            {
                window.Activated += Window_Activated;
            }

            ScheduleMidnightRefresh();

            await ViewModel.RefreshIfDateChangedAsync();
            await ViewModel.LoadDataAsync();

            // With rows to show, evaluation waits for the first one to realize so the tour
            // has something to point at. With none, it can happen now.
            if (ViewModel.StillToGo.Count == 0)
            {
                await EvaluateTipsOnceAsync(canPointAtARow: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading the diary failed");
        }
    }

    /// <summary>
    /// Runs the teaching flow's decision once per page load. A tip is never worth taking
    /// the page down with it, so a failure here is logged and forgotten.
    /// </summary>
    private async Task EvaluateTipsOnceAsync(bool canPointAtARow)
    {
        if (_tipsEvaluated)
        {
            return;
        }

        _tipsEvaluated = true;

        try
        {
            await ViewModel.EvaluateTipsAsync(canPointAtARow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deciding which teaching tip to show failed");
        }
    }

    private void WidthStates_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        => ApplyColumnLayout(e.NewState?.Name == TwoColumnStateName);

    private void ApplyColumnLayout(bool twoColumns)
    {
        var columns = twoColumns ? 2 : 1;
        var minItemWidth = twoColumns ? TwoColumnMinItemWidth : 0;

        StillToGoLayout.MaximumRowsOrColumns = columns;
        StillToGoLayout.MinItemWidth = minItemWidth;
        DoneTodayLayout.MaximumRowsOrColumns = columns;
        DoneTodayLayout.MinItemWidth = minItemWidth;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiaryViewModel.OverallProgress))
        {
            UpdateTallyFill();
        }
    }

    private void TallyTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTallyFill();

    private void UpdateTallyFill()
        => TallyFill.Width = TallyTrack.ActualWidth * Math.Clamp(ViewModel.OverallProgress, 0, 1);

    private void ViewModel_DayCompleted(object? sender, DayCompleteInfo info)
        => Parade.Play(info.TotalServings, info.Headline, info.Subhead);

    private void ViewModel_DayReset(object? sender, EventArgs e)
    {
        _pendingArrivals.Clear();
        Parade.Reset();
    }

    private void Group_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Adds during a load are the whole day being rebuilt, not a row changing group.
        if (!_animationsEnabled || ViewModel.IsLoading ||
            e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            return;
        }

        foreach (var item in e.NewItems.OfType<ChecklistItemViewModel>())
        {
            _pendingArrivals.Add(item);
        }
    }

    private void GroupRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element)
        {
            return;
        }

        var group = ReferenceEquals(sender, DoneTodayRepeater) ? ViewModel.DoneToday : ViewModel.StillToGo;
        if (args.Index < 0 || args.Index >= group.Count)
        {
            return;
        }

        var itemVm = group[args.Index];

        // Containers are recycled, so the row is renamed on every prepare rather than once.
        AutomationProperties.SetName(element, RowAutomationName(itemVm));

        if (args.Index == 0 && ReferenceEquals(sender, StillToGoRepeater))
        {
            LogServingTip.Target = element;

            // A row is prepared before it is arranged, and a tip opened against its
            // pre-layout bounds lands over the row instead of below it. Going through the
            // queue at low priority puts the decision after this pass of layout.
            DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                async () => await EvaluateTipsOnceAsync(canPointAtARow: true));
        }

        if (_pendingArrivals.Count == 0 || !_pendingArrivals.Remove(itemVm))
        {
            return;
        }

        PlayRowArrival(element);
    }

    /// <summary>
    /// What a screen reader says for a row: the item, then what tapping it does. The wash alone
    /// would leave the affordance purely visual.
    /// </summary>
    private static string RowAutomationName(ChecklistItemViewModel itemVm) => string.Format(
        CultureInfo.CurrentCulture,
        Localizer.GetString("Diary_ItemRowAutomationName"),
        itemVm.Item.Name);

    private static void PlayRowArrival(FrameworkElement element)
    {
        var translate = new TranslateTransform { Y = 8 };
        element.RenderTransform = translate;
        element.Opacity = 0;

        var duration = new Duration(RowArrivalDuration);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var slide = new DoubleAnimation { From = 8, To = 0, Duration = duration, EasingFunction = easing, EnableDependentAnimation = true };
        Storyboard.SetTarget(slide, translate);
        Storyboard.SetTargetProperty(slide, "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Completed += (_, _) =>
        {
            // Containers are recycled, so hand the element back in its resting state.
            element.Opacity = 1;
            element.RenderTransform = null;
        };
        storyboard.Begin();
    }

    private void DiaryView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (App.Current.MainWindow is { } window)
        {
            window.Activated -= Window_Activated;
        }

        StopMidnightTimer();
    }

    private async void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Covers the common case: the app is resumed the morning after it was left open.
        // The activation state is deliberately not inspected — its enum type differs
        // between the Windows and Uno heads, and the refresh is a no-op when the date has
        // not changed, so running it on deactivation costs nothing.
        try
        {
            await ViewModel.RefreshIfDateChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refreshing the diary date failed");
        }
    }

    /// <summary>
    /// Covers the case the activation hook cannot: the window stays focused across midnight.
    /// </summary>
    private void ScheduleMidnightRefresh()
    {
        StopMidnightTimer();

        var now = DateTime.Now;
        var untilMidnight = now.Date.AddDays(1) - now;
        if (untilMidnight <= TimeSpan.Zero)
        {
            untilMidnight = TimeSpan.FromMinutes(1);
        }

        _midnightTimer = new DispatcherTimer
        {
            // A second past the boundary, so the new date has definitely arrived.
            Interval = untilMidnight + TimeSpan.FromSeconds(1)
        };
        _midnightTimer.Tick += MidnightTimer_Tick;
        _midnightTimer.Start();
    }

    /// <summary>
    /// Stops the timer and detaches the handler: a tick already queued on the dispatcher
    /// would otherwise still run, and the subscription keeps the page alive.
    /// </summary>
    private void StopMidnightTimer()
    {
        if (_midnightTimer is not { } timer) return;

        timer.Tick -= MidnightTimer_Tick;
        timer.Stop();
        _midnightTimer = null;
    }

    private async void MidnightTimer_Tick(object? sender, object e)
    {
        try
        {
            await ViewModel.RefreshIfDateChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Midnight diary refresh failed");
        }

        ScheduleMidnightRefresh();
    }

    private async void CalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (args.AddedDates.Count == 0) return;

        try
        {
            var selectedDate = DateOnly.FromDateTime(args.AddedDates[0].DateTime);
            await ViewModel.GoToDateAsync(selectedDate);
            DatePickerFlyout.Hide();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigating to the selected date failed");
        }
    }

    private async void ViewModel_SaveFailed(object? sender, Exception exception)
    {
        _logger.LogError(exception, "Saving a serving failed");

        try
        {
            // The count shown has already been rolled back, so the user is told rather
            // than left believing a serving was recorded.
            var dialog = new ContentDialog
            {
                Title = Localizer.GetString("Diary_SaveFailedTitle"),
                Content = Localizer.GetString("Diary_SaveFailedMessage"),
                CloseButtonText = Localizer.GetString("Common_Ok"),
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not show the save failure dialog");
        }
    }

    private async void ViewModel_ItemDetailRequested(object? sender, ChecklistItemViewModel itemVm)
    {
        try
        {
            await ShowItemDetailDialogAsync(itemVm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Showing item detail failed");
        }
    }

    private async Task ShowItemDetailDialogAsync(ChecklistItemViewModel itemVm)
    {
        var item = itemVm.Item;
        var content = new StackPanel { Spacing = 16 };

        // Description
        content.Children.Add(new TextBlock
        {
            Text = item.Description,
            Style = (Style)Application.Current.Resources["DpBodyTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        });

        // Serving size section
        var servingSection = new StackPanel { Spacing = 4 };
        servingSection.Children.Add(new TextBlock
        {
            Text = Localizer.GetString("Diary_ServingSize"),
            Style = (Style)Application.Current.Resources["DpBodyStrongTextBlockStyle"]
        });
        servingSection.Children.Add(new TextBlock
        {
            Text = itemVm.TotalRecommendedServings == 1
                ? Localizer.GetString("Diary_ServingPerDay")
                : string.Format(Localizer.GetString("Diary_ServingsPerDay"), itemVm.TotalRecommendedServings),
            Style = (Style)Application.Current.Resources["DpCaptionTextBlockStyle"]
        });
        servingSection.Children.Add(new TextBlock
        {
            Text = itemVm.ServingSizeDisplay,
            Style = (Style)Application.Current.Resources["DpBodyTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(servingSection);

        // Merged children info (when items from multiple checklists are combined)
        if (itemVm.HasMergedChildren)
        {
            var mergeSection = new StackPanel { Spacing = 4 };
            mergeSection.Children.Add(new TextBlock
            {
                Text = Localizer.GetString("Diary_AlsoIncludes"),
                Style = (Style)Application.Current.Resources["DpBodyStrongTextBlockStyle"]
            });

            foreach (var child in itemVm.MergedChildren)
            {
                mergeSection.Children.Add(new TextBlock
                {
                    Text = $"+{child.RecommendedServings} {child.Name} ({itemVm.GetServingSizeDisplay(child)})",
                    Style = (Style)Application.Current.Resources["DpCaptionTextBlockStyle"],
                    TextWrapping = TextWrapping.Wrap
                });
            }

            content.Children.Add(mergeSection);
        }

        // The same stepper the Diary row uses, so the two cannot drift apart.
        content.Children.Add(new Controls.ServingStepper
        {
            Item = itemVm,
            HorizontalAlignment = HorizontalAlignment.Center,

            // Alone in a dialog, dropping remove from the layout throws the remaining
            // controls off-centre; keep it in place and let it grey out.
            CollapseRemoveWhenEmpty = false
        });

        // Health benefits section (if available)
        if (!string.IsNullOrEmpty(item.HealthBenefits))
        {
            var benefitsSection = new StackPanel { Spacing = 4 };
            benefitsSection.Children.Add(new TextBlock
            {
                Text = Localizer.GetString("Diary_HealthBenefits"),
                Style = (Style)Application.Current.Resources["DpBodyStrongTextBlockStyle"]
            });
            benefitsSection.Children.Add(new TextBlock
            {
                Text = item.HealthBenefits,
                Style = (Style)Application.Current.Resources["DpBodyTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(benefitsSection);
        }

        // "Latest on <item>" - built now, filled in after the dialog is up, absent when the item
        // has no nutritionfacts.org topic.
        var topic = new ItemTopicViewModel(
            App.Current.Services!.GetRequiredService<IFeedService>(),
            App.Current.Services!.GetRequiredService<IAppNavigator>(),
            item,
            App.Current.Services!.GetRequiredService<ILoggerFactory>());

        if (topic.HasTopic)
        {
            content.Children.Add(BuildTopicSection(topic));
        }

        // More info link (if available)
        if (!string.IsNullOrEmpty(item.MoreInfoUrl))
        {
            var linkSection = new StackPanel { Spacing = 4 };
            linkSection.Children.Add(new TextBlock
            {
                Text = Localizer.GetString("Diary_LearnMore"),
                Style = (Style)Application.Current.Resources["DpBodyStrongTextBlockStyle"]
            });
            var link = new HyperlinkButton
            {
                Content = Localizer.GetString("Diary_ViewOnNutritionFacts"),
                NavigateUri = new Uri(item.MoreInfoUrl)
            };
            linkSection.Children.Add(link);
            content.Children.Add(linkSection);
        }

        // Checklists this item belongs to
        var checklistsSection = new StackPanel { Spacing = 4 };
        checklistsSection.Children.Add(new TextBlock
        {
            Text = Localizer.GetString("Diary_FoundIn"),
            Style = (Style)Application.Current.Resources["DpBodyStrongTextBlockStyle"]
        });
        var checklistNames = item.Checklists.Select(c => c switch
        {
            ChecklistType.DailyDozen => Localizer.GetString("Settings_DailyDozen"),
            ChecklistType.TwentyOneTweaks => Localizer.GetString("Settings_TwentyOneTweaks"),
            _ => c.ToString()
        });
        checklistsSection.Children.Add(new TextBlock
        {
            Text = string.Join(", ", checklistNames),
            Style = (Style)Application.Current.Resources["DpCaptionTextBlockStyle"]
        });
        content.Children.Add(checklistsSection);

        var dialog = new ContentDialog
        {
            Title = BuildDialogTitle(item),
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 400
            },
            CloseButtonText = Localizer.GetString("Common_Close"),
            Style = (Style)Application.Current.Resources["DpContentDialogStyle"],
            XamlRoot = this.XamlRoot,

            // A dialog is hosted in a popup, so it does not inherit the page's theme.
            // Without this it renders dark while the app is set to light, and vice versa.
            RequestedTheme = (XamlRoot?.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default
        };

        // Raised before the navigation, so the frame never changes under an open dialog.
        topic.SeeAllRequested += (_, _) => dialog.Hide();

        var showing = dialog.ShowAsync();

        // Only now, with the dialog already on screen, is anything asked of the network.
        // LoadAsync never throws and nothing waits on it.
        _ = topic.LoadAsync();

        await showing;
    }

    /// <summary>
    /// The dialog's heading: the item's own colourful icon beside its name, drawn the way the
    /// Diary row draws it. Items with no icon get the name alone.
    /// </summary>
    private static object BuildDialogTitle(ChecklistItem item)
    {
        var name = new TextBlock
        {
            Text = item.Name,
            Style = (Style)Application.Current.Resources["DpTitleTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!Uri.TryCreate(item.IconPath, UriKind.Absolute, out var iconUri))
        {
            return name;
        }

        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        heading.Children.Add(new BitmapIcon
        {
            Width = DialogIconSize,
            Height = DialogIconSize,
            VerticalAlignment = VerticalAlignment.Center,
            ShowAsMonochrome = false,
            UriSource = iconUri
        });
        heading.Children.Add(name);
        return heading;
    }

    /// <summary>
    /// The dialog's "Latest on ..." block: the section header with a "See all", a small spinner
    /// while the topic feed is out, then the three newest cards. Its chrome is a XAML template so
    /// that its ThemeResource brushes resolve against the dialog's own theme; only the block's
    /// overall visibility is driven from here, and it stays collapsed unless there is something to
    /// show - a failed or empty fetch leaves no trace.
    /// </summary>
    private FrameworkElement BuildTopicSection(ItemTopicViewModel topic)
    {
        var template = (DataTemplate)Resources["DialogTopicSectionTemplate"];
        var section = (FrameworkElement)template.LoadContent()!;

        // Assigning the DataContext is what connects the template's compiled bindings.
        section.DataContext = topic;

        void Sync() => section.Visibility = VisibleWhen(topic.ShowProgress || topic.ShowItems);

        topic.PropertyChanged += (_, _) => Sync();
        Sync();

        return section;
    }

    private static Visibility VisibleWhen(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ItemRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Taps anywhere on the row open the detail dialog, except within the serving
        // controls -- otherwise adding a serving would also pop the dialog over it.
        if (IsWithinRowControls(e.OriginalSource, sender))
        {
            return;
        }

        // The dialog covers the row, so no pointer event arrives to take the wash back down.
        SetRowWash(sender, 0);

        // Tag first: ItemsRepeater leaves DataContext unset on x:Bind templates.
        if (sender is FrameworkElement element
            && (element.Tag as ChecklistItemViewModel ?? element.DataContext as ChecklistItemViewModel) is { } itemVm)
        {
            itemVm.ShowItemDetailCommand.Execute(null);
        }
    }

    private void ItemRow_PointerOver(object sender, PointerRoutedEventArgs e)
        => SetRowWash(sender, IsWithinRowControls(e.OriginalSource, sender) ? 0 : RowHoverWashOpacity);

    private void ItemRow_PointerPressed(object sender, PointerRoutedEventArgs e)
        => SetRowWash(sender, IsWithinRowControls(e.OriginalSource, sender) ? 0 : RowPressedWashOpacity);

    /// <summary>
    /// The pointer left, was released, or was taken away. Exits bubbling up from the stepper leave
    /// the wash down for a frame until the next move lights it again - cheaper than tracking which
    /// child the pointer moved to.
    /// </summary>
    private void ItemRow_PointerLeft(object sender, PointerRoutedEventArgs e) => SetRowWash(sender, 0);

    /// <summary>
    /// True when the pointer is on one of the row's own controls - the stepper, or a button inside
    /// it. Tapping there does not open the dialog, so the row must not look tappable either. Shared
    /// with <see cref="ItemRow_Tapped"/> so the wash and the activation cannot disagree.
    /// </summary>
    private static bool IsWithinRowControls(object originalSource, object row)
    {
        if (originalSource is not DependencyObject source)
        {
            return false;
        }

        var current = source;
        while (current != null && !ReferenceEquals(current, row))
        {
            if (current is Button or Controls.ServingStepper)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// The row template's only Border child is the wash; its own ScalarTransition does the fade.
    /// </summary>
    private static void SetRowWash(object row, double opacity)
    {
        if ((row as Panel)?.Children.OfType<Border>().FirstOrDefault() is { } wash)
        {
            wash.Opacity = opacity;
        }
    }
}
