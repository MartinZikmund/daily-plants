using DailyPlants.ViewModels;

namespace DailyPlants.Controls;

/// <summary>
/// Remove / serving dots / add. Shared by the Diary row and the item detail dialog.
/// </summary>
public sealed partial class ServingStepper : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(
            nameof(Item),
            typeof(ChecklistItemViewModel),
            typeof(ServingStepper),
            new PropertyMetadata(null));

    public ServingStepper() => this.InitializeComponent();

    public ChecklistItemViewModel? Item
    {
        get => (ChecklistItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>
    /// Qualified so x:Bind emits a static call; an unqualified function binding is
    /// emitted as an instance call and will not compile against a static method.
    /// </summary>
    public static Visibility VisibleWhen(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
