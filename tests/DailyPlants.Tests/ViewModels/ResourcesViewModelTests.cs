using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class ResourcesViewModelTests
{
    private static FeedItem NewItem(FeedKind kind, string id) => new()
    {
        Id = id,
        Kind = kind,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary."
    };

    private static FeedResult Result(
        FeedKind kind,
        FeedResultStatus status,
        int itemCount = 1,
        DateTimeOffset? fetchedAt = null)
    {
        List<FeedItem> items = new();
        for (var i = 0; i < itemCount; i++)
        {
            items.Add(NewItem(kind, $"{kind}-{i}"));
        }

        return new FeedResult(kind, items, status, fetchedAt);
    }

    private static async Task<FeedListViewModel> LoadTabAsync(FeedResultStatus status, int itemCount)
    {
        FakeFeedService feedService = new();
        feedService.SetResponse(FeedKind.Blog, Result(FeedKind.Blog, status, itemCount, DateTimeOffset.UtcNow));
        FeedListViewModel tab = new(feedService, FeedKind.Blog, "Blog");

        await tab.LoadAsync();

        return tab;
    }

    [TestMethod]
    public async Task LoadAsync_NoInitialKind_SelectsLatestAndLoadsOnlyTheOverview()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.LoadAsync();

        vm.SelectedTab.Should().BeSameAs(vm.Latest);
        vm.IsOverviewActive.Should().BeTrue();
        vm.Latest.HasLoaded.Should().BeTrue();
        feedService.OverviewCallCount.Should().Be(1);
        feedService.CallCounts.Values.Should().AllSatisfy(count => count.Should().Be(0));
    }

    [TestMethod]
    public async Task LoadAsync_WithInitialKind_SelectsThatTabAndLoadsIt()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.LoadAsync(FeedKind.Podcast);

        vm.SelectedTab.Should().BeSameAs(vm.Podcast);
        vm.Podcast.HasLoaded.Should().BeTrue();
        feedService.CallCounts[FeedKind.Podcast].Should().Be(1);
        feedService.CallCounts[FeedKind.Blog].Should().Be(0);
        feedService.OverviewCallCount.Should().Be(0, "the deep link skipped the overview entirely");
    }

    [TestMethod]
    public async Task SelectTabAsync_UnknownName_LeavesSelectionUnchanged()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync("Newsletter");

        vm.SelectedTab.Should().BeSameAs(vm.Latest);
        feedService.CallCounts.Values.Should().AllSatisfy(count => count.Should().Be(0));
        feedService.OverviewCallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task SelectTabAsync_SecondVisitToATab_DoesNotRefetch()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync("Videos");
        await vm.SelectTabCommand.ExecuteAsync("Blog");
        await vm.SelectTabCommand.ExecuteAsync("Videos");

        feedService.CallCounts[FeedKind.Videos].Should().Be(1);
        feedService.CallCounts[FeedKind.Blog].Should().Be(1);
    }

    [TestMethod]
    public async Task RefreshAsync_ForcesRefreshOfSelectedTabOnly()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Blog");

        await vm.RefreshCommand.ExecuteAsync(null);

        feedService.CallCounts[FeedKind.Blog].Should().Be(2);
        feedService.LastForceRefresh[FeedKind.Blog].Should().BeTrue();
        feedService.CallCounts[FeedKind.Videos].Should().Be(0);
        feedService.CallCounts[FeedKind.Podcast].Should().Be(0);
    }

    [TestMethod]
    public async Task RefreshAsync_OnTheOverview_RefreshesTheOverview()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);
        await vm.LoadAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        feedService.OverviewCallCount.Should().Be(2);
        feedService.LastForceRefresh.Should().NotBeEmpty("a forced overview has to get past the freshness window");
        feedService.LastForceRefresh.Values.Should().AllSatisfy(forced => forced.Should().BeTrue());
    }

    [TestMethod]
    public void Tabs_AreLatestThenEveryFeedInDisplayOrder()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        vm.Tabs.Select(tab => tab.Kind).Should().Equal(
            (FeedKind?)null,
            FeedKind.Blog,
            FeedKind.Videos,
            FeedKind.Podcast,
            FeedKind.Recipes,
            FeedKind.Questions,
            FeedKind.Webinars);

        vm.Tabs[0].Should().BeSameAs(vm.Latest);
        vm.SelectedTab.Should().BeSameAs(vm.Latest, "the overview is the tab the page opens on");
    }

    /// <summary>
    /// UI automation drives this page by these ids, and three of them predate the seventh tab.
    /// They are a contract, not a naming convention, so they are asserted literally.
    /// </summary>
    [TestMethod]
    public void TabAutomationId_KeepsTheIdsUiAutomationAlreadyUses()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        vm.Tabs.Select(tab => tab.TabAutomationId).Should().Equal(
            "ResourcesTabLatestButton",
            "ResourcesTabBlogButton",
            "ResourcesTabVideosButton",
            "ResourcesTabPodcastButton",
            "ResourcesTabRecipesButton",
            "ResourcesTabQuestionsButton",
            "ResourcesTabWebinarsButton");
    }

    [TestMethod]
    public async Task SelectTab_LeavesExactlyOneTabSelected()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        vm.Tabs.Where(tab => tab.IsSelected).Should().ContainSingle().Which.Should().BeSameAs(vm.Latest);

        await vm.SelectTabCommand.ExecuteAsync("Webinars");

        vm.Tabs.Where(tab => tab.IsSelected).Should().ContainSingle().Which.Should().BeSameAs(vm.Webinars);
        vm.Latest.IsSelected.Should().BeFalse();
    }

    [TestMethod]
    public async Task SelectTabAsync_TabInstance_SelectsIt()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync(vm.Questions);

        vm.SelectedTab.Should().BeSameAs(vm.Questions);
        feedService.CallCounts[FeedKind.Questions].Should().Be(1);
    }

    [TestMethod]
    public async Task SelectTabAsync_FeedKind_SelectsThatFeedsTab()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync(FeedKind.Recipes);

        vm.SelectedTab.Should().BeSameAs(vm.Recipes);
    }

    [TestMethod]
    public async Task SelectTabAsync_KindWithNoFeed_IsIgnored()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync(FeedKind.Other);

        vm.SelectedTab.Should().BeSameAs(vm.Latest);
        feedService.CallCounts.Values.Should().AllSatisfy(count => count.Should().Be(0));
    }

    /// <summary>
    /// Seven tabs is seven fetches if they are eager. Opening the page must cost the overview and
    /// nothing else, and the first tab you pick must cost only itself.
    /// </summary>
    [TestMethod]
    public async Task SelectTab_AfterOpeningThePage_FetchesOnlyThatTab()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);
        await vm.LoadAsync();

        await vm.SelectTabCommand.ExecuteAsync("Blog");

        feedService.OverviewCallCount.Should().Be(1);
        feedService.CallCounts[FeedKind.Blog].Should().Be(1);
        feedService.CallCounts.Where(entry => entry.Key != FeedKind.Blog)
            .Should().AllSatisfy(entry => entry.Value.Should().Be(0, $"{entry.Key} was never opened"));
    }

    [TestMethod]
    public async Task LoadAsync_StaleResult_SetsOfflineNotice()
    {
        var tab = await LoadTabAsync(FeedResultStatus.Stale, itemCount: 2);

        tab.Items.Should().HaveCount(2);
        tab.NoticeText.Should().NotBeEmpty();
        tab.ShowNotice.Should().BeTrue();
        tab.ShowItems.Should().BeTrue();
        tab.ShowEmptyState.Should().BeFalse();
        tab.UpdatedText.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task LoadAsync_UnavailableResult_ShowsEmptyStateAndNoNotice()
    {
        var unavailable = await LoadTabAsync(FeedResultStatus.Unavailable, itemCount: 0);
        var fresh = await LoadTabAsync(FeedResultStatus.Fresh, itemCount: 0);

        unavailable.NoticeText.Should().BeEmpty();
        unavailable.ShowNotice.Should().BeFalse();
        unavailable.ShowEmptyState.Should().BeTrue();
        unavailable.EmptyStateText.Should().NotBeEmpty().And.NotBe(fresh.EmptyStateText);
    }

    [TestMethod]
    public async Task LoadAsync_LiveFetchUnavailable_ShowsBrowserMessage()
    {
        var browser = await LoadTabAsync(FeedResultStatus.LiveFetchUnavailable, itemCount: 0);
        var unavailable = await LoadTabAsync(FeedResultStatus.Unavailable, itemCount: 0);
        var fresh = await LoadTabAsync(FeedResultStatus.Fresh, itemCount: 0);

        browser.NoticeText.Should().BeEmpty();
        browser.ShowEmptyState.Should().BeTrue();
        browser.EmptyStateText.Should().NotBeEmpty()
            .And.NotBe(unavailable.EmptyStateText)
            .And.NotBe(fresh.EmptyStateText);
    }

    [TestMethod]
    public async Task LoadAsync_Failure_LeavesIsLoadingFalse()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new InvalidOperationException("boom") };
        FeedListViewModel tab = new(feedService, FeedKind.Blog, "Blog");

        await tab.Invoking(t => t.LoadAsync()).Should().NotThrowAsync();

        tab.IsLoading.Should().BeFalse();
        tab.HasLoaded.Should().BeTrue();
        tab.NoticeText.Should().NotBeEmpty();
        tab.ShowEmptyState.Should().BeTrue();
    }

    private static FakeFeedService SearchingFor(string query, params string[] ids)
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage(
            query,
            1,
            new FeedPage(ids.Select(id => NewItem(FeedKind.Other, id)).ToList(), FeedResultStatus.Fresh, false));

        return feedService;
    }

    [TestMethod]
    public async Task ActiveList_FollowsTheSelectedTab()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        vm.ActiveList.Should().BeNull("the overview is not a paginated list");
        vm.IsSearchActive.Should().BeFalse();
        vm.SearchResults.Kind.Should().BeNull();

        await vm.SelectTabCommand.ExecuteAsync("Blog");

        vm.ActiveList.Should().BeSameAs(vm.Blog);
        vm.IsOverviewActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task ActiveList_FollowsTheSelectedTab_AndAnnouncesIt()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.SelectTabCommand.ExecuteAsync("Videos");

        vm.ActiveList.Should().BeSameAs(vm.Videos);
        changed.Should().Contain(nameof(ResourcesViewModel.ActiveList));
    }

    [TestMethod]
    public async Task SubmitSearchAsync_ActivatesSearchAndAnnouncesTheNewActiveList()
    {
        var feedService = SearchingFor("beans", "one", "two");
        ResourcesViewModel vm = new(feedService);
        await vm.LoadAsync();
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        vm.IsSearchActive.Should().BeTrue();
        vm.ActiveList.Should().BeSameAs(vm.SearchResults);
        vm.SearchResults.Items.Should().HaveCount(2);
        changed.Should().Contain(nameof(ResourcesViewModel.ActiveList));
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task SubmitSearchAsync_TrimsTheQuery()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);

        await vm.SubmitSearchCommand.ExecuteAsync("  beans  ");

        vm.SearchQuery.Should().Be("beans");
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task SubmitSearchAsync_NoArgument_UsesWhatIsInTheBox()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService) { SearchQuery = "beans" };

        await vm.SubmitSearchCommand.ExecuteAsync(null);

        vm.IsSearchActive.Should().BeTrue();
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task SubmitSearchAsync_BlankQuery_ClearsInsteadOfSearching()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Blog");
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        await vm.SubmitSearchCommand.ExecuteAsync("   ");

        vm.IsSearchActive.Should().BeFalse();
        vm.SearchQuery.Should().BeEmpty();
        vm.SearchResults.Items.Should().BeEmpty();
        vm.ActiveList.Should().BeSameAs(vm.Blog);
        feedService.SearchRequests.Should().Equal(("beans", 1));
    }

    [TestMethod]
    public async Task ClearSearch_PutsTheTabsBack()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Blog");
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        vm.ClearSearchCommand.Execute(null);

        vm.IsSearchActive.Should().BeFalse();
        vm.SearchQuery.Should().BeEmpty();
        vm.SearchResults.Items.Should().BeEmpty();
        vm.SearchResults.Query.Should().BeEmpty();
        vm.ActiveList.Should().BeSameAs(vm.SelectedTab);
    }

    [TestMethod]
    public async Task SelectTab_WhileSearching_LeavesTheResults()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        await vm.SelectTabCommand.ExecuteAsync("Podcast");

        vm.IsSearchActive.Should().BeFalse();
        vm.ActiveList.Should().BeSameAs(vm.Podcast);
    }

    [TestMethod]
    public async Task RefreshAsync_WhileSearching_RerunsTheSearchAndLeavesTheTabsAlone()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Blog");
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        await vm.RefreshCommand.ExecuteAsync(null);

        feedService.SearchRequests.Should().Equal(("beans", 1), ("beans", 1));
        feedService.CallCounts[FeedKind.Blog].Should().Be(1, "the tab was not the list on screen");
    }

    [TestMethod]
    public async Task LoadMoreAsync_GoesToTheListOnScreen()
    {
        var feedService = SearchingFor("beans", "one");
        ResourcesViewModel vm = new(feedService);
        feedService.SetSearchPage(
            "beans",
            1,
            new FeedPage([NewItem(FeedKind.Other, "one")], FeedResultStatus.Fresh, true));
        feedService.SetSearchPage(
            "beans",
            2,
            new FeedPage([NewItem(FeedKind.Other, "two")], FeedResultStatus.Fresh, false));
        await vm.SubmitSearchCommand.ExecuteAsync("beans");

        await vm.LoadMoreCommand.ExecuteAsync(null);

        vm.SearchResults.Items.Should().HaveCount(2);
        feedService.SearchRequests.Should().Equal(("beans", 1), ("beans", 2));
    }

    [TestMethod]
    public async Task LoadMoreAsync_OnTheOverview_DoesNothing()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);
        await vm.LoadAsync();

        await vm.LoadMoreCommand.ExecuteAsync(null);

        feedService.PageRequests.Should().BeEmpty();
        feedService.SearchRequests.Should().BeEmpty();
    }

    /// <summary>
    /// The scroll handler fires LoadMoreCommand.Execute on every ViewChanged, so the guard has to
    /// hold against the fire-and-forget command, not just against an awaited LoadMoreAsync.
    /// </summary>
    [TestMethod]
    public async Task LoadMoreCommand_FiredRepeatedlyByScrolling_FetchesOnePage()
    {
        FakeFeedService feedService = new();
        feedService.SetResponse(FeedKind.Blog, Result(FeedKind.Blog, FeedResultStatus.Fresh, itemCount: 1));
        feedService.SetPage(FeedKind.Blog, 2, new FeedPage([NewItem(FeedKind.Blog, "two")], FeedResultStatus.Fresh, false));

        ResourcesViewModel vm = new(feedService);
        await vm.SelectTabCommand.ExecuteAsync("Blog");

        // Hold page 2 open so the burst of scroll events lands while it is still in flight.
        feedService.Pause();
        for (var i = 0; i < 10; i++)
        {
            vm.LoadMoreCommand.Execute(null);
        }

        feedService.Resume();
        await vm.LoadMoreCommand.ExecutionTask!;

        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2));
        vm.Blog.Items.Should().HaveCount(2);
        vm.Blog.IsLoadingMore.Should().BeFalse();
    }
}
