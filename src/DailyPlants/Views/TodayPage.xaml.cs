using DailyPlants.Models;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class TodayPage : Page
{
    public TodayViewModel ViewModel { get; }

    public TodayPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var appPreferences = App.Current.Services!.GetRequiredService<IAppPreferences>();
        var achievementService = App.Current.Services!.GetService<IAchievementService>();
        ViewModel = new TodayViewModel(dataService, appPreferences, achievementService);
        ViewModel.ItemDetailRequested += ViewModel_ItemDetailRequested;

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += TodayPage_Loaded;
    }

    private async void TodayPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDataAsync();
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
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        });

        // Serving size section
        var servingSection = new StackPanel { Spacing = 4 };
        servingSection.Children.Add(new TextBlock
        {
            Text = "Serving Size",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        servingSection.Children.Add(new TextBlock
        {
            Text = $"{item.RecommendedServings} serving{(item.RecommendedServings > 1 ? "s" : "")} per day",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        });
        servingSection.Children.Add(new TextBlock
        {
            Text = item.ServingSizeExample,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(servingSection);

        // Serving controls
        var servingCountText = new TextBlock
        {
            Text = itemVm.ServingsDisplayText,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center
        };

        var minusButton = new Button
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Content = new FontIcon { FontSize = 14, Glyph = "\uE738" }
        };

        var plusButton = new Button
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Content = new FontIcon { FontSize = 14, Glyph = "\uE710", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black) }
        };

        minusButton.Click += (s, e) =>
        {
            itemVm.DecrementServingCommand.Execute(null);
            servingCountText.Text = itemVm.ServingsDisplayText;
        };

        plusButton.Click += (s, e) =>
        {
            itemVm.IncrementServingCommand.Execute(null);
            servingCountText.Text = itemVm.ServingsDisplayText;
        };

        var servingControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        servingControls.Children.Add(minusButton);
        servingControls.Children.Add(servingCountText);
        servingControls.Children.Add(plusButton);
        content.Children.Add(servingControls);

        // Health benefits section (if available)
        if (!string.IsNullOrEmpty(item.HealthBenefits))
        {
            var benefitsSection = new StackPanel { Spacing = 4 };
            benefitsSection.Children.Add(new TextBlock
            {
                Text = "Health Benefits",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            benefitsSection.Children.Add(new TextBlock
            {
                Text = item.HealthBenefits,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
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
                Text = "Learn More",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            var link = new HyperlinkButton
            {
                Content = "View on NutritionFacts.org",
                NavigateUri = new Uri(item.MoreInfoUrl)
            };
            linkSection.Children.Add(link);
            content.Children.Add(linkSection);
        }

        // Checklists this item belongs to
        var checklistsSection = new StackPanel { Spacing = 4 };
        checklistsSection.Children.Add(new TextBlock
        {
            Text = "Found In",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        var checklistNames = item.Checklists.Select(c => c switch
        {
            ChecklistType.DailyDozen => "Daily Dozen",
            ChecklistType.TwentyOneTweaks => "Twenty-One Tweaks",
            ChecklistType.AntiAgingEight => "Anti-Aging Eight",
            _ => c.ToString()
        });
        checklistsSection.Children.Add(new TextBlock
        {
            Text = string.Join(", ", checklistNames),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        });
        content.Children.Add(checklistsSection);

        var dialog = new ContentDialog
        {
            Title = item.Name,
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 400
            },
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void Grid_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ChecklistItemViewModel itemVm)
        {
            itemVm.ShowItemDetailCommand.Execute(null);
        }
    }
}
