namespace DailyPlants.Tests.Services;

[TestClass]
public class JsonFeedCacheTests
{
    private string _cacheDirectory = string.Empty;
    private JsonFeedCache _cache = null!;

    [TestInitialize]
    public void Initialize()
    {
        _cacheDirectory = Path.Combine(Path.GetTempPath(), $"DailyPlants-FeedCache-{Guid.NewGuid():N}");
        _cache = new JsonFeedCache(_cacheDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
            }
            else if (File.Exists(_cacheDirectory))
            {
                File.Delete(_cacheDirectory);
            }
        }
        catch (IOException)
        {
            // Another handle may still be closing; the temp directory is disposable either way.
        }
    }

    private static CachedFeed SampleFeed() => new()
    {
        Kind = FeedKind.Blog,
        FetchedAt = new DateTimeOffset(2025, 9, 3, 14, 0, 0, TimeSpan.Zero),
        Items =
        [
            new FeedItem
            {
                Id = "https://nutritionfacts.org/?p=101010",
                Kind = FeedKind.Blog,
                Title = "Greens & Beans: A Love Story",
                Link = "https://nutritionfacts.org/blog/greens-and-beans/",
                Summary = "Why the humble bean belongs on every plate.",
                Author = "Michael Greger M.D. FACLM",
                ThumbnailUrl = "https://nutritionfacts.org/beans.jpg",
                PublishedAt = new DateTimeOffset(2025, 9, 3, 13, 0, 0, TimeSpan.Zero)
            },
            new FeedItem
            {
                Id = "https://nutritionfacts.org/?p=101012",
                Kind = FeedKind.Blog,
                Title = "A Post With No Picture",
                Link = "https://nutritionfacts.org/blog/no-picture/",
                ThumbnailUrl = null,
                PublishedAt = null
            }
        ]
    };

    [TestMethod]
    public async Task WriteAsync_ThenReadAsync_RoundTripsAllFields()
    {
        var written = SampleFeed();
        await _cache.WriteAsync(written);

        var read = await _cache.ReadAsync(FeedKind.Blog);

        read.Should().NotBeNull();
        read!.Kind.Should().Be(FeedKind.Blog);
        read.FetchedAt.Should().Be(written.FetchedAt);
        read.Items.Should().HaveCount(2);

        var first = read.Items[0];
        first.Id.Should().Be("https://nutritionfacts.org/?p=101010");
        first.Kind.Should().Be(FeedKind.Blog);
        first.Title.Should().Be("Greens & Beans: A Love Story");
        first.Link.Should().Be("https://nutritionfacts.org/blog/greens-and-beans/");
        first.Summary.Should().Be("Why the humble bean belongs on every plate.");
        first.Author.Should().Be("Michael Greger M.D. FACLM");
        first.ThumbnailUrl.Should().Be("https://nutritionfacts.org/beans.jpg");
        first.PublishedAt.Should().Be(new DateTimeOffset(2025, 9, 3, 13, 0, 0, TimeSpan.Zero));

        var second = read.Items[1];
        second.ThumbnailUrl.Should().BeNull();
        second.PublishedAt.Should().BeNull();
        second.Summary.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ReadAsync_NoFile_ReturnsNull()
    {
        var read = await _cache.ReadAsync(FeedKind.Videos);

        read.Should().BeNull();
    }

    [TestMethod]
    public async Task ReadAsync_CorruptJson_ReturnsNullAndDoesNotThrow()
    {
        await File.WriteAllTextAsync(Path.Combine(_cacheDirectory, "podcast.json"), "{ this is not json");

        var read = await _cache.ReadAsync(FeedKind.Podcast);

        read.Should().BeNull();
    }

    [TestMethod]
    public async Task WriteAsync_ExistingFile_IsReplaced()
    {
        await _cache.WriteAsync(SampleFeed());

        CachedFeed replacement = new()
        {
            Kind = FeedKind.Blog,
            FetchedAt = new DateTimeOffset(2025, 9, 4, 9, 0, 0, TimeSpan.Zero),
            Items = [new FeedItem { Id = "only", Kind = FeedKind.Blog, Title = "Only One" }]
        };
        await _cache.WriteAsync(replacement);

        var read = await _cache.ReadAsync(FeedKind.Blog);

        read.Should().NotBeNull();
        read!.FetchedAt.Should().Be(replacement.FetchedAt);
        read.Items.Should().ContainSingle().Which.Title.Should().Be("Only One");
    }

    [TestMethod]
    public async Task WriteAsync_UnwritableDirectory_DoesNotThrow()
    {
        // A plain file where the cache directory should be: creating it and writing into it both fail.
        var blockedPath = Path.Combine(Path.GetTempPath(), $"DailyPlants-FeedCache-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blockedPath, "not a directory");

        try
        {
            JsonFeedCache blocked = new(blockedPath);

            var write = async () => await blocked.WriteAsync(SampleFeed());

            await write.Should().NotThrowAsync();
            (await blocked.ReadAsync(FeedKind.Blog)).Should().BeNull();
        }
        finally
        {
            try
            {
                File.Delete(blockedPath);
            }
            catch (IOException)
            {
                // Best effort cleanup of a temp file.
            }
        }
    }
}
