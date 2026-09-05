using DailyPlants.Helpers;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class LatestOverviewViewModelTests
{
    private static FeedItem NewItem(FeedKind kind, string id) => new()
    {
        Id = id,
        Kind = kind,
        Title = id,
        Link = $"https://nutritionfacts.org/{id}/",
        Summary = "Summary."
    };

    /// <summary>Three items per feed, so a test can tell "took two" from "took whatever came".</summary>
    private static FakeFeedService WithEveryFeedPopulated()
    {
        FakeFeedService feedService = new()
        {
            OverviewGroups = FeedKinds.Feeds
                .Select(kind => new FeedGroup(
                    kind,
                    [NewItem(kind, $"{kind}-1"), NewItem(kind, $"{kind}-2"), NewItem(kind, $"{kind}-3")]))
                .ToList()
        };

        return feedService;
    }

    /// <summary>Mirrors the ViewModels' own resource lookup so the assertions hold in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    [TestMethod]
    public async Task LoadAsync_EveryFeedPopulated_ShowsTwoItemsPerSectionInTabOrder()
    {
        var feedService = WithEveryFeedPopulated();
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();

        vm.Latest.Groups.Select(group => group.Kind).Should().Equal(FeedKinds.Feeds);
        vm.Latest.Groups.Should().AllSatisfy(group => group.Items.Should().HaveCount(LatestOverviewViewModel.ItemsPerSection));
        vm.Latest.ShowItems.Should().BeTrue();
        vm.Latest.ShowEmptyState.Should().BeFalse();
        vm.Latest.ShowNotice.Should().BeFalse();
        vm.Latest.IsLoading.Should().BeFalse();
        vm.Latest.HasLoaded.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_AFeedThatFailed_IsNotRenderedAsAnEmptySection()
    {
        FakeFeedService feedService = new()
        {
            OverviewGroups =
            [
                new FeedGroup(FeedKind.Blog, [NewItem(FeedKind.Blog, "b1")]),
                new FeedGroup(FeedKind.Videos, []),
                new FeedGroup(FeedKind.Podcast, [NewItem(FeedKind.Podcast, "p1")])
            ]
        };
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();

        vm.Latest.Groups.Select(group => group.Kind).Should().Equal(FeedKind.Blog, FeedKind.Podcast);
        vm.Latest.ShowEmptyState.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_EverySectionEmpty_FallsBackToTheEmptyState()
    {
        FakeFeedService feedService = new()
        {
            OverviewGroups = FeedKinds.Feeds.Select(kind => new FeedGroup(kind, [])).ToList()
        };
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();

        vm.Latest.Groups.Should().BeEmpty();
        vm.Latest.ShowItems.Should().BeFalse();
        vm.Latest.ShowEmptyState.Should().BeTrue();
        vm.Latest.EmptyStateText.Should().Be(Localized("Resources_Empty", "Nothing here yet"));
        vm.Latest.NoticeText.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadAsync_NothingToShowOnAHeadThatCannotFetch_SaysSoInsteadOfNothingHereYet()
    {
        FakeFeedService feedService = new() { SupportsLiveFetch = false };
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();

        vm.Latest.ShowEmptyState.Should().BeTrue();
        vm.Latest.EmptyStateText.Should().Be(
            Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app."));
    }

    [TestMethod]
    public async Task LoadAsync_ServiceThrows_ShowsANoticeAndDoesNotThrow()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new InvalidOperationException("boom") };
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.Invoking(overview => overview.LoadAsync()).Should().NotThrowAsync();

        vm.Latest.IsLoading.Should().BeFalse();
        vm.Latest.HasLoaded.Should().BeTrue();
        vm.Latest.NoticeText.Should().NotBeEmpty();
        vm.Latest.ShowEmptyState.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_ForceRefresh_GetsPastTheFreshnessWindowOnEveryFeed()
    {
        var feedService = WithEveryFeedPopulated();
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync(forceRefresh: true);

        feedService.LastForceRefresh.Keys.Should().BeEquivalentTo(FeedKinds.Feeds);
        feedService.LastForceRefresh.Values.Should().AllSatisfy(forced => forced.Should().BeTrue());
        feedService.OverviewCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task LoadAsync_Twice_ReplacesTheSectionsRatherThanAppending()
    {
        var feedService = WithEveryFeedPopulated();
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();
        await vm.Latest.LoadAsync();

        vm.Latest.Groups.Should().HaveCount(FeedKinds.Feeds.Count);
    }

    [TestMethod]
    public async Task Groups_CarryTheirTabLabelAndHeadingAutomationId()
    {
        var feedService = WithEveryFeedPopulated();
        ResourcesViewModel vm = new(feedService);

        await vm.Latest.LoadAsync();

        vm.Latest.Groups.Select(group => group.HeadingAutomationId).Should().Equal(
            "ResourcesOverviewHeadingBlog",
            "ResourcesOverviewHeadingVideos",
            "ResourcesOverviewHeadingPodcast",
            "ResourcesOverviewHeadingRecipes",
            "ResourcesOverviewHeadingQuestions",
            "ResourcesOverviewHeadingWebinars");

        foreach (var group in vm.Latest.Groups)
        {
            var tab = vm.Tabs.Single(candidate => candidate.Kind == group.Kind);
            group.Title.Should().Be(tab.Title, "a heading and its tab are the same thing named twice");
        }
    }

    [TestMethod]
    public void TabSurface_IsTheOverviewsOwnIdentity()
    {
        FakeFeedService feedService = new();
        ResourcesViewModel vm = new(feedService);

        vm.Latest.Kind.Should().BeNull();
        vm.Latest.TabAutomationId.Should().Be("ResourcesTabLatestButton");
        vm.Latest.Title.Should().Be(Localized("Resources_TabLatest", "Latest"));
    }

    [DataTestMethod]
    [DataRow(FeedKind.Blog)]
    [DataRow(FeedKind.Videos)]
    [DataRow(FeedKind.Podcast)]
    [DataRow(FeedKind.Recipes)]
    [DataRow(FeedKind.Questions)]
    [DataRow(FeedKind.Webinars)]
    public async Task SelectSectionCommand_TappingAHeading_OpensThatSectionsTab(FeedKind kind)
    {
        var feedService = WithEveryFeedPopulated();
        ResourcesViewModel vm = new(feedService);
        await vm.Latest.LoadAsync();

        var group = vm.Latest.Groups.Single(candidate => candidate.Kind == kind);
        await group.SelectSectionCommand.ExecuteAsync(kind);

        vm.SelectedTab.Should().BeSameAs(vm.Tabs.Single(tab => tab.Kind == kind));
        vm.IsOverviewActive.Should().BeFalse();
        feedService.CallCounts[kind].Should().Be(1, "the tab loads when it is first opened");
    }
}
