using System.Net.Http;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class FeedServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 9, 3, 15, 0, 0, TimeSpan.Zero);

    private FakeHttpMessageHandler _handler = null!;
    private InMemoryFeedCache _cache = null!;
    private TestTimeProvider _clock = null!;

    [TestInitialize]
    public void Initialize()
    {
        _handler = new FakeHttpMessageHandler();
        _cache = new InMemoryFeedCache();
        _clock = new TestTimeProvider(Now);
    }

    private FeedService CreateService() => new(new HttpClient(_handler), _cache, _clock);

    private static string Load(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static CachedFeed CachedBlog(DateTimeOffset fetchedAt) => new()
    {
        Kind = FeedKind.Blog,
        FetchedAt = fetchedAt,
        Items = [new FeedItem { Id = "cached-1", Kind = FeedKind.Blog, Title = "A Saved Post" }]
    };

    [TestMethod]
    public async Task GetFeedAsync_NoCache_FetchesAndReturnsFresh()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));

        var result = await CreateService().GetFeedAsync(FeedKind.Blog);

        result.Status.Should().Be(FeedResultStatus.Fresh);
        result.Kind.Should().Be(FeedKind.Blog);
        result.Items.Should().HaveCount(3);
        result.HasItems.Should().BeTrue();
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetFeedAsync_CacheWithinOneHour_ReturnsCachedWithoutHttpCall()
    {
        _cache.Store[FeedKind.Blog] = CachedBlog(Now.AddMinutes(-59));

        var result = await CreateService().GetFeedAsync(FeedKind.Blog);

        result.Status.Should().Be(FeedResultStatus.Cached);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("A Saved Post");
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetFeedAsync_CacheExactlyOneHourOld_RefetchesFromNetwork()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));
        _cache.Store[FeedKind.Blog] = CachedBlog(Now);
        var service = CreateService();

        _clock.Advance(TimeSpan.FromHours(1));
        var result = await service.GetFeedAsync(FeedKind.Blog);

        result.Status.Should().Be(FeedResultStatus.Fresh);
        result.Items.Should().HaveCount(3);
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetFeedAsync_ForceRefresh_BypassesFreshCache()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));
        _cache.Store[FeedKind.Blog] = CachedBlog(Now.AddMinutes(-1));

        var result = await CreateService().GetFeedAsync(FeedKind.Blog, forceRefresh: true);

        result.Status.Should().Be(FeedResultStatus.Fresh);
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetFeedAsync_NetworkFails_WithStaleCache_ReturnsStaleItems()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), new HttpRequestException("no network"));
        var fetchedAt = Now.AddHours(-5);
        _cache.Store[FeedKind.Blog] = CachedBlog(fetchedAt);

        var result = await CreateService().GetFeedAsync(FeedKind.Blog);

        result.Status.Should().Be(FeedResultStatus.Stale);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("A Saved Post");
        result.FetchedAt.Should().Be(fetchedAt);
    }

    [TestMethod]
    public async Task GetFeedAsync_NetworkFails_NoCache_ReturnsUnavailableWithEmptyItems()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Videos), new HttpRequestException("no network"));

        var result = await CreateService().GetFeedAsync(FeedKind.Videos);

        result.Status.Should().Be(FeedResultStatus.Unavailable);
        result.Items.Should().BeEmpty();
        result.HasItems.Should().BeFalse();
        result.FetchedAt.Should().BeNull();
    }

    [TestMethod]
    public async Task GetFeedAsync_MalformedXml_ReturnsUnavailableRatherThanThrowing()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("malformed.xml"));

        var result = await CreateService().GetFeedAsync(FeedKind.Blog);

        result.Status.Should().Be(FeedResultStatus.Unavailable);
        result.Items.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetFeedAsync_Success_WritesCacheWithFetchedAtFromTimeProvider()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Podcast), Load("podcast.xml"));

        var result = await CreateService().GetFeedAsync(FeedKind.Podcast);

        result.FetchedAt.Should().Be(Now);
        _cache.Store.Should().ContainKey(FeedKind.Podcast);
        _cache.Store[FeedKind.Podcast].FetchedAt.Should().Be(Now);
        _cache.Store[FeedKind.Podcast].Items.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task GetFeedAsync_TwoConcurrentCallsSameKind_IssuesOneHttpRequest()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));
        var service = CreateService();

        var results = await Task.WhenAll(
            service.GetFeedAsync(FeedKind.Blog),
            service.GetFeedAsync(FeedKind.Blog));

        _handler.RequestCount.Should().Be(1);
        results.Should().AllSatisfy(result => result.Items.Should().HaveCount(3));
    }

    [TestMethod]
    public async Task GetLatestAcrossFeedsAsync_MergesNewestFirstAndDedupesById()
    {
        var blog = Load("blog.xml");

        // The same document on two feeds: the duplicate guids must collapse to one card each.
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), blog);
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Videos), blog);
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Podcast), Load("podcast.xml"));

        var items = await CreateService().GetLatestAcrossFeedsAsync(10);

        items.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        items.Should().HaveCount(6);
        items[0].Title.Should().Be("Greens & Beans: A Love Story");
        items[1].Title.Should().Be("Eating to Live: The Podcast");
        items.Last().Title.Should().Be("The Undated Episode");
    }

    [TestMethod]
    public async Task GetLatestAcrossFeedsAsync_OneFeedFails_StillReturnsOthers()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Videos), new HttpRequestException("no network"));
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Podcast), Load("podcast.xml"));

        var items = await CreateService().GetLatestAcrossFeedsAsync(3);

        items.Should().HaveCount(3);
        items.Should().NotContain(item => item.Kind == FeedKind.Videos);
        items[0].Title.Should().Be("Greens & Beans: A Love Story");
    }
}
