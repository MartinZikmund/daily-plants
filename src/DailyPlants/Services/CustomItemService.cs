using System.Globalization;
using DailyPlants.Models;

namespace DailyPlants.Services;

public class CustomItemService : ICustomItemService
{
    private const int SortOrderIncrement = 100;
    private const int NameMaxLength = 60;
    private const int DescriptionMaxLength = 500;

    private readonly IDataService _dataService;

    public CustomItemService(IDataService dataService)
    {
        _dataService = dataService;
    }

    public Task<IReadOnlyList<CustomItem>> GetAllAsync() => _dataService.GetCustomItemsAsync();

    public async Task<CustomItem> CreateAsync(
        string name,
        string description,
        int recommendedServings,
        CustomItemIconType iconType,
        string iconValue)
    {
        var existing = await _dataService.GetCustomItemsAsync();
        var nextSort = (existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder)) + SortOrderIncrement;

        var item = new CustomItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ClampName(name),
            Description = ClampDescription(description),
            RecommendedServings = Math.Max(1, recommendedServings),
            IconType = iconType,
            IconValue = NormalizeIconValue(iconType, iconValue),
            SortOrder = nextSort,
            UpdatedAt = DateTime.UtcNow,
        };

        await _dataService.SaveCustomItemAsync(item);
        return item;
    }

    public Task UpdateAsync(CustomItem item)
    {
        var sanitized = item with
        {
            Name = ClampName(item.Name),
            Description = ClampDescription(item.Description),
            RecommendedServings = Math.Max(1, item.RecommendedServings),
            IconValue = NormalizeIconValue(item.IconType, item.IconValue),
            UpdatedAt = DateTime.UtcNow,
        };
        return _dataService.SaveCustomItemAsync(sanitized);
    }

    public Task DeleteAsync(string id, bool cascadeEntries) =>
        _dataService.DeleteCustomItemAsync(id, cascadeEntries);

    private static string ClampName(string name)
    {
        var trimmed = (name ?? "").Trim();
        return trimmed.Length > NameMaxLength ? trimmed[..NameMaxLength] : trimmed;
    }

    private static string ClampDescription(string description)
    {
        var value = description ?? "";
        return value.Length > DescriptionMaxLength ? value[..DescriptionMaxLength] : value;
    }

    private static string NormalizeIconValue(CustomItemIconType type, string value)
    {
        if (type == CustomItemIconType.Emoji)
        {
            var firstGrapheme = TakeFirstEmojiGrapheme(value);
            if (firstGrapheme is not null)
            {
                return firstGrapheme;
            }
            // Fall back to default catalog glyph when the emoji input is invalid.
            return CustomIconCatalog.DefaultKey;
        }

        return CustomIconCatalog.IsKnown(value) ? value : CustomIconCatalog.DefaultKey;
    }

    /// <summary>
    /// Returns the first grapheme cluster in <paramref name="input"/> if it contains
    /// an Extended_Pictographic codepoint; otherwise null.
    /// </summary>
    public static string? TakeFirstEmojiGrapheme(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(input);
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var first = (string)enumerator.Current;
        return ContainsEmojiCodepoint(first) ? first : null;
    }

    private static bool ContainsEmojiCodepoint(string grapheme)
    {
        for (var i = 0; i < grapheme.Length;)
        {
            var cp = char.ConvertToUtf32(grapheme, i);
            if (IsEmojiCodepoint(cp))
            {
                return true;
            }
            i += char.IsSurrogatePair(grapheme, i) ? 2 : 1;
        }
        return false;
    }

    private static bool IsEmojiCodepoint(int cp)
    {
        // Conservative Extended_Pictographic ranges covering common emoji blocks.
        // Avoids pulling in a full Unicode database while still rejecting plain text.
        return cp is
            (>= 0x00A9 and <= 0x00AE) or
            (>= 0x2000 and <= 0x3300) or
            (>= 0x1F000 and <= 0x1FAFF) or
            (>= 0x1F900 and <= 0x1F9FF);
    }
}
