using DailyPlants.Models;
using DailyPlants.Services;
using Microsoft.UI.Xaml.Controls;

namespace DailyPlants.ViewModels;

/// <summary>
/// Backing view-model for the add/edit CustomItem dialog. Save is enabled only when
/// inputs validate; duplicate-name and over-length checks raise non-blocking warnings.
/// </summary>
public partial class CustomItemEditorViewModel : ObservableObject
{
    private const int NameMaxLength = 60;
    private const int DescriptionMaxLength = 500;

    private readonly ICustomItemService _customItemService;
    private readonly IReadOnlyList<CustomItem> _otherItems;
    private readonly string? _editingId;
    private readonly int? _editingSortOrder;

    public bool IsEdit => _editingId is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNameTooLong))]
    [NotifyPropertyChangedFor(nameof(IsDuplicateName))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDescriptionTooLong))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _description = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _recommendedServings = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconSource))]
    private CustomItemIconType _iconType = CustomItemIconType.Catalog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconSource))]
    private string _iconValue = CustomIconCatalog.DefaultKey;

    [ObservableProperty]
    private int _sortOrder;

    public bool IsNameTooLong => Name?.Length > NameMaxLength;
    public bool IsDescriptionTooLong => Description?.Length > DescriptionMaxLength;

    public bool IsDuplicateName
    {
        get
        {
            var trimmed = (Name ?? "").Trim();
            if (trimmed.Length == 0) return false;
            return _otherItems.Any(i =>
                !string.Equals(i.Id, _editingId, StringComparison.Ordinal) &&
                string.Equals(i.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !IsNameTooLong &&
        !IsDescriptionTooLong &&
        RecommendedServings >= 1;

    public IconSource IconSource => CustomItemIconSourceFactory.Create(IconType, IconValue);

    /// <summary>
    /// New-item constructor.
    /// </summary>
    public CustomItemEditorViewModel(ICustomItemService customItemService, IReadOnlyList<CustomItem> existingItems)
    {
        _customItemService = customItemService;
        _otherItems = existingItems;
        _editingId = null;
        _editingSortOrder = null;
        SortOrder = (existingItems.Count == 0 ? 0 : existingItems.Max(i => i.SortOrder)) + 100;
    }

    /// <summary>
    /// Edit-existing constructor.
    /// </summary>
    public CustomItemEditorViewModel(ICustomItemService customItemService, IReadOnlyList<CustomItem> existingItems, CustomItem existing)
    {
        _customItemService = customItemService;
        _otherItems = existingItems;
        _editingId = existing.Id;
        _editingSortOrder = existing.SortOrder;
        Name = existing.Name;
        Description = existing.Description;
        RecommendedServings = existing.RecommendedServings;
        IconType = existing.IconType;
        IconValue = existing.IconValue;
        SortOrder = existing.SortOrder;
    }

    /// <summary>
    /// Selects a built-in catalog glyph from the picker.
    /// </summary>
    public void SelectCatalogIcon(string key)
    {
        IconType = CustomItemIconType.Catalog;
        IconValue = CustomIconCatalog.IsKnown(key) ? key : CustomIconCatalog.DefaultKey;
    }

    /// <summary>
    /// Applies user emoji input. Falls back to default catalog glyph when invalid.
    /// </summary>
    public void SetEmojiInput(string? rawInput)
    {
        var firstGrapheme = CustomItemService.TakeFirstEmojiGrapheme(rawInput);
        if (firstGrapheme is null)
        {
            IconType = CustomItemIconType.Catalog;
            IconValue = CustomIconCatalog.DefaultKey;
            return;
        }
        IconType = CustomItemIconType.Emoji;
        IconValue = firstGrapheme;
    }

    [RelayCommand(CanExecute = nameof(IsValid))]
    private async Task SaveAsync()
    {
        var trimmedName = (Name ?? "").Trim();
        var description = Description ?? "";

        if (_editingId is null)
        {
            await _customItemService.CreateAsync(trimmedName, description, RecommendedServings, IconType, IconValue);
        }
        else
        {
            var updated = new CustomItem
            {
                Id = _editingId,
                Name = trimmedName,
                Description = description,
                RecommendedServings = RecommendedServings,
                IconType = IconType,
                IconValue = IconValue,
                SortOrder = SortOrder,
                UpdatedAt = DateTime.UtcNow,
            };
            await _customItemService.UpdateAsync(updated);
        }
    }
}
