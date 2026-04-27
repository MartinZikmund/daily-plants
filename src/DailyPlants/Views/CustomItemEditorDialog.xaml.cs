using DailyPlants.Helpers;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class CustomItemEditorDialog : ContentDialog
{
    public CustomItemEditorViewModel ViewModel { get; }

    public CustomItemEditorDialog(CustomItemEditorViewModel viewModel, XamlRoot xamlRoot)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
        this.XamlRoot = xamlRoot;
        this.Title = Localizer.GetString(viewModel.IsEdit
            ? "CustomItemEditor_Title_Edit"
            : "CustomItemEditor_Title_Add");
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!ViewModel.IsValid)
        {
            args.Cancel = true;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            await ViewModel.SaveCommand.ExecuteAsync(null);
        }
        finally
        {
            deferral.Complete();
        }
    }
}
