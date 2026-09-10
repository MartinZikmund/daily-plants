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

    /// <summary>
    /// In a list, an empty row should offer only the control that can do something, so
    /// remove is dropped from the layout. On its own in a dialog there is no list to
    /// keep tidy, and dropping it leaves the remaining controls looking off-centre --
    /// there it stays put and greys out instead.
    /// </summary>
    public static readonly DependencyProperty CollapseRemoveWhenEmptyProperty =
        DependencyProperty.Register(
            nameof(CollapseRemoveWhenEmpty),
            typeof(bool),
            typeof(ServingStepper),
            new PropertyMetadata(true));

    /// <summary>
    /// The add button, for anything that needs to point at it - the diary's teaching tip
    /// anchors here, because this is the control that actually logs a serving.
    /// </summary>
    public FrameworkElement IncrementTarget => IncrementButton;

    public ServingStepper() => this.InitializeComponent();

    public ChecklistItemViewModel? Item
    {
        get => (ChecklistItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public bool CollapseRemoveWhenEmpty
    {
        get => (bool)GetValue(CollapseRemoveWhenEmptyProperty);
        set => SetValue(CollapseRemoveWhenEmptyProperty, value);
    }

    // Qualified so x:Bind emits static calls; an unqualified function binding is
    // emitted as an instance call and will not compile against a static method.
    public static Visibility RemoveVisibility(bool canDecrement, bool collapseWhenEmpty)
        => canDecrement || !collapseWhenEmpty ? Visibility.Visible : Visibility.Collapsed;
}
