using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class FeedItemViewModelTests
{
    private static FeedItem NewItem(
        string id = "https://nutritionfacts.org/?p=12345",
        FeedKind kind = FeedKind.Blog,
        string title = "Beans, beans",
        string summary = "A short summary.",
        string? thumbnailUrl = "https://nutritionfacts.org/thumb.jpg",
        DateTimeOffset? publishedAt = null) => new()
        {
            Id = id,
            Kind = kind,
            Title = title,
            Link = "https://nutritionfacts.org/blog/beans/",
            Summary = summary,
            ThumbnailUrl = thumbnailUrl,
            PublishedAt = publishedAt
        };

    /// <summary>Mirrors the ViewModels' own resource lookup so the assertions hold in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    [TestMethod]
    public void Summary_LongerThanLimit_TruncatedAtWordBoundaryWithEllipsis()
    {
        var summary = string.Join(" ", Enumerable.Repeat("plants", 60));
        var vm = new FeedItemViewModel(NewItem(summary: summary));

        vm.Summary.Should().EndWith("…");
        vm.Summary.Length.Should().BeLessThanOrEqualTo(181);

        var head = vm.Summary[..^1];
        summary.Should().StartWith(head);
        summary[head.Length].Should().Be(' ', "the cut lands on a word boundary");
    }

    [TestMethod]
    public void Summary_ShorterThanLimit_Unchanged()
    {
        var vm = new FeedItemViewModel(NewItem(summary: "Kale is a leafy green."));

        vm.Summary.Should().Be("Kale is a leafy green.");
        vm.HasSummary.Should().BeTrue();
    }

    [TestMethod]
    public void PublishedText_Today_UsesTodayLabel()
    {
        var vm = new FeedItemViewModel(NewItem(publishedAt: DateTimeOffset.Now));

        vm.PublishedText.Should().Be(Localized("Latest_Today", "Today"));
    }

    [TestMethod]
    public void PublishedText_OlderThanYesterday_UsesAShortDate()
    {
        var published = DateTimeOffset.Now.AddDays(-10);
        var vm = new FeedItemViewModel(NewItem(publishedAt: published));

        vm.PublishedText.Should().Be(published.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void PublishedText_Null_IsEmpty()
    {
        var vm = new FeedItemViewModel(NewItem(publishedAt: null));

        vm.PublishedText.Should().BeEmpty();
    }

    [TestMethod]
    public void AutomationId_IsTheFeedGuid()
    {
        var vm = new FeedItemViewModel(NewItem(id: "https://nutritionfacts.org/?p=99"));

        vm.AutomationId.Should().Be("https://nutritionfacts.org/?p=99");
        vm.AutomationName.Should().Contain(vm.Title).And.Contain(vm.KindText);
    }

    [TestMethod]
    public void HasThumbnail_NullUrl_IsFalse()
    {
        var withThumbnail = new FeedItemViewModel(NewItem());
        var without = new FeedItemViewModel(NewItem(thumbnailUrl: null));

        withThumbnail.HasThumbnail.Should().BeTrue();
        without.HasThumbnail.Should().BeFalse();
        without.ThumbnailUrl.Should().BeNull();
    }
}
