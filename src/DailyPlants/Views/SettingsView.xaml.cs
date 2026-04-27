using DailyPlants.Helpers;
using DailyPlants.Models;
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
        var customItemService = App.Current.Services!.GetService<ICustomItemService>();
        var dataService = App.Current.Services!.GetService<IDataService>();
        ViewModel = new SettingsViewModel(appPreferences, exportService, localizationService, customItemService, dataService);
        ViewModel.CustomItemDialogRequested += OnCustomItemDialogRequested;
        ViewModel.CustomItemDeletePromptRequested += OnCustomItemDeletePromptRequested;

        this.InitializeComponent();
        this.DataContext = ViewModel;
        this.Loaded += SettingsView_Loaded;
    }

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSettingsAsync();
    }

    private async Task<bool> OnCustomItemDialogRequested(CustomItemEditorViewModel editorVm)
    {
        var dialog = new CustomItemEditorDialog(editorVm, this.XamlRoot);
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<CustomItemDeleteChoice> OnCustomItemDeletePromptRequested(CustomItem item)
    {
        var dialog = new ContentDialog
        {
            Title = Localizer.GetString("SettingsView_CustomItems_DeletePrompt_Title"),
            Content = item.Name,
            PrimaryButtonText = Localizer.GetString("SettingsView_CustomItems_DeletePrompt_KeepHistory"),
            SecondaryButtonText = Localizer.GetString("SettingsView_CustomItems_DeletePrompt_DeleteAll"),
            CloseButtonText = Localizer.GetString("SettingsView_CustomItems_DeletePrompt_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => CustomItemDeleteChoice.KeepHistory,
            ContentDialogResult.Secondary => CustomItemDeleteChoice.Cascade,
            _ => CustomItemDeleteChoice.Cancel,
        };
    }
}
