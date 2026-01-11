using DailyDozen.Services;
using DailyDozen.ViewModels;

namespace DailyDozen.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var exportService = App.Current.Services!.GetRequiredService<IExportService>();
        ViewModel = new SettingsViewModel(dataService, exportService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSettingsAsync();
    }
}
