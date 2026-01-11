using DailyDozen.Services;
using DailyDozen.ViewModels;

namespace DailyDozen.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        ViewModel = new SettingsViewModel(dataService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSettingsAsync();
    }
}
