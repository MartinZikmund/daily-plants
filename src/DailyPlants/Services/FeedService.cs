using System.Collections.Concurrent;
using System.Net.Http;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// <see cref="IFeedService"/> over HTTP plus <see cref="IFeedCache"/>. The clock is injected so the
/// freshness window can be tested without waiting for it.
/// </summary>
public sealed class FeedService : IFeedService
{
    /// <summary>
    /// nutritionfacts.org sends no Access-Control-Allow-Origin, so a browser fetch can never
    /// read the response. On wasm we serve cache only rather than spin on a guaranteed failure.
    /// </summary>
    /// <remarks>Deliberately not a const: as one it makes the cache-only branch unreachable code on every other head.</remarks>
#if __WASM__
    private static readonly bool LiveFetchSupported = false;
#else
    private static readonly bool LiveFetchSupported = true;
#endif

    /// <inheritdoc />
    public bool SupportsLiveFetch => LiveFetchSupported;

    private const string SiteRoot = "https://nutritionfacts.org/";

    private readonly HttpClient _httpClient;
    private readonly IFeedCache _cache;
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<FeedKind, SemaphoreSlim> _gates = new()
    {
        [FeedKind.Blog] = new(1, 1),
        [FeedKind.Videos] = new(1, 1),
        [FeedKind.Podcast] = new(1, 1)
    };

    private readonly ConcurrentDictionary<FeedKind, CachedFeed> _memory = new();

    public FeedService(HttpClient httpClient, IFeedCache cache, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The feed URL for one page. Page 1 is the bare feed - WordPress serves it identically with or
    /// without <c>?paged=1</c>, and leaving the parameter off keeps the cached page-1 URL unchanged.
    /// </summary>
    public static Uri GetFeedUrl(FeedKind kind, int page = 1)
    {
        var path = kind switch
        {
            FeedKind.Blog => "feed/",
            FeedKind.Videos => "videos/feed/",
            FeedKind.Podcast => "audio/feed/",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return new Uri(page > 1 ? $"{SiteRoot}{path}?paged={page}" : $"{SiteRoot}{path}");
    }

    /// <summary>
    /// The site-wide search feed for one page. The query is URL-encoded; page 1 carries no
    /// <c>paged</c> parameter.
    /// </summary>
    public static Uri GetSearchUrl(string query, int page = 1)
    {
        var url = $"{SiteRoot}?s={Uri.EscapeDataString(query.Trim())}&feed=rss2";
        return new Uri(page > 1 ? $"{url}&paged={page}" : url);
    }

    public async Task<FeedResult> GetFeedAsync(FeedKind kind, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && TryGetFreshMemo(kind) is { } memo)
        {
            return Served(memo, FeedResultStatus.Cached);
        }

        var gate = _gates[kind];
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check: whoever held the gate may have just filled the memo for us.
            if (!forceRefresh && TryGetFreshMemo(kind) is { } coalesced)
            {
                return Served(coalesced, FeedResultStatus.Cached);
            }

            var cached = await _cache.ReadAsync(kind, cancellationToken);
            if (!forceRefresh && cached is not null && IsFresh(cached))
            {
                _memory[kind] = cached;
                return Served(cached, FeedResultStatus.Cached);
            }

            if (!LiveFetchSupported)
            {
                AppLog.Info($"Live fetch is unavailable on this platform; serving cache for {kind}.");
                return cached is null
                    ? new FeedResult(kind, [], FeedResultStatus.LiveFetchUnavailable, null)
                    : Served(cached, IsFresh(cached) ? FeedResultStatus.Cached : FeedResultStatus.Stale);
            }

            return await FetchAsync(kind, cached, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FeedPage> GetFeedPageAsync(FeedKind kind, int page, CancellationToken cancellationToken = default)
    {
        // Other has no feed of its own - it only ever arrives as a search hit.
        if (page < 1 || page > IFeedService.MaxPage || kind == FeedKind.Other)
        {
            return FeedPage.End(FeedResultStatus.Fresh);
        }

        // Page 1 is the cached page: reuse GetFeedAsync wholesale so the freshness window,
        // the memo, the gate and the offline fallback all behave exactly as they did before paging.
        if (page == 1)
        {
            var result = await GetFeedAsync(kind, cancellationToken: cancellationToken);
            var mayHaveMore = result.Items.Count > 0 && result.Status is FeedResultStatus.Fresh or FeedResultStatus.Cached;
            return new FeedPage(result.Items, result.Status, mayHaveMore);
        }

        return await FetchPageAsync(
            () => GetFeedUrl(kind, page),
            xml => RssFeedParser.Parse(xml, kind),
            page,
            $"{kind} page {page}",
            cancellationToken);
    }

    public async Task<FeedPage> SearchAsync(string query, int page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || page < 1 || page > IFeedService.MaxPage)
        {
            return FeedPage.End(FeedResultStatus.Fresh);
        }

        return await FetchPageAsync(
            () => GetSearchUrl(query, page),
            RssFeedParser.ParseMixed,
            page,
            $"search page {page}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<FeedItem>> GetLatestAcrossFeedsAsync(int count, CancellationToken cancellationToken = default)
    {
        // Each call already degrades on its own failure, so a dead feed cannot fail the whole strip.
        var results = await Task.WhenAll(
            GetFeedAsync(FeedKind.Blog, cancellationToken: cancellationToken),
            GetFeedAsync(FeedKind.Videos, cancellationToken: cancellationToken),
            GetFeedAsync(FeedKind.Podcast, cancellationToken: cancellationToken));

        return results
            .SelectMany(result => result.Items)
            .DistinctBy(item => item.Id)
            .OrderByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(count)
            .ToList();
    }

    private async Task<FeedResult> FetchAsync(FeedKind kind, CachedFeed? cached, CancellationToken cancellationToken)
    {
        try
        {
            var xml = await _httpClient.GetStringAsync(GetFeedUrl(kind), cancellationToken);

            CachedFeed feed = new()
            {
                Kind = kind,
                FetchedAt = _timeProvider.GetUtcNow(),
                Items = RssFeedParser.Parse(xml, kind)
            };

            _memory[kind] = feed;
            await _cache.WriteAsync(feed, cancellationToken);

            return Served(feed, FeedResultStatus.Fresh);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Feed fetch failed for {kind}", ex);
            return cached is null
                ? new FeedResult(kind, [], FeedResultStatus.Unavailable, null)
                : Served(cached, FeedResultStatus.Stale);
        }
    }

    /// <summary>
    /// Fetches one uncached page. Nothing here touches the memo or the disk cache: those stay
    /// page-1-sized so the offline fallback keeps its shape.
    /// </summary>
    private async Task<FeedPage> FetchPageAsync(
        Func<Uri> url,
        Func<string, IReadOnlyList<FeedItem>> parse,
        int page,
        string description,
        CancellationToken cancellationToken)
    {
        if (!LiveFetchSupported)
        {
            AppLog.Info($"Live fetch is unavailable on this platform; skipping {description}.");
            return FeedPage.End(FeedResultStatus.LiveFetchUnavailable);
        }

        try
        {
            var xml = await _httpClient.GetStringAsync(url(), cancellationToken);
            var items = parse(xml);

            // A page that came back empty is the end of the archive. Search does not 404 past its
            // last page, so an empty page is the only end signal both sources share.
            return new FeedPage(items, FeedResultStatus.Fresh, items.Count > 0 && page < IFeedService.MaxPage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Feed fetch failed for {description}", ex);
            return FeedPage.End(FeedResultStatus.Unavailable);
        }
    }

    private CachedFeed? TryGetFreshMemo(FeedKind kind)
        => _memory.TryGetValue(kind, out var cached) && IsFresh(cached) ? cached : null;

    private bool IsFresh(CachedFeed feed) => _timeProvider.GetUtcNow() - feed.FetchedAt < IFeedService.FreshnessWindow;

    private static FeedResult Served(CachedFeed feed, FeedResultStatus status)
        => new(feed.Kind, feed.Items, status, feed.FetchedAt);
}
