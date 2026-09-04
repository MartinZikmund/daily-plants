namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Hand-written <see cref="IFeedService"/> for ViewModel tests. Mirrors the semantics the
/// ViewModels rely on from <see cref="FeedService"/>: a failure arrives as a
/// <see cref="FeedResultStatus"/> rather than an exception, and an unconfigured feed returns an
/// empty <see cref="FeedResultStatus.Fresh"/> result. Every call is counted, so a test can prove a
/// tab was - or was not - fetched. Set <see cref="ExceptionToThrow"/> to exercise the ViewModels'
/// belt-and-braces catch.
/// </summary>
internal sealed class FakeFeedService : IFeedService
{
    private readonly Dictionary<FeedKind, FeedResult> _responses = new();

    /// <summary>GetFeedAsync calls per feed - assert 0 to prove a tab loaded lazily.</summary>
    public Dictionary<FeedKind, int> CallCounts { get; } = new()
    {
        [FeedKind.Blog] = 0,
        [FeedKind.Videos] = 0,
        [FeedKind.Podcast] = 0
    };

    /// <summary>The forceRefresh flag of the most recent GetFeedAsync call, per feed.</summary>
    public Dictionary<FeedKind, bool> LastForceRefresh { get; } = new();

    /// <summary>What GetLatestAcrossFeedsAsync returns, before the count is applied.</summary>
    public IReadOnlyList<FeedItem> LatestAcrossFeeds { get; set; } = [];

    public int LatestAcrossFeedsCallCount { get; private set; }

    /// <summary>When set, every call throws this instead of returning.</summary>
    public Exception? ExceptionToThrow { get; set; }

    public FakeFeedService SetResponse(FeedKind kind, FeedResult result)
    {
        _responses[kind] = result;
        return this;
    }

    public Task<FeedResult> GetFeedAsync(FeedKind kind, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        CallCounts[kind]++;
        LastForceRefresh[kind] = forceRefresh;

        if (ExceptionToThrow is { } exception)
        {
            throw exception;
        }

        return Task.FromResult(_responses.TryGetValue(kind, out var result)
            ? result
            : new FeedResult(kind, [], FeedResultStatus.Fresh, null));
    }

    public Task<IReadOnlyList<FeedItem>> GetLatestAcrossFeedsAsync(int count, CancellationToken cancellationToken = default)
    {
        LatestAcrossFeedsCallCount++;

        if (ExceptionToThrow is { } exception)
        {
            throw exception;
        }

        return Task.FromResult<IReadOnlyList<FeedItem>>(LatestAcrossFeeds.Take(count).ToList());
    }
}
