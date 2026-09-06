using System.Net;
using System.Net.Http;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class FeedServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 9, 3, 15, 0, 0, TimeSpan.Zero);

    /// <summary>What a page past the end of the archive - or a search nothing matches - answers with.</summary>
    private const string EmptyChannel = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
            <channel>
                <title>NutritionFacts.org</title>
                <link>https://nutritionfacts.org/</link>
            </channel>
        </rss>
        """;

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

    /// <summary>Stubs all six feeds with fixture XML, so an overview call can succeed end to end.</summary>
    private void RespondToEveryFeed()
    {
        foreach (var kind in FeedKinds.Feeds)
        {
            var fixture = kind switch
            {
                FeedKind.Videos or FeedKind.Webinars => "videos.xml",
                FeedKind.Podcast or FeedKind.Questions => "podcast.xml",
                _ => "blog.xml"
            };

            _handler.RespondWith(FeedService.GetFeedUrl(kind), Load(fixture));
        }
    }

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
    [TestMethod]
    public void GetFeedUrl_PageOne_CarriesNoPagedParameter()
    {
        FeedService.GetFeedUrl(FeedKind.Blog, 1).AbsoluteUri.Should().Be("https://nutritionfacts.org/feed/");
        FeedService.GetFeedUrl(FeedKind.Blog, 1).Should().Be(FeedService.GetFeedUrl(FeedKind.Blog));
        FeedService.GetFeedUrl(FeedKind.Videos, 1).AbsoluteUri.Should().NotContain("paged");
    }

    [TestMethod]
    public void GetFeedUrl_EveryFeed_UsesThePluralFeedPath()
    {
        // The feed path is plural where the item path is singular: /recipes/feed/ serves /recipe/
        // items, and /recipe/feed/ is a 404. Questions is plural on both sides.
        FeedService.GetFeedUrl(FeedKind.Blog).AbsoluteUri.Should().Be("https://nutritionfacts.org/feed/");
        FeedService.GetFeedUrl(FeedKind.Videos).AbsoluteUri.Should().Be("https://nutritionfacts.org/videos/feed/");
        FeedService.GetFeedUrl(FeedKind.Podcast).AbsoluteUri.Should().Be("https://nutritionfacts.org/audio/feed/");
        FeedService.GetFeedUrl(FeedKind.Recipes).AbsoluteUri.Should().Be("https://nutritionfacts.org/recipes/feed/");
        FeedService.GetFeedUrl(FeedKind.Questions).AbsoluteUri.Should().Be("https://nutritionfacts.org/questions/feed/");
        FeedService.GetFeedUrl(FeedKind.Webinars).AbsoluteUri.Should().Be("https://nutritionfacts.org/webinars/feed/");
    }

    [TestMethod]
    public void GetFeedUrl_Other_HasNoFeedOfItsOwn()
    {
        var url = () => FeedService.GetFeedUrl(FeedKind.Other);

        url.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void GetFeedUrl_PageTwoAndUp_CarriesThePagedParameter()
    {
        FeedService.GetFeedUrl(FeedKind.Blog, 2).AbsoluteUri.Should().Be("https://nutritionfacts.org/feed/?paged=2");
        FeedService.GetFeedUrl(FeedKind.Videos, 3).AbsoluteUri.Should().Be("https://nutritionfacts.org/videos/feed/?paged=3");
        FeedService.GetFeedUrl(FeedKind.Podcast, 7).AbsoluteUri.Should().Be("https://nutritionfacts.org/audio/feed/?paged=7");
    }

    [TestMethod]
    public void GetFeedUrl_NewFeedsPageTwoAndUp_CarryThePagedParameter()
    {
        FeedService.GetFeedUrl(FeedKind.Recipes, 2).AbsoluteUri.Should().Be("https://nutritionfacts.org/recipes/feed/?paged=2");
        FeedService.GetFeedUrl(FeedKind.Questions, 4).AbsoluteUri.Should().Be("https://nutritionfacts.org/questions/feed/?paged=4");
        FeedService.GetFeedUrl(FeedKind.Webinars, 6).AbsoluteUri.Should().Be("https://nutritionfacts.org/webinars/feed/?paged=6");
    }

    [TestMethod]
    public void GetSearchUrl_PageOne_UrlEncodesTheQueryAndOmitsPaged()
    {
        FeedService.GetSearchUrl("greens & beans", 1).AbsoluteUri
            .Should().Be("https://nutritionfacts.org/?s=greens%20%26%20beans&feed=rss2");
        FeedService.GetSearchUrl("  kale  ", 1).AbsoluteUri
            .Should().Be("https://nutritionfacts.org/?s=kale&feed=rss2");
    }

    [TestMethod]
    public void GetSearchUrl_PageTwo_AppendsPagedAfterTheFeedParameter()
        => FeedService.GetSearchUrl("kale", 2).AbsoluteUri
            .Should().Be("https://nutritionfacts.org/?s=kale&feed=rss2&paged=2");

    [TestMethod]
    public async Task GetFeedPageAsync_PageOne_FreshCache_ServesCacheWithoutHttpCall()
    {
        _cache.Store[FeedKind.Blog] = CachedBlog(Now.AddMinutes(-59));

        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, 1);

        page.Status.Should().Be(FeedResultStatus.Cached);
        page.Items.Should().ContainSingle().Which.Title.Should().Be("A Saved Post");
        page.MayHaveMore.Should().BeTrue();
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PageOne_NoCache_FetchesTheUnpagedFeed()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), Load("blog.xml"));

        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, 1);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Should().HaveCount(3);
        page.MayHaveMore.Should().BeTrue();
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PageOne_OfflineWithStaleCache_ServesItemsButOffersNoMore()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog), new HttpRequestException("no network"));
        _cache.Store[FeedKind.Blog] = CachedBlog(Now.AddHours(-5));

        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, 1);

        page.Status.Should().Be(FeedResultStatus.Stale);
        page.HasItems.Should().BeTrue();

        // The network is down: offering "load more" would only buy a second failure.
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PageTwo_FetchesEvenWithAFreshCacheAndNeverWritesToIt()
    {
        _cache.Store[FeedKind.Blog] = CachedBlog(Now.AddMinutes(-1));
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog, 2), Load("blog.xml"));
        var service = CreateService();

        var page = await service.GetFeedPageAsync(FeedKind.Blog, 2);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Should().HaveCount(3);
        page.MayHaveMore.Should().BeTrue();
        _handler.RequestCount.Should().Be(1);

        // The cache stays page-1-sized, and the memo is untouched.
        _cache.Store[FeedKind.Blog].Items.Should().ContainSingle().Which.Title.Should().Be("A Saved Post");
        (await service.GetFeedAsync(FeedKind.Blog)).Items.Should().ContainSingle();
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PageWithZeroItems_EndsTheList()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog, 4), EmptyChannel);

        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, 4);

        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
        page.Status.Should().Be(FeedResultStatus.Fresh);
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PageThatFails_ReportsUnavailableAndEndsTheList()
    {
        // Past the last page the site answers 404, which arrives here as a failed fetch.
        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, 9);

        page.Status.Should().Be(FeedResultStatus.Unavailable);
        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task GetFeedPageAsync_AtTheRunawayCap_ServesTheItemsButOffersNoMore()
    {
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Blog, IFeedService.MaxPage), Load("blog.xml"));

        var page = await CreateService().GetFeedPageAsync(FeedKind.Blog, IFeedService.MaxPage);

        page.Items.Should().HaveCount(3);
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task GetFeedPageAsync_PastTheRunawayCap_ReturnsAnEmptyPageWithoutHttpCall()
    {
        var service = CreateService();

        var beyond = await service.GetFeedPageAsync(FeedKind.Blog, IFeedService.MaxPage + 1);
        var nonsense = await service.GetFeedPageAsync(FeedKind.Blog, 0);

        beyond.Items.Should().BeEmpty();
        beyond.MayHaveMore.Should().BeFalse();
        nonsense.Items.Should().BeEmpty();
        nonsense.MayHaveMore.Should().BeFalse();
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task SearchAsync_BlankQuery_ReturnsAnEmptyPageWithoutHttpCall()
    {
        var service = CreateService();

        foreach (var query in new[] { string.Empty, "   ", "\t" })
        {
            var page = await service.SearchAsync(query, 1);

            page.Items.Should().BeEmpty();
            page.MayHaveMore.Should().BeFalse();
        }

        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task SearchAsync_FirstPage_ReturnsMixedKindsAndNeverTouchesTheCache()
    {
        _handler.RespondWith(FeedService.GetSearchUrl("greens", 1), Load("search.xml"));

        var page = await CreateService().SearchAsync("greens", 1);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Should().HaveCount(7);
        page.Items.Select(item => item.Kind).Should().Equal(
            FeedKind.Blog,
            FeedKind.Videos,
            FeedKind.Podcast,
            FeedKind.Questions,
            FeedKind.Recipes,
            FeedKind.Webinars,
            FeedKind.Other);
        page.MayHaveMore.Should().BeTrue();
        _cache.Store.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SearchAsync_PageTwo_RequestsThePagedSearchUrl()
    {
        // Only the paged URL is stubbed, so a request to any other URL 404s into Unavailable.
        _handler.RespondWith(FeedService.GetSearchUrl("greens", 2), Load("search.xml"));

        var page = await CreateService().SearchAsync("greens", 2);

        page.Items.Should().HaveCount(7);
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task SearchAsync_NoResults_EndsTheList()
    {
        // A query nothing matches answers 200 with an empty channel rather than 404.
        _handler.RespondWith(FeedService.GetSearchUrl("zzzz", 1), EmptyChannel);

        var page = await CreateService().SearchAsync("zzzz", 1);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task SearchAsync_Offline_ReportsUnavailableRatherThanThrowing()
    {
        _handler.RespondWith(FeedService.GetSearchUrl("greens", 1), new HttpRequestException("no network"));

        var page = await CreateService().SearchAsync("greens", 1);

        page.Status.Should().Be(FeedResultStatus.Unavailable);
        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task SearchAsync_MalformedXml_ReportsUnavailableRatherThanThrowing()
    {
        _handler.RespondWith(FeedService.GetSearchUrl("greens", 1), Load("malformed.xml"));

        var page = await CreateService().SearchAsync("greens", 1);

        page.Status.Should().Be(FeedResultStatus.Unavailable);
        page.Items.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SearchAsync_PastTheRunawayCap_ReturnsAnEmptyPageWithoutHttpCall()
    {
        var page = await CreateService().SearchAsync("greens", IFeedService.MaxPage + 1);

        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public void GetTopicUrl_PageOne_OmitsPaged()
    {
        FeedService.GetTopicUrl("berries").AbsoluteUri.Should().Be("https://nutritionfacts.org/topics/berries/feed/");
        FeedService.GetTopicUrl("flax-seeds", 1).AbsoluteUri.Should().Be("https://nutritionfacts.org/topics/flax-seeds/feed/");
        FeedService.GetTopicUrl("  grains  ", 1).AbsoluteUri.Should().Be("https://nutritionfacts.org/topics/grains/feed/");
    }

    [TestMethod]
    public void GetTopicUrl_PageTwoAndUp_AppendsPaged()
    {
        FeedService.GetTopicUrl("berries", 2).AbsoluteUri.Should().Be("https://nutritionfacts.org/topics/berries/feed/?paged=2");
        FeedService.GetTopicUrl("vitamin-b12", 7).AbsoluteUri.Should().Be("https://nutritionfacts.org/topics/vitamin-b12/feed/?paged=7");
    }

    [TestMethod]
    public async Task GetTopicPageAsync_BlankSlug_ReturnsAnEmptyPageWithoutHttpCall()
    {
        var service = CreateService();

        foreach (var slug in new[] { string.Empty, "   ", "	" })
        {
            var page = await service.GetTopicPageAsync(slug, 1);

            page.Items.Should().BeEmpty();
            page.MayHaveMore.Should().BeFalse();
        }

        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetTopicPageAsync_FirstPage_ReturnsMixedKindsAndNeverTouchesTheCache()
    {
        // A topic collects every content type, so items must be stamped from their own links -
        // the search fixture is the one that carries all seven kinds.
        _handler.RespondWith(FeedService.GetTopicUrl("berries", 1), Load("search.xml"));

        var page = await CreateService().GetTopicPageAsync("berries", 1);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Select(item => item.Kind).Should().Equal(
            FeedKind.Blog,
            FeedKind.Videos,
            FeedKind.Podcast,
            FeedKind.Questions,
            FeedKind.Recipes,
            FeedKind.Webinars,
            FeedKind.Other);
        page.MayHaveMore.Should().BeTrue();
        _cache.Store.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetTopicPageAsync_PageTwo_RequestsThePagedTopicUrl()
    {
        // Only the paged URL is stubbed, so a request to any other URL 404s into Unavailable.
        _handler.RespondWith(FeedService.GetTopicUrl("berries", 2), Load("search.xml"));

        var page = await CreateService().GetTopicPageAsync("berries", 2);

        page.Items.Should().HaveCount(7);
        _handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GetTopicPageAsync_NoResults_EndsTheList()
    {
        _handler.RespondWith(FeedService.GetTopicUrl("berries", 1), EmptyChannel);

        var page = await CreateService().GetTopicPageAsync("berries", 1);

        page.Status.Should().Be(FeedResultStatus.Fresh);
        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task GetTopicPageAsync_Offline_ReportsUnavailableRatherThanThrowing()
    {
        _handler.RespondWith(FeedService.GetTopicUrl("berries", 1), new HttpRequestException("no network"));

        var page = await CreateService().GetTopicPageAsync("berries", 1);

        page.Status.Should().Be(FeedResultStatus.Unavailable);
        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
    }

    [TestMethod]
    public async Task GetTopicPageAsync_UnknownSlug_ReportsUnavailableRatherThanThrowing()
    {
        // A slug that is not a topic 404s - "whole-grains" and "flaxseeds" both do.
        var page = await CreateService().GetTopicPageAsync("whole-grains", 1);

        page.Status.Should().Be(FeedResultStatus.Unavailable);
        page.Items.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetTopicPageAsync_PastTheRunawayCap_ReturnsAnEmptyPageWithoutHttpCall()
    {
        var page = await CreateService().GetTopicPageAsync("berries", IFeedService.MaxPage + 1);

        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetTopicPageAsync_WithoutLiveFetch_ReportsLiveFetchUnavailableWithoutARequest()
    {
        // LiveFetchSupported is compiled false on the browser head and true everywhere else, so
        // assert whichever half this build is: the browser skips the request entirely.
        var service = CreateService();

        var page = await service.GetTopicPageAsync("berries", 1);

        if (service.SupportsLiveFetch)
        {
            page.Status.Should().Be(FeedResultStatus.Unavailable);
            _handler.RequestCount.Should().Be(1);
        }
        else
        {
            page.Status.Should().Be(FeedResultStatus.LiveFetchUnavailable);
            _handler.RequestCount.Should().Be(0);
        }
    }
    [TestMethod]
    public async Task GetFeedAsync_OtherKind_ReturnsAnEmptyResultRatherThanThrowing()
    {
        // Other is the default enum value, so an unset kind must not fall onto a missing gate.
        var result = await CreateService().GetFeedAsync(FeedKind.Other);

        result.Items.Should().BeEmpty();
        result.Kind.Should().Be(FeedKind.Other);
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetFeedPageAsync_OtherKind_ReturnsAnEmptyPageRatherThanThrowing()
    {
        // Other is a search-only kind: there is no /other/ feed to page through.
        var page = await CreateService().GetFeedPageAsync(FeedKind.Other, 1);

        page.Items.Should().BeEmpty();
        page.MayHaveMore.Should().BeFalse();
        _handler.RequestCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetOverviewAsync_ReturnsOneGroupPerFeedInDisplayOrder()
    {
        RespondToEveryFeed();

        var groups = await CreateService().GetOverviewAsync(2);

        groups.Select(group => group.Kind).Should().Equal(FeedKinds.Feeds);
        groups.Should().OnlyContain(group => group.Items.Count == 2);
        groups.Should().OnlyContain(group => group.HasItems);
        _handler.RequestCount.Should().Be(6);
    }

    [TestMethod]
    public async Task GetOverviewAsync_AsksEachFeedForExactlyTheRequestedCount()
    {
        RespondToEveryFeed();

        var groups = await CreateService().GetOverviewAsync(1);

        // The fixtures hold three items each; the overview shows only the newest one asked for.
        groups.Should().OnlyContain(group => group.Items.Count == 1);
        groups[0].Items[0].Title.Should().Be("Greens & Beans: A Love Story");
    }

    [TestMethod]
    public async Task GetOverviewAsync_StampsEachGroupWithItsOwnKind()
    {
        RespondToEveryFeed();

        var groups = await CreateService().GetOverviewAsync(2);

        groups.Should().OnlyContain(group => group.Items.All(item => item.Kind == group.Kind));
    }

    [TestMethod]
    public async Task GetOverviewAsync_OneFeedFails_YieldsAnEmptyGroupAndKeepsTheOtherFive()
    {
        RespondToEveryFeed();
        _handler.RespondWith(FeedService.GetFeedUrl(FeedKind.Recipes), new HttpRequestException("no network"));

        var groups = await CreateService().GetOverviewAsync(2);

        groups.Should().HaveCount(6);
        groups.Single(group => group.Kind == FeedKind.Recipes).Items.Should().BeEmpty();
        groups.Where(group => group.Kind != FeedKind.Recipes).Should().OnlyContain(group => group.HasItems);
    }

    [TestMethod]
    public async Task GetOverviewAsync_SecondCall_ServesTheWarmCacheWithoutHttpCalls()
    {
        RespondToEveryFeed();
        var service = CreateService();

        await service.GetOverviewAsync(2);
        var groups = await service.GetOverviewAsync(2);

        groups.Should().OnlyContain(group => group.HasItems);
        _handler.RequestCount.Should().Be(6);
    }

    [TestMethod]
    public async Task GetOverviewAsync_FreshDiskCache_SkipsThatFeedsHttpCall()
    {
        RespondToEveryFeed();
        _cache.Store[FeedKind.Webinars] = new CachedFeed
        {
            Kind = FeedKind.Webinars,
            FetchedAt = Now.AddMinutes(-10),
            Items = [new FeedItem { Id = "cached-web", Kind = FeedKind.Webinars, Title = "A Saved Webinar" }]
        };

        var groups = await CreateService().GetOverviewAsync(2);

        groups.Single(group => group.Kind == FeedKind.Webinars)
            .Items.Should().ContainSingle().Which.Title.Should().Be("A Saved Webinar");

        // Only the five uncached feeds went to the network.
        _handler.RequestCount.Should().Be(5);
    }

    [TestMethod]
    public async Task GetOverviewAsync_FetchesTheSixFeedsConcurrentlyNotOneAfterAnother()
    {
        // Every request blocks until all six have arrived, so a sequential implementation waits out
        // the probe timeout and comes back with empty groups instead.
        ConcurrentArrivalHandler probe = new(expected: FeedKinds.Feeds.Count, body: Load("blog.xml"));
        FeedService service = new(new HttpClient(probe), _cache, _clock);

        var groups = await service.GetOverviewAsync(2);

        groups.Should().OnlyContain(group => group.HasItems);
    }

    [TestMethod]
    public async Task GetOverviewAsync_ThenOpeningATab_ServesThatFeedFromTheWarmCache()
    {
        // The overview is the default tab, so it warms all six. Selecting one afterwards must read
        // what the overview already fetched rather than going back to the network for it.
        RespondToEveryFeed();
        var service = CreateService();

        await service.GetOverviewAsync(2);
        var page = await service.GetFeedPageAsync(FeedKind.Recipes, 1);

        page.Items.Should().NotBeEmpty();
        _handler.RequestCount.Should().Be(6, "the six the overview made, and not one more");
    }

    [TestMethod]
    public async Task GetOverviewAsync_ZeroPerFeed_ReturnsEmptyGroupsRatherThanThrowing()
    {
        RespondToEveryFeed();

        var groups = await CreateService().GetOverviewAsync(0);

        groups.Should().HaveCount(6);
        groups.Should().OnlyContain(group => group.Items.Count == 0);
    }

    [TestMethod]
    public async Task GetLatestAcrossFeedsAsync_AsksOnlyTheThreeTeaserFeeds()
    {
        // The Diary teaser must not grow when FeedKind does: six fetches on the app's first page
        // is not worth two more cards. Every request is counted, so 3 is the proof.
        RespondToEveryFeed();

        await CreateService().GetLatestAcrossFeedsAsync(10);

        _handler.RequestCount.Should().Be(3);
    }

    /// <summary>
    /// Answers only once every expected request has arrived: succeeds under a concurrent caller,
    /// times out under a sequential one.
    /// </summary>
    private sealed class ConcurrentArrivalHandler : HttpMessageHandler
    {
        private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(5);

        private readonly int _expected;
        private readonly string _body;
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _arrived;

        public ConcurrentArrivalHandler(int expected, string body)
        {
            _expected = expected;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) >= _expected)
            {
                _allArrived.TrySetResult();
            }

            try
            {
                await _allArrived.Task.WaitAsync(GiveUpAfter, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Give up for everyone, so a sequential caller fails once rather than six times over.
                _allArrived.TrySetResult();
                throw;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_body) };
        }
    }
}
