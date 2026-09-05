using System.Xml;

namespace DailyPlants.Tests.Services;

[TestClass]
public class RssFeedParserTests
{
    [TestMethod]
    public void Parse_BlogFixture_ReturnsThreeItems()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items.Should().HaveCount(3);
        items.Should().NotContain(item => item.Title == "NutritionFacts.org");
    }

    [TestMethod]
    public void Parse_BlogFixture_TitleIsEntityDecodedByParser()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items[0].Title.Should().Be("Greens & Beans: A Love Story");
    }

    [TestMethod]
    public void Parse_BlogFixture_DescriptionEntitiesAreDecoded()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items[0].Summary.Should().EndWith("a fistful of greens…");
        items[0].Summary.Should().NotContain("&#8230;");
    }

    [TestMethod]
    public void Parse_BlogFixture_SummaryHasNoHtmlTags()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items[1].Summary.Should().Be("The recommended intake, and the amount actually associated with the lowest disease risk.");
        items.Should().OnlyContain(item => !item.Summary.Contains('<') && !item.Summary.Contains('>'));
    }

    [TestMethod]
    public void Parse_BlogFixture_UsesGuidAsIdNotLink()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items[0].Id.Should().Be("https://nutritionfacts.org/?p=101010");
        items[0].Link.Should().Be("https://nutritionfacts.org/blog/greens-and-beans/");
    }

    [TestMethod]
    public void Parse_BlogFixture_ItemWithoutMediaContent_ThumbnailUrlIsNull()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items[2].ThumbnailUrl.Should().BeNull();
        items[0].ThumbnailUrl.Should().Be("https://nutritionfacts.org/app/uploads/2025/09/greens-and-beans.jpg");
    }

    [TestMethod]
    public void Parse_BlogFixture_CreatorIsReadFromDublinCoreNamespace()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);

        items.Should().OnlyContain(item => item.Author == "Michael Greger M.D. FACLM");
    }

    [TestMethod]
    public void Parse_VideosFixture_MediaKeywordsAdjacentToMediaContent_ThumbnailStillParsed()
    {
        var items = RssFeedParser.Parse(Load("videos.xml"), FeedKind.Videos);

        items[0].ThumbnailUrl.Should().Be("https://nutritionfacts.org/app/uploads/2025/09/broccoli.jpg");
    }

    [TestMethod]
    public void Parse_VideosFixture_LinkIsUsedNotEnclosure()
    {
        var items = RssFeedParser.Parse(Load("videos.xml"), FeedKind.Videos);

        items[0].Link.Should().Be("https://nutritionfacts.org/video/the-best-way-to-cook-broccoli/");
        items.Should().OnlyContain(item => !item.Link.EndsWith(".mp4"));
    }

    [TestMethod]
    public void Parse_PodcastFixture_UnparseablePubDate_PublishedAtIsNull()
    {
        var items = RssFeedParser.Parse(Load("podcast.xml"), FeedKind.Podcast);

        items[1].PublishedAt.Should().BeNull();
        items[1].Title.Should().Be("The Undated Episode");
    }

    [TestMethod]
    public void Parse_AllFixtures_PubDateParsedAsUtcOffset()
    {
        var blog = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Blog);
        var videos = RssFeedParser.Parse(Load("videos.xml"), FeedKind.Videos);
        var podcast = RssFeedParser.Parse(Load("podcast.xml"), FeedKind.Podcast);

        blog[0].PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 3, 13, 0, 0, TimeSpan.Zero));
        blog[2].PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 1, 11, 15, 0, TimeSpan.Zero));
        videos[0].PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 3, 9, 0, 0, TimeSpan.Zero));
        podcast[0].PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 3, 8, 0, 0, TimeSpan.Zero));
    }

    [TestMethod]
    public void Parse_MalformedXml_ThrowsXmlException()
    {
        var parse = () => RssFeedParser.Parse(Load("malformed.xml"), FeedKind.Blog);

        parse.Should().Throw<XmlException>();
    }

    [TestMethod]
    public void Parse_EmptyChannel_ReturnsEmptyList()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
                <channel>
                    <title>NutritionFacts.org</title>
                    <link>https://nutritionfacts.org/</link>
                    <description>The latest in nutrition related research</description>
                </channel>
            </rss>
            """;

        var items = RssFeedParser.Parse(xml, FeedKind.Blog);

        items.Should().BeEmpty();
    }

    [TestMethod]
    public void Parse_ItemWithNeitherGuidNorLink_IsSkipped()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
                <channel>
                    <title>NutritionFacts.org</title>
                    <item>
                        <title>Nothing to open</title>
                    </item>
                    <item>
                        <title>Something to open</title>
                        <link>https://nutritionfacts.org/blog/something/</link>
                    </item>
                </channel>
            </rss>
            """;

        var items = RssFeedParser.Parse(xml, FeedKind.Blog);

        items.Should().ContainSingle().Which.Id.Should().Be("https://nutritionfacts.org/blog/something/");
    }

    [TestMethod]
    public void Parse_AnyFixture_KindIsStampedOnEveryItem()
    {
        var items = RssFeedParser.Parse(Load("blog.xml"), FeedKind.Videos);

        items.Should().OnlyContain(item => item.Kind == FeedKind.Videos);
    }

    [TestMethod]
    public void KindFromLink_BlogPermalink_ReturnsBlog()
        => RssFeedParser.KindFromLink("https://nutritionfacts.org/blog/greens-and-beans/").Should().Be(FeedKind.Blog);

    [TestMethod]
    public void KindFromLink_VideoPermalink_ReturnsVideos()
        => RssFeedParser.KindFromLink("https://nutritionfacts.org/video/the-best-way-to-cook-greens/").Should().Be(FeedKind.Videos);

    [TestMethod]
    public void KindFromLink_AudioPermalink_ReturnsPodcast()
        => RssFeedParser.KindFromLink("https://nutritionfacts.org/audio/greens-on-the-podcast/").Should().Be(FeedKind.Podcast);

    [TestMethod]
    public void KindFromLink_QuestionsPermalink_ReturnsOther()
        => RssFeedParser.KindFromLink("https://nutritionfacts.org/questions/are-bagged-greens-safe/").Should().Be(FeedKind.Other);

    [TestMethod]
    public void KindFromLink_UnrecognisedLink_ReturnsOtherNotBlog()
    {
        RssFeedParser.KindFromLink("banana").Should().Be(FeedKind.Other);
        RssFeedParser.KindFromLink("https://nutritionfacts.org/topics/kale/").Should().Be(FeedKind.Other);
        RssFeedParser.KindFromLink(null).Should().Be(FeedKind.Other);
        RssFeedParser.KindFromLink("   ").Should().Be(FeedKind.Other);
    }

    [TestMethod]
    public void KindFromLink_VideosFeedUrl_IsNotMistakenForAVideo()
    {
        // The feed URL is /videos/, an item is /video/ - the plural must not match.
        RssFeedParser.KindFromLink("https://nutritionfacts.org/videos/feed/").Should().Be(FeedKind.Other);
    }

    [TestMethod]
    public void ParseMixed_SearchFixture_DerivesKindPerItemFromTheLink()
    {
        var items = RssFeedParser.ParseMixed(Load("search.xml"));

        items.Should().HaveCount(4);
        items.Select(item => item.Kind).Should().Equal(FeedKind.Blog, FeedKind.Videos, FeedKind.Podcast, FeedKind.Other);
    }

    [TestMethod]
    public void ParseMixed_SearchFixture_ReadsTheSameFieldsAsAFeedItem()
    {
        var items = RssFeedParser.ParseMixed(Load("search.xml"));

        items[0].Id.Should().Be("https://nutritionfacts.org/?p=101010");
        items[0].Title.Should().Be("Greens & Beans: A Love Story");
        items[0].Author.Should().Be("Michael Greger M.D. FACLM");
        items[0].ThumbnailUrl.Should().Be("https://nutritionfacts.org/app/uploads/2025/09/greens-and-beans.jpg");
        items[0].PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 3, 13, 0, 0, TimeSpan.Zero));
        items[3].Summary.Should().Be("A question from the Q&A archive.");
        items[3].ThumbnailUrl.Should().BeNull();
    }

    [TestMethod]
    public void ParseMixed_EmptyChannel_ReturnsEmptyList()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
                <channel>
                    <title>Search Results</title>
                    <link>https://nutritionfacts.org/?s=zzzz</link>
                </channel>
            </rss>
            """;

        RssFeedParser.ParseMixed(xml).Should().BeEmpty();
    }

    private static string Load(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
