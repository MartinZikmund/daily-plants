using DailyPlants.Helpers;
using DailyPlants.Models;
using DailyPlants.ViewModels;

namespace DailyPlants.Views;

public sealed partial class IconPickerControl : UserControl
{
    private bool _suppressSelectionEvents;

    public IReadOnlyList<CustomIconCatalogEntry> CatalogEntries { get; } = CustomIconCatalogEntry.CreateAll();

    public CustomItemEditorViewModel? ViewModel => DataContext as CustomItemEditorViewModel;

    public IconPickerControl()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is null) return;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CustomItemEditorViewModel.IconType)
                or nameof(CustomItemEditorViewModel.IconValue))
            {
                RefreshFromViewModel();
            }
        };

        RefreshFromViewModel();
    }

    private void RefreshFromViewModel()
    {
        if (ViewModel is null) return;

        PreviewIcon.IconSource = ViewModel.IconSource;

        _suppressSelectionEvents = true;
        try
        {
            if (ViewModel.IconType == CustomItemIconType.Catalog)
            {
                var idx = -1;
                for (var i = 0; i < CatalogEntries.Count; i++)
                {
                    if (CatalogEntries[i].Key == ViewModel.IconValue)
                    {
                        idx = i;
                        break;
                    }
                }
                CatalogGrid.SelectedIndex = idx;
                if (EmojiInputBox.Text.Length > 0)
                {
                    EmojiInputBox.Text = "";
                }
            }
            else
            {
                CatalogGrid.SelectedIndex = -1;
                if (EmojiInputBox.Text != ViewModel.IconValue)
                {
                    EmojiInputBox.Text = ViewModel.IconValue;
                }
            }
        }
        finally
        {
            _suppressSelectionEvents = false;
        }
    }

    private void OnCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || ViewModel is null) return;

        if (CatalogGrid.SelectedItem is CustomIconCatalogEntry entry)
        {
            ViewModel.SelectCatalogIcon(entry.Key);
        }
    }

    private void OnEmojiInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSelectionEvents || ViewModel is null) return;
        ViewModel.SetEmojiInput(EmojiInputBox.Text);
    }
}
