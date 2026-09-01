using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class SettingsView : Page
{
    private readonly ILogger _logger =
        App.Current.Services!.GetRequiredService<ILoggerFactory>().CreateLogger<SettingsView>();

    public SettingsViewModel ViewModel { get; }

    public SettingsView()
    {
        var appPreferences = App.Current.Services!.GetRequiredService<IAppPreferences>();
        var exportService = App.Current.Services!.GetRequiredService<IExportService>();
        var localizationService = App.Current.Services!.GetRequiredService<ILocalizationService>();
        ViewModel = new SettingsViewModel(appPreferences, exportService, localizationService);

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += SettingsView_Loaded;
    }

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading settings failed");
        }
    }
}
