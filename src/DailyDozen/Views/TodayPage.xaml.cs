using DailyDozen.Services;
using DailyDozen.ViewModels;

namespace DailyDozen.Views;

public sealed partial class TodayPage : Page
{
    public TodayViewModel ViewModel { get; }

    public TodayPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        ViewModel = new TodayViewModel(dataService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += TodayPage_Loaded;
    }

    private async void TodayPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDataAsync();
    }

    private void ItemCard_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Handle tap on item card to increment serving
        if (sender is FrameworkElement element && element.DataContext is ChecklistItemViewModel itemVm)
        {
            itemVm.ToggleServingCommand.Execute(null);
        }
    }
}
