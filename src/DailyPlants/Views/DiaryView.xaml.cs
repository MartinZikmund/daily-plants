using System.Collections.Specialized;
using System.ComponentModel;
using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace DailyPlants.Views;

public sealed partial class DiaryView : Page
{
    private const string TwoColumnStateName = "TwoColumnState";

    /// <summary>
    /// Wide enough that a 900px canvas still splits into two readable columns.
    /// </summary>
    private const double TwoColumnMinItemWidth = 380;

    private static readonly TimeSpan RowArrivalDuration = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Rows that changed group since the last layout pass and should animate in.
    /// </summary>
    private readonly HashSet<ChecklistItemViewModel> _pendingArrivals = [];

    private readonly bool _animationsEnabled = AreAnimationsEnabled();

    public DiaryViewModel ViewModel { get; }

    public DiaryView()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var appPreferences = App.Current.Services!.GetRequiredService<IAppPreferences>();
        var achievementService = App.Current.Services!.GetService<IAchievementService>();
        ViewModel = new DiaryViewModel(dataService, appPreferences, achievementService)
        {
            AnimateGroupChanges = _animationsEnabled
        };
        ViewModel.ItemDetailRequested += ViewModel_ItemDetailRequested;

        this.InitializeComponent();
        this.DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DayCompleted += ViewModel_DayCompleted;
        ViewModel.DayReset += ViewModel_DayReset;
        ViewModel.StillToGo.CollectionChanged += Group_CollectionChanged;
        ViewModel.DoneToday.CollectionChanged += Group_CollectionChanged;

        this.Loaded += DiaryView_Loaded;
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
        await ViewModel.LoadDataAsync();
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
        if (_pendingArrivals.Count == 0 || args.Element is not FrameworkElement element)
        {
            return;
        }

        var group = ReferenceEquals(sender, DoneTodayRepeater) ? ViewModel.DoneToday : ViewModel.StillToGo;
        if (args.Index < 0 || args.Index >= group.Count || !_pendingArrivals.Remove(group[args.Index]))
        {
            return;
        }

        PlayRowArrival(element);
    }

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

    private async void CalendarView_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (args.AddedDates.Count > 0)
        {
            var selectedDate = DateOnly.FromDateTime(args.AddedDates[0].DateTime);
            await ViewModel.GoToDateAsync(selectedDate);
            DatePickerFlyout.Hide();
        }
    }

    private async void ViewModel_ItemDetailRequested(object? sender, ChecklistItemViewModel itemVm)
    {
        await ShowItemDetailDialogAsync(itemVm);
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
            Title = new TextBlock
            {
                Text = item.Name,
                Style = (Style)Application.Current.Resources["DpTitleTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            },
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

        await dialog.ShowAsync();
    }

    private void ItemRow_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // Taps anywhere on the row open the detail dialog, except within the serving
        // controls -- otherwise adding a serving would also pop the dialog over it.
        if (e.OriginalSource is DependencyObject source)
        {
            var current = source;
            while (current != null && !ReferenceEquals(current, sender))
            {
                if (current is Button or Controls.ServingStepper)
                {
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        // Tag first: ItemsRepeater leaves DataContext unset on x:Bind templates.
        if (sender is FrameworkElement element
            && (element.Tag as ChecklistItemViewModel ?? element.DataContext as ChecklistItemViewModel) is { } itemVm)
        {
            itemVm.ShowItemDetailCommand.Execute(null);
        }
    }
}
