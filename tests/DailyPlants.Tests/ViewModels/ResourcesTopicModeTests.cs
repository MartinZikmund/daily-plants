using System.Globalization;
using DailyPlants.Helpers;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

/// <summary>
/// Topic mode on the Resources page: one nutritionfacts.org topic standing in for the tab strip,
/// and its relationship with search mode, which it is never allowed to share the screen with.
/// </summary>
[TestClass]
public class ResourcesTopicModeTests
{
    private const string Slug = "berries";
    private const string TopicName = "Berries";

    private static FeedItem NewItem(string id, FeedKind kind = FeedKind.Videos) => new()
    {
        Id = id,
        Kind = kind,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary."
    };

    private static FeedPage Page(bool mayHaveMore, params string[] ids)
        => new(ids.Select(id => NewItem(id)).ToList(), FeedResultStatus.Fresh, mayHaveMore);

    /// <summary>A service whose page 1 of <see cref="Slug"/> has two items and claims more.</summary>
    private static FakeFeedService TopicService()
    {
        FakeFeedService feedService = new();
        feedService.SetTopicPage(Slug, 1, Page(mayHaveMore: true, "berries-1", "berries-2"));
        return feedService;
    }

    /// <summary>Mirrors the ViewModels' own resource lookup so the assertions hold in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    [TestMethod]
    public async Task ShowTopicAsync_LoadsTheTopicAndStandsInForTheTabs()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);

        await vm.ShowTopicAsync(Slug, TopicName);

        vm.IsTopicActive.Should().BeTrue();
        vm.TopicSlug.Should().Be(Slug);
        vm.TopicTitle.Should().Be(TopicName);
        vm.ShowTabs.Should().BeFalse();
        vm.IsOverviewActive.Should().BeFalse();
        vm.ActiveList.Should().BeSameAs(vm.TopicResults);
        vm.TopicResults.Items.Should().HaveCount(2);
        feedService.TopicRequests.Should().ContainSingle().Which.Should().Be((Slug, 1));
    }

    [TestMethod]
    public async Task ShowTopicAsync_TrimsTheSlugAndTheTitle()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);

        await vm.ShowTopicAsync($"  {Slug}  ", $" {TopicName} ");

        vm.TopicSlug.Should().Be(Slug);
        vm.TopicTitle.Should().Be(TopicName);
        feedService.TopicRequests.Should().ContainSingle().Which.Should().Be((Slug, 1));
    }

    [TestMethod]
    public async Task TopicHeader_ReadsBackTheItemName()
    {
        ResourcesViewModel vm = new(TopicService());

        await vm.ShowTopicAsync(Slug, TopicName);

        vm.TopicHeader.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Localized("Resources_TopicHeader", "Latest on {0}"),
            TopicName));
    }

    [TestMethod]
    public async Task LoadAsync_WithATopicRequest_OpensTopicModeWithoutLoadingAnyTab()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);

        await vm.LoadAsync(new ResourcesTopicRequest(Slug, TopicName));

        vm.IsTopicActive.Should().BeTrue();
        vm.TopicResults.Items.Should().HaveCount(2);
        feedService.OverviewCallCount.Should().Be(0, "the deep link went straight to the topic");
        feedService.CallCounts.Values.Should().AllSatisfy(count => count.Should().Be(0));
    }

    [TestMethod]
    public async Task ShowTopicAsync_WhileSearching_LeavesSearchMode()
    {
        var feedService = TopicService();
        feedService.SetSearchPage("kefir", 1, Page(mayHaveMore: false, "kefir-1"));
        ResourcesViewModel vm = new(feedService);
        await vm.SubmitSearchCommand.ExecuteAsync("kefir");

        await vm.ShowTopicAsync(Slug, TopicName);

        vm.IsSearchActive.Should().BeFalse();
        vm.IsTopicActive.Should().BeTrue();
        vm.SearchQuery.Should().BeEmpty();
        vm.SearchResults.Items.Should().BeEmpty();
        vm.ActiveList.Should().BeSameAs(vm.TopicResults);
    }

    [TestMethod]
    public async Task SubmitSearchAsync_WhileATopicIsOpen_LeavesTopicMode()
    {
        var feedService = TopicService();
        feedService.SetSearchPage("kefir", 1, Page(mayHaveMore: false, "kefir-1"));
        ResourcesViewModel vm = new(feedService);
        await vm.ShowTopicAsync(Slug, TopicName);

        await vm.SubmitSearchCommand.ExecuteAsync("kefir");

        vm.IsTopicActive.Should().BeFalse();
        vm.TopicSlug.Should().BeEmpty();
        vm.TopicTitle.Should().BeEmpty();
        vm.TopicResults.Items.Should().BeEmpty();
        vm.IsSearchActive.Should().BeTrue();
        vm.ActiveList.Should().BeSameAs(vm.SearchResults);
    }

    [TestMethod]
    public async Task SelectTabAsync_WhileATopicIsOpen_LeavesTopicMode()
    {
        ResourcesViewModel vm = new(TopicService());
        await vm.ShowTopicAsync(Slug, TopicName);

        await vm.SelectTabCommand.ExecuteAsync("Podcast");

        vm.IsTopicActive.Should().BeFalse();
        vm.ShowTabs.Should().BeTrue();
        vm.SelectedTab.Should().BeSameAs(vm.Podcast);
        vm.ActiveList.Should().BeSameAs(vm.Podcast);
        vm.TopicResults.Items.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ClearTopicAsync_ReturnsToThePreviouslySelectedTabRatherThanLatest()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Videos");
        await vm.ShowTopicAsync(Slug, TopicName);

        await vm.ClearTopicCommand.ExecuteAsync(null);

        vm.SelectedTab.Should().BeSameAs(vm.Videos);
        vm.ActiveList.Should().BeSameAs(vm.Videos);
        vm.IsOverviewActive.Should().BeFalse();
        vm.ShowTabs.Should().BeTrue();
        vm.TopicSlug.Should().BeEmpty();
        vm.TopicResults.Items.Should().BeEmpty();
        feedService.CallCounts[FeedKind.Videos].Should().Be(1, "the tab was still loaded underneath the topic");
        feedService.OverviewCallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task ClearTopicAsync_AfterADeepLinkStraightIntoATopic_LoadsTheOverviewItLandsOn()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);
        await vm.LoadAsync(new ResourcesTopicRequest(Slug, TopicName));

        await vm.ClearTopicCommand.ExecuteAsync(null);

        vm.SelectedTab.Should().BeSameAs(vm.Latest);
        vm.IsOverviewActive.Should().BeTrue();
        vm.Latest.HasLoaded.Should().BeTrue();
        feedService.OverviewCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task LoadMoreAsync_InTopicMode_AppendsTheNextPageOfTheSameTopic()
    {
        var feedService = TopicService();
        feedService.SetTopicPage(Slug, 2, Page(mayHaveMore: false, "berries-3", "berries-4"));
        ResourcesViewModel vm = new(feedService);
        await vm.ShowTopicAsync(Slug, TopicName);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        vm.TopicResults.Items.Select(item => item.Title).Should()
            .Equal("berries-1", "berries-2", "berries-3", "berries-4");
        vm.TopicResults.HasMore.Should().BeFalse();
        feedService.TopicRequests.Should().Equal((Slug, 1), (Slug, 2));
        feedService.SearchRequests.Should().BeEmpty("topic paging must not fall through to search");
    }

    [TestMethod]
    public async Task RefreshAsync_InTopicMode_RefetchesTheSameTopicsFirstPage()
    {
        var feedService = TopicService();
        ResourcesViewModel vm = new(feedService);
        await vm.ShowTopicAsync(Slug, TopicName);

        await vm.RefreshCommand.ExecuteAsync(null);

        feedService.TopicRequests.Should().Equal((Slug, 1), (Slug, 1));
        vm.TopicResults.Items.Should().HaveCount(2, "a refresh replaces the page rather than appending it");
        vm.IsTopicActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task ShowTopicAsync_TopicWithNothingInIt_EndsEmptyRatherThanStuckLoading()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.ShowTopicAsync("empty-topic", "Empty");

        vm.IsTopicActive.Should().BeTrue();
        vm.TopicResults.Items.Should().BeEmpty();
        vm.TopicResults.IsLoading.Should().BeFalse();
        vm.TopicResults.HasLoaded.Should().BeTrue();
        vm.TopicResults.ShowEmptyState.Should().BeTrue();
        vm.TopicResults.HasMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task ShowTopicAsync_BlankSlug_StaysOnTheTabsAndAsksForNothing()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.ShowTopicAsync("   ", "Ghost");

        vm.IsTopicActive.Should().BeFalse();
        vm.ShowTabs.Should().BeTrue();
        vm.SelectedTab.Should().BeSameAs(vm.Latest);
        vm.Latest.HasLoaded.Should().BeTrue();
        feedService.TopicRequests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ShowTopicAsync_ServiceThrows_LeavesTheListEmptyAndDoesNotThrow()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new HttpRequestException("offline") };
        ResourcesViewModel vm = new(feedService);

        await vm.Invoking(v => v.ShowTopicAsync(Slug, TopicName)).Should().NotThrowAsync();

        vm.IsTopicActive.Should().BeTrue();
        vm.TopicResults.Items.Should().BeEmpty();
        vm.TopicResults.IsLoading.Should().BeFalse();
        vm.TopicResults.HasMore.Should().BeFalse();
        vm.TopicResults.EmptyStateText.Should()
            .Be(Localized("Resources_LoadError", "We couldn't load the newest posts."));
    }

    [TestMethod]
    public async Task SearchAndTopicMode_AreNeverBothActive()
    {
        var feedService = TopicService();
        feedService.SetSearchPage("kefir", 1, Page(mayHaveMore: false, "kefir-1"));
        ResourcesViewModel vm = new(feedService);

        await vm.SubmitSearchCommand.ExecuteAsync("kefir");
        (vm.IsSearchActive && vm.IsTopicActive).Should().BeFalse();

        await vm.ShowTopicAsync(Slug, TopicName);
        (vm.IsSearchActive && vm.IsTopicActive).Should().BeFalse();

        await vm.SubmitSearchCommand.ExecuteAsync("kefir");
        (vm.IsSearchActive && vm.IsTopicActive).Should().BeFalse();

        await vm.ClearTopicCommand.ExecuteAsync(null);
        (vm.IsSearchActive && vm.IsTopicActive).Should().BeFalse();
    }

    [TestMethod]
    public async Task ClearSearch_WhileATopicIsOpen_LeavesTheTopicAlone()
    {
        ResourcesViewModel vm = new(TopicService());
        await vm.ShowTopicAsync(Slug, TopicName);

        // The search box's own clear button fires this even when nothing was searched.
        vm.ClearSearchCommand.Execute(null);

        vm.IsTopicActive.Should().BeTrue();
        vm.ActiveList.Should().BeSameAs(vm.TopicResults);
        vm.TopicResults.Items.Should().HaveCount(2);
    }

    [TestMethod]
    public void TopicResults_IsATopicListAndNotASearchOne()
    {
        ResourcesViewModel vm = new(new FakeFeedService());

        vm.TopicResults.IsTopic.Should().BeTrue();
        vm.TopicResults.IsSearch.Should().BeFalse();
        vm.TopicResults.Kind.Should().BeNull();
        vm.TopicResults.TabAutomationId.Should().Be("ResourcesTopicResults");
        vm.Tabs.Should().NotContain(vm.TopicResults, "the topic list is not a tab");
    }

    [TestMethod]
    public async Task LoadTopicAsync_OnAFeedTab_DoesNothing()
    {
        FakeFeedService feedService = new();
        FeedListViewModel tab = new(feedService, FeedKind.Blog, "Blog");

        await tab.LoadTopicAsync(Slug, TopicName);

        feedService.TopicRequests.Should().BeEmpty();
        tab.Title.Should().Be("Blog");
        tab.HasLoaded.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadTopicAsync_RetitlesTheListToTheTopicItLoaded()
    {
        var feedService = TopicService();
        var list = FeedListViewModel.CreateTopic(feedService, string.Empty);

        await list.LoadTopicAsync(Slug, TopicName);

        list.Title.Should().Be(TopicName);
        list.Query.Should().Be(Slug);
    }
}
