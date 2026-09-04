using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class LatestViewModelTests
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

    private static async Task<FeedTabViewModel> LoadTabAsync(FeedResultStatus status, int itemCount)
    {
        FakeFeedService feedService = new();
        feedService.SetResponse(FeedKind.Blog, Result(FeedKind.Blog, status, itemCount, DateTimeOffset.UtcNow));
        FeedTabViewModel tab = new(feedService, FeedKind.Blog, "Blog");

        await tab.LoadAsync();

        return tab;
    }

    [TestMethod]
    public async Task LoadAsync_NoInitialKind_SelectsBlogAndLoadsOnlyBlog()
    {
        FakeFeedService feedService = new();
        LatestViewModel vm = new(feedService);

        await vm.LoadAsync();

        vm.SelectedTab.Should().BeSameAs(vm.Blog);
        feedService.CallCounts[FeedKind.Blog].Should().Be(1);
        feedService.CallCounts[FeedKind.Videos].Should().Be(0);
        feedService.CallCounts[FeedKind.Podcast].Should().Be(0);
    }

    [TestMethod]
    public async Task LoadAsync_WithInitialKind_SelectsThatTabAndLoadsIt()
    {
        FakeFeedService feedService = new();
        LatestViewModel vm = new(feedService);

        await vm.LoadAsync(FeedKind.Podcast);

        vm.SelectedTab.Should().BeSameAs(vm.Podcast);
        vm.Podcast.HasLoaded.Should().BeTrue();
        feedService.CallCounts[FeedKind.Podcast].Should().Be(1);
        feedService.CallCounts[FeedKind.Blog].Should().Be(0);
    }

    [TestMethod]
    public async Task SelectTabAsync_UnknownName_LeavesSelectionUnchanged()
    {
        FakeFeedService feedService = new();
        LatestViewModel vm = new(feedService);

        await vm.SelectTabCommand.ExecuteAsync("Newsletter");

        vm.SelectedTab.Should().BeSameAs(vm.Blog);
        feedService.CallCounts.Values.Should().AllSatisfy(count => count.Should().Be(0));
    }

    [TestMethod]
    public async Task SelectTabAsync_SecondVisitToATab_DoesNotRefetch()
    {
        FakeFeedService feedService = new();
        LatestViewModel vm = new(feedService);

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
        LatestViewModel vm = new(feedService);
        await vm.LoadAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        feedService.CallCounts[FeedKind.Blog].Should().Be(2);
        feedService.LastForceRefresh[FeedKind.Blog].Should().BeTrue();
        feedService.CallCounts[FeedKind.Videos].Should().Be(0);
        feedService.CallCounts[FeedKind.Podcast].Should().Be(0);
    }

    [TestMethod]
    public async Task IsBlogSelected_TracksSelectedTab()
    {
        FakeFeedService feedService = new();
        LatestViewModel vm = new(feedService);

        vm.IsBlogSelected.Should().BeTrue();
        vm.IsVideosSelected.Should().BeFalse();

        await vm.SelectTabCommand.ExecuteAsync("Videos");

        vm.IsBlogSelected.Should().BeFalse();
        vm.IsVideosSelected.Should().BeTrue();
        vm.IsPodcastSelected.Should().BeFalse();
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
        FeedTabViewModel tab = new(feedService, FeedKind.Blog, "Blog");

        await tab.Invoking(t => t.LoadAsync()).Should().NotThrowAsync();

        tab.IsLoading.Should().BeFalse();
        tab.HasLoaded.Should().BeTrue();
        tab.NoticeText.Should().NotBeEmpty();
        tab.ShowEmptyState.Should().BeTrue();
    }
}
