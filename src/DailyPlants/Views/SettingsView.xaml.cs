using DailyPlants.Helpers;
using DailyPlants.Services;
using DailyPlants.Services.Settings;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class SettingsView : Page
{
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
        ViewModel.ChecklistImpactWarningRequested += ViewModel_ChecklistImpactWarningRequested;
    }

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Loading settings failed", ex);
        }
    }

    private async void ViewModel_ChecklistImpactWarningRequested(object? sender, EventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Localizer.GetString("Settings_ChecklistImpactTitle"),
                Content = Localizer.GetString("Settings_ChecklistImpactMessage"),
                CloseButtonText = Localizer.GetString("Common_Ok"),
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // Async void: a notice the user cannot act on must never take the app down.
            AppLog.Error("Showing the checklist change warning failed", ex);
        }
    }
}
