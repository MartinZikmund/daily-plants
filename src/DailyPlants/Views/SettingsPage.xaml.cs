using DailyPlants.Services;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        var dataService = App.Current.Services!.GetRequiredService<IDataService>();
        var exportService = App.Current.Services!.GetRequiredService<IExportService>();
        var localizationService = App.Current.Services!.GetRequiredService<ILocalizationService>();
        ViewModel = new SettingsViewModel(dataService, exportService, localizationService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSettingsAsync();
    }
}
