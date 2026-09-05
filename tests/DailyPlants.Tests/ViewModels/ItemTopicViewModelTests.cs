using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// The "Latest on ..." block of the item detail dialog. Its whole job is to be absent unless it has
/// something to show, so most of these assert what it does <i>not</i> do.
/// </summary>
[TestClass]
public class ItemTopicViewModelTests
{
    private const string Slug = "berries";

    private static ChecklistItem Item(string? topicSlug, string name = "Berries") => new()
    {
        Id = "berries",
        Name = name,
        Description = "A daily serving of berries.",
        RecommendedServings = 1,
        ServingSizeMetric = "60 g",
        ServingSizeImperial = "1/2 cup",
        TopicSlug = topicSlug,
        Checklists = [ChecklistType.DailyDozen]
    };

    private static FeedItem NewItem(string id) => new()
    {
        Id = id,
        Kind = FeedKind.Videos,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary."
    };

    private static FeedPage Page(params string[] ids)
        => new(ids.Select(NewItem).ToList(), FeedResultStatus.Fresh, MayHaveMore: true);

    private static ItemTopicViewModel Create(
        FakeFeedService feedService,
        FakeAppNavigator navigator,
        string? topicSlug,
        string name = "Berries")
        => new(feedService, navigator, Item(topicSlug, name));

    /// <summary>Mirrors the ViewModel's own resource lookup so the assertion holds in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    [TestMethod]
    public async Task NullTopicSlug_ProducesNoSectionAndNoRequest()
    {
        FakeFeedService feedService = new();
        var vm = Create(feedService, new FakeAppNavigator(), topicSlug: null);

        await vm.LoadAsync();

        vm.HasTopic.Should().BeFalse();
        vm.ShowProgress.Should().BeFalse();
        vm.ShowItems.Should().BeFalse();
        vm.Items.Should().BeEmpty();
        feedService.TopicRequests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task BlankTopicSlug_IsTreatedAsNoTopicAtAll()
    {
        FakeFeedService feedService = new();
        var vm = Create(feedService, new FakeAppNavigator(), topicSlug: "   ");

        await vm.LoadAsync();

        vm.HasTopic.Should().BeFalse();
        vm.ShowItems.Should().BeFalse();
        feedService.TopicRequests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadAsync_ShowsOnlyTheNewestThree()
    {
        FakeFeedService feedService = new();
        feedService.SetTopicPage(Slug, 1, Page("one", "two", "three", "four", "five"));
        var vm = Create(feedService, new FakeAppNavigator(), Slug);

        await vm.LoadAsync();

        vm.Items.Select(item => item.Title).Should().Equal("one", "two", "three");
        vm.HasTopic.Should().BeTrue();
        vm.ShowItems.Should().BeTrue();
        vm.ShowProgress.Should().BeFalse();
        feedService.TopicRequests.Should().ContainSingle().Which.Should().Be((Slug, 1));
    }

    [TestMethod]
    public async Task LoadAsync_FewerThanThree_ShowsWhatThereIs()
    {
        FakeFeedService feedService = new();
        feedService.SetTopicPage(Slug, 1, Page("one"));
        var vm = Create(feedService, new FakeAppNavigator(), Slug);

        await vm.LoadAsync();

        vm.Items.Should().ContainSingle();
        vm.ShowItems.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_TopicWithNothingInIt_LeavesTheSectionAbsent()
    {
        FakeFeedService feedService = new();
        var vm = Create(feedService, new FakeAppNavigator(), Slug);

        await vm.LoadAsync();

        vm.Items.Should().BeEmpty();
        vm.HasItems.Should().BeFalse();
        vm.ShowItems.Should().BeFalse();
        vm.ShowProgress.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_ServiceThrows_StaysAbsentAndDoesNotThrow()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new HttpRequestException("offline") };
        var vm = Create(feedService, new FakeAppNavigator(), Slug);

        await vm.Invoking(v => v.LoadAsync()).Should().NotThrowAsync();

        vm.ShowItems.Should().BeFalse();
        vm.ShowProgress.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();
        vm.Items.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadAsync_UnreachableTopic_StaysAbsentRatherThanShowingAnError()
    {
        FakeFeedService feedService = new();
        feedService.SetTopicPage(Slug, 1, FeedPage.End(FeedResultStatus.Unavailable));
        var vm = Create(feedService, new FakeAppNavigator(), Slug);

        await vm.LoadAsync();

        vm.ShowItems.Should().BeFalse();
        vm.ShowProgress.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_WhileTheFetchIsOut_ShowsTheProgressIndicatorAndNoCards()
    {
        FakeFeedService feedService = new();
        feedService.SetTopicPage(Slug, 1, Page("one"));
        var vm = Create(feedService, new FakeAppNavigator(), Slug);
        feedService.Pause();

        var loading = vm.LoadAsync();

        vm.IsLoading.Should().BeTrue();
        vm.ShowProgress.Should().BeTrue();
        vm.ShowItems.Should().BeFalse();

        feedService.Resume();
        await loading;

        vm.ShowProgress.Should().BeFalse();
        vm.ShowItems.Should().BeTrue();
    }

    [TestMethod]
    public void Header_ReadsBackTheItemName()
    {
        var vm = Create(new FakeFeedService(), new FakeAppNavigator(), Slug, "Flaxseeds");

        vm.Header.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Localized("Resources_TopicHeader", "Latest on {0}"),
            "Flaxseeds"));
    }

    [TestMethod]
    public void SeeAll_DeepLinksToTheResourcesPageInTopicMode()
    {
        FakeAppNavigator navigator = new();
        var vm = Create(new FakeFeedService(), navigator, "flax-seeds", "Flaxseeds");

        vm.SeeAllCommand.Execute(null);

        navigator.RequestCount.Should().Be(1);
        navigator.LastPageTag.Should().Be("Resources");
        navigator.LastParameter.Should().Be(new ResourcesTopicRequest("flax-seeds", "Flaxseeds"));
    }

    [TestMethod]
    public void SeeAll_AsksTheDialogToCloseBeforeItNavigates()
    {
        FakeAppNavigator navigator = new();
        var vm = Create(new FakeFeedService(), navigator, Slug);
        var requestsWhenAskedToClose = -1;
        vm.SeeAllRequested += (_, _) => requestsWhenAskedToClose = navigator.RequestCount;

        vm.SeeAllCommand.Execute(null);

        requestsWhenAskedToClose.Should().Be(0, "the dialog has to be gone before the page changes underneath it");
        navigator.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public void SeeAll_WithNoTopic_DoesNothing()
    {
        FakeAppNavigator navigator = new();
        var vm = Create(new FakeFeedService(), navigator, topicSlug: null);
        var closed = false;
        vm.SeeAllRequested += (_, _) => closed = true;

        vm.SeeAllCommand.Execute(null);

        navigator.RequestCount.Should().Be(0);
        closed.Should().BeFalse();
    }
}
