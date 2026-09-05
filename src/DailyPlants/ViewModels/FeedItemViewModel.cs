using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Models;

namespace DailyPlants.ViewModels;

/// <summary>
/// One card on the Resources page or in the Diary teaser.
/// </summary>
public partial class FeedItemViewModel : ObservableObject
{
    private const int SummaryMaxLength = 180;

    public FeedItemViewModel(FeedItem item)
    {
        Item = item;
        ThumbnailUrl = Uri.TryCreate(item.ThumbnailUrl, UriKind.Absolute, out _) ? item.ThumbnailUrl : null;
        Summary = Truncate(item.Summary);
        PublishedText = FormatPublished(item.PublishedAt);
        KindText = FeedKindLabel.For(item.Kind);
        AutomationName = string.Format(
            CultureInfo.CurrentCulture,
            Localized("Resources_ItemAutomationName", "{0}. {1}, {2}"),
            Title,
            KindText,
            PublishedText);
    }

    public FeedItem Item { get; }

    /// <summary>The feed &lt;guid&gt;; used verbatim as AutomationProperties.AutomationId.</summary>
    public string AutomationId => Item.Id;

    public string Title => Item.Title;

    /// <summary>Summary trimmed at a word boundary and ellipsed; empty when the feed had none.</summary>
    public string Summary { get; }

    public bool HasSummary => !string.IsNullOrEmpty(Summary);

    /// <summary>Absolute thumbnail URL, or null. StringToUriConverter throws on anything else.</summary>
    public string? ThumbnailUrl { get; }

    public bool HasThumbnail => ThumbnailUrl is not null;

    /// <summary>Localized "Today"/"Yesterday" or a short local date. Empty when PublishedAt is null.</summary>
    public string PublishedText { get; }

    /// <summary>Localized feed name, e.g. "Blog" - shown as the card's kicker.</summary>
    public string KindText { get; }

    /// <summary>Resources_ItemAutomationName formatted with title, kind and date.</summary>
    public string AutomationName { get; }

    /// <summary>Opens <see cref="FeedItem.Link"/> in the system browser.</summary>
    /// <remarks>Nothing may be awaited before the launch call - wasm blocks a popup opened after the tap has yielded.</remarks>
    [RelayCommand]
    private async Task OpenAsync() => await BrowserLauncher.OpenAsync(Item.Link);

    private static string Truncate(string summary)
    {
        if (summary.Length <= SummaryMaxLength)
        {
            return summary;
        }

        var lastSpace = summary.LastIndexOf(' ', SummaryMaxLength);
        var head = lastSpace > 0 ? summary[..lastSpace] : summary[..SummaryMaxLength];
        return head.TrimEnd() + "…";
    }

    private static string FormatPublished(DateTimeOffset? publishedAt)
    {
        if (publishedAt is not { } published)
        {
            return string.Empty;
        }

        var localDate = published.ToLocalTime().Date;
        var today = DateTimeOffset.Now.Date;

        if (localDate == today)
        {
            return Localized("Resources_Today", "Today");
        }

        if (localDate == today.AddDays(-1))
        {
            return Localized("Resources_Yesterday", "Yesterday");
        }

        return published.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Resource lookup with an English fallback, for keys that are not yet in every
    /// <c>Strings/*/Resources.resw</c>. <see cref="Localizer"/> returns "[Key]" on a miss.
    /// </summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }
}
