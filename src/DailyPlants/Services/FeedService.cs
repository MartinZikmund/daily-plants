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
    private static readonly bool SupportsLiveFetch = false;
#else
    private static readonly bool SupportsLiveFetch = true;
#endif

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

    public static Uri GetFeedUrl(FeedKind kind) => kind switch
    {
        FeedKind.Blog => new Uri("https://nutritionfacts.org/feed/"),
        FeedKind.Videos => new Uri("https://nutritionfacts.org/videos/feed/"),
        FeedKind.Podcast => new Uri("https://nutritionfacts.org/audio/feed/"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

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

            if (!SupportsLiveFetch)
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

    private CachedFeed? TryGetFreshMemo(FeedKind kind)
        => _memory.TryGetValue(kind, out var cached) && IsFresh(cached) ? cached : null;

    private bool IsFresh(CachedFeed feed) => _timeProvider.GetUtcNow() - feed.FetchedAt < IFeedService.FreshnessWindow;

    private static FeedResult Served(CachedFeed feed, FeedResultStatus status)
        => new(feed.Kind, feed.Items, status, feed.FetchedAt);
}
