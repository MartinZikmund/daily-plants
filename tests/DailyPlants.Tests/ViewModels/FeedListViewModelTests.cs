using DailyPlants.Helpers;
using DailyPlants.Tests.TestDoubles;
using DailyPlants.ViewModels;

namespace DailyPlants.Tests.ViewModels;

[TestClass]
public class FeedListViewModelTests
{
    private const string Query = "beans";

    private static FeedItem NewItem(string id, FeedKind kind = FeedKind.Blog) => new()
    {
        Id = id,
        Kind = kind,
        Title = id,
        Link = $"https://nutritionfacts.org/blog/{id}/",
        Summary = "Summary."
    };

    private static FeedPage Page(bool mayHaveMore, params string[] ids)
        => new(ids.Select(id => NewItem(id)).ToList(), FeedResultStatus.Fresh, mayHaveMore);

    /// <summary>A tab whose first page is loaded through GetFeedAsync, exactly as the page does it.</summary>
    private static async Task<FeedListViewModel> LoadedTabAsync(FakeFeedService feedService, params string[] pageOneIds)
    {
        feedService.SetResponse(
            FeedKind.Blog,
            new FeedResult(FeedKind.Blog, pageOneIds.Select(id => NewItem(id)).ToList(), FeedResultStatus.Fresh, DateTimeOffset.UtcNow));

        FeedListViewModel list = new(feedService, FeedKind.Blog, "Blog");
        await list.LoadAsync();

        return list;
    }

    private static async Task<FeedListViewModel> SearchedAsync(FakeFeedService feedService, FeedPage firstPage)
    {
        feedService.SetSearchPage(Query, 1, firstPage);

        var list = FeedListViewModel.CreateSearch(feedService, "Search");
        await list.SearchAsync(Query);

        return list;
    }

    /// <summary>Mirrors the ViewModels' own resource lookup so the assertions hold in any locale.</summary>
    private static string Localized(string key, string fallback)
    {
        var value = Localizer.GetString(key);
        return value == $"[{key}]" ? fallback : value;
    }

    [TestMethod]
    public async Task LoadMoreAsync_AppendsTheNextPageAfterTheFirst()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a", "b");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "c", "d"));

        await list.LoadMoreAsync();

        list.Items.Select(item => item.Item.Id).Should().Equal("a", "b", "c", "d");
        list.HasMore.Should().BeTrue();
        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2));
    }

    [TestMethod]
    public async Task LoadMoreAsync_PageRepeatsAKnownItem_AppendsOnlyTheNewOnes()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a", "b");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "b", "c"));

        await list.LoadMoreAsync();

        list.Items.Select(item => item.Item.Id).Should().Equal("a", "b", "c");
        list.HasMore.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadMoreAsync_PageOfOnlyKnownItems_EndsTheList()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a", "b");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "a", "b"));

        await list.LoadMoreAsync();
        await list.LoadMoreAsync();

        list.Items.Should().HaveCount(2);
        list.HasMore.Should().BeFalse("a page that contributes nothing new is the end of the list");
        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2));
    }

    [TestMethod]
    public async Task LoadMoreAsync_EmptyPage_EndsTheList()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.SetPage(FeedKind.Blog, 2, FeedPage.End(FeedResultStatus.Fresh));

        await list.LoadMoreAsync();

        list.Items.Should().HaveCount(1);
        list.HasMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadMoreAsync_FailedPage_KeepsWhatIsOnScreenAndEndsTheList()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a", "b");
        feedService.SetPage(FeedKind.Blog, 2, FeedPage.End(FeedResultStatus.Unavailable));

        await list.LoadMoreAsync();

        list.Items.Should().HaveCount(2);
        list.HasMore.Should().BeFalse();
        list.IsLoadingMore.Should().BeFalse();
        list.ShowEmptyState.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadMoreAsync_WhenTheListHasEnded_MakesNoRequest()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.SetPage(FeedKind.Blog, 2, FeedPage.End(FeedResultStatus.Fresh));

        await list.LoadMoreAsync();
        await list.LoadMoreAsync();
        await list.LoadMoreAsync();

        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2));
    }

    [TestMethod]
    public async Task LoadMoreAsync_FirstPageWasOffline_NeverOffersMore()
    {
        FakeFeedService feedService = new();
        feedService.SetResponse(
            FeedKind.Blog,
            new FeedResult(FeedKind.Blog, [NewItem("a")], FeedResultStatus.Stale, DateTimeOffset.UtcNow));
        FeedListViewModel list = new(feedService, FeedKind.Blog, "Blog");
        await list.LoadAsync();

        await list.LoadMoreAsync();

        list.HasMore.Should().BeFalse("a stale first page means the network is down; more can only fail");
        feedService.PageRequests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadMoreAsync_CalledTwiceConcurrently_FetchesOnePage()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "b"));
        feedService.Pause();

        var first = list.LoadMoreAsync();
        var second = list.LoadMoreAsync();

        list.IsLoadingMore.Should().BeTrue();
        list.ShowLoadingMore.Should().BeTrue();
        feedService.Resume();
        await Task.WhenAll(first, second);

        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2));
        list.Items.Should().HaveCount(2);
        list.IsLoadingMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task LoadMoreAsync_ServiceThrows_DoesNotThrowAndEndsTheList()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.ExceptionToThrow = new InvalidOperationException("boom");

        await list.Invoking(l => l.LoadMoreAsync()).Should().NotThrowAsync();

        list.HasMore.Should().BeFalse();
        list.IsLoadingMore.Should().BeFalse();
        list.Items.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task LoadAsync_AfterPaging_StartsOverAtPageOne()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a", "b");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "c"));
        await list.LoadMoreAsync();

        await list.LoadAsync(forceRefresh: true);

        list.Items.Select(item => item.Item.Id).Should().Equal("a", "b");
        list.HasMore.Should().BeTrue("a refresh re-opens the list");

        await list.LoadMoreAsync();

        feedService.PageRequests.Should().Equal((FeedKind.Blog, 2), (FeedKind.Blog, 2));
        list.Items.Select(item => item.Item.Id).Should().Equal("a", "b", "c");
    }

    [TestMethod]
    public async Task LoadAsync_AfterARefresh_ForgetsIdsFromTheOldPages()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.SetPage(FeedKind.Blog, 2, Page(mayHaveMore: true, "b"));
        await list.LoadMoreAsync();

        // The refresh brings back what used to be page 2 - it must not be deduped away.
        feedService.SetResponse(
            FeedKind.Blog,
            new FeedResult(FeedKind.Blog, [NewItem("b"), NewItem("a")], FeedResultStatus.Fresh, DateTimeOffset.UtcNow));
        await list.LoadAsync(forceRefresh: true);

        list.Items.Select(item => item.Item.Id).Should().Equal("b", "a");
    }

    [TestMethod]
    public void CreateSearch_HasNoKind()
    {
        FakeFeedService feedService = new();

        var list = FeedListViewModel.CreateSearch(feedService, "Search");

        list.Kind.Should().BeNull();
        list.IsSearch.Should().BeTrue();
        list.Query.Should().BeEmpty();
        list.HasMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task SearchAsync_LoadsTheFirstPageAndRemembersTheQuery()
    {
        FakeFeedService feedService = new();

        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a", "b"));

        list.Query.Should().Be(Query);
        list.Items.Should().HaveCount(2);
        list.HasLoaded.Should().BeTrue();
        list.HasMore.Should().BeTrue();
        list.NoticeText.Should().BeEmpty();
        feedService.SearchRequests.Should().Equal((Query, 1));
    }

    [TestMethod]
    public async Task SearchAsync_TrimsTheQuery()
    {
        FakeFeedService feedService = new();
        feedService.SetSearchPage(Query, 1, Page(mayHaveMore: false, "a"));
        var list = FeedListViewModel.CreateSearch(feedService, "Search");

        await list.SearchAsync($"  {Query}\t");

        list.Query.Should().Be(Query);
        list.Items.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task SearchAsync_BlankQuery_ClearsInsteadOfSearching()
    {
        FakeFeedService feedService = new();
        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a"));

        await list.SearchAsync("   ");

        list.Items.Should().BeEmpty();
        list.Query.Should().BeEmpty();
        list.HasMore.Should().BeFalse();
        list.HasLoaded.Should().BeFalse("an empty box is not an empty result");
        list.ShowEmptyState.Should().BeFalse();
        feedService.SearchRequests.Should().Equal((Query, 1));
    }

    [TestMethod]
    public async Task SearchAsync_NoResults_NamesTheQueryInTheEmptyState()
    {
        FakeFeedService feedService = new();

        var list = await SearchedAsync(feedService, FeedPage.End(FeedResultStatus.Fresh));

        list.ShowEmptyState.Should().BeTrue();
        list.EmptyStateText.Should().Contain(Query);
        list.NoticeText.Should().BeEmpty();
        list.HasMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task SearchAsync_Offline_SaysSearchNeedsAConnection()
    {
        FakeFeedService feedService = new();

        var unavailable = await SearchedAsync(feedService, FeedPage.End(FeedResultStatus.Unavailable));

        unavailable.EmptyStateText.Should().Be(
            Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org."));
        unavailable.EmptyStateText.Should().NotBe(Localized("Resources_LoadError", "We couldn't load the newest posts."));
        unavailable.ShowEmptyState.Should().BeTrue();
        unavailable.NoticeText.Should().BeEmpty("with nothing on screen the empty state carries the message");
    }

    [TestMethod]
    public async Task SearchAsync_OnWasm_SaysTheBrowserCannotFetchRatherThanBlamingTheConnection()
    {
        FakeFeedService feedService = new() { SupportsLiveFetch = false };

        var browser = await SearchedAsync(feedService, FeedPage.End(FeedResultStatus.LiveFetchUnavailable));

        // The browser head has a connection; CORS is what stops it, so the offline wording would lie.
        browser.EmptyStateText.Should().Be(
            Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app."));
        browser.EmptyStateText.Should().NotBe(
            Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org."));
    }

    [TestMethod]
    public async Task SearchAsync_OnATabList_DoesNothing()
    {
        FakeFeedService feedService = new();
        FeedListViewModel list = new(feedService, FeedKind.Blog, "Blog");

        await list.SearchAsync(Query);

        feedService.SearchRequests.Should().BeEmpty();
        list.Query.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SearchAsync_ServiceThrows_DoesNotThrow()
    {
        FakeFeedService feedService = new() { ExceptionToThrow = new InvalidOperationException("boom") };
        var list = FeedListViewModel.CreateSearch(feedService, "Search");

        await list.Invoking(l => l.SearchAsync(Query)).Should().NotThrowAsync();

        list.IsLoading.Should().BeFalse();
        list.HasLoaded.Should().BeTrue();
        list.HasMore.Should().BeFalse();
        list.EmptyStateText.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task LoadMoreAsync_SearchMode_AsksForTheNextPageOfTheSameQuery()
    {
        FakeFeedService feedService = new();
        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a"));
        feedService.SetSearchPage(Query, 2, Page(mayHaveMore: true, "b"));

        await list.LoadMoreAsync();

        feedService.SearchRequests.Should().Equal((Query, 1), (Query, 2));
        list.Items.Select(item => item.Item.Id).Should().Equal("a", "b");
    }

    [TestMethod]
    public async Task LoadMoreAsync_SearchModeWithNoQuery_MakesNoRequest()
    {
        FakeFeedService feedService = new();
        var list = FeedListViewModel.CreateSearch(feedService, "Search");

        // HasMore is only ever true after a query, so force the guard to be the thing under test.
        list.HasMore = true;
        await list.LoadMoreAsync();

        feedService.SearchRequests.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadMoreAsync_SearchGoesOffline_NoticesInsteadOfLosingTheResults()
    {
        FakeFeedService feedService = new();
        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a"));
        feedService.SetSearchPage(Query, 2, FeedPage.End(FeedResultStatus.Unavailable));

        await list.LoadMoreAsync();

        list.Items.Should().HaveCount(1);
        list.HasMore.Should().BeFalse();
        list.ShowNotice.Should().BeTrue();
        list.NoticeText.Should().Be(
            Localized("Resources_SearchOffline", "Search needs a connection to NutritionFacts.org."));
    }

    [TestMethod]
    public async Task LoadAsync_SearchMode_RerunsTheCurrentQueryFromPageOne()
    {
        FakeFeedService feedService = new();
        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a"));
        feedService.SetSearchPage(Query, 2, Page(mayHaveMore: true, "b"));
        await list.LoadMoreAsync();

        await list.LoadAsync(forceRefresh: true);

        feedService.SearchRequests.Should().Equal((Query, 1), (Query, 2), (Query, 1));
        list.Items.Select(item => item.Item.Id).Should().Equal("a");
    }

    [TestMethod]
    public async Task LoadMoreAsync_OnWasm_SaysTheBrowserCannotFetchRatherThanBlamingTheNetwork()
    {
        FakeFeedService feedService = new();
        var list = await LoadedTabAsync(feedService, "a");
        feedService.SetPage(FeedKind.Blog, 2, FeedPage.End(FeedResultStatus.LiveFetchUnavailable));

        await list.LoadMoreAsync();

        // The same status on page 1 says this, so page 2 must not contradict it.
        list.NoticeText.Should().Be(
            Localized("Resources_BrowserUnavailable", "New posts can't be fetched in the browser version of the app."));
        list.NoticeText.Should().NotBe(Localized("Resources_LoadError", "We couldn't load the newest posts."));
        list.Items.Should().HaveCount(1);
        list.HasMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task Clear_EmptiesTheListAndItsPagingState()
    {
        FakeFeedService feedService = new();
        var list = await SearchedAsync(feedService, Page(mayHaveMore: true, "a"));

        list.Clear();

        list.Items.Should().BeEmpty();
        list.Query.Should().BeEmpty();
        list.HasMore.Should().BeFalse();
        list.HasLoaded.Should().BeFalse();
        list.ShowItems.Should().BeFalse();
        list.ShowEmptyState.Should().BeFalse();
        list.NoticeText.Should().BeEmpty();
    }
}
