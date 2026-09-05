namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Hand-written <see cref="IFeedService"/> for ViewModel tests. Mirrors the semantics the
/// ViewModels rely on from <see cref="FeedService"/>: a failure arrives as a
/// <see cref="FeedResultStatus"/> rather than an exception, and an unconfigured feed returns an
/// empty <see cref="FeedResultStatus.Fresh"/> result. Every call is counted, so a test can prove a
/// tab was - or was not - fetched. Set <see cref="ExceptionToThrow"/> to exercise the ViewModels'
/// belt-and-braces catch, and <see cref="Pause"/> to hold a fetch open while a second call races it.
/// </summary>
internal sealed class FakeFeedService : IFeedService
{
    /// <summary>Set false to model the browser head, which cannot reach the feeds at all.</summary>
    public bool SupportsLiveFetch { get; set; } = true;

    private readonly Dictionary<FeedKind, FeedResult> _responses = new();
    private readonly Dictionary<(FeedKind Kind, int Page), FeedPage> _feedPages = new();
    private readonly Dictionary<(string Query, int Page), FeedPage> _searchPages = new();

    private TaskCompletionSource? _pause;

    /// <summary>GetFeedAsync calls per feed - assert 0 to prove a tab loaded lazily.</summary>
    public Dictionary<FeedKind, int> CallCounts { get; } = FeedKinds.Feeds.ToDictionary(kind => kind, _ => 0);

    /// <summary>The forceRefresh flag of the most recent GetFeedAsync call, per feed.</summary>
    public Dictionary<FeedKind, bool> LastForceRefresh { get; } = new();

    /// <summary>Every GetFeedPageAsync call in order, so a test can prove which page was asked for.</summary>
    public List<(FeedKind Kind, int Page)> PageRequests { get; } = [];

    /// <summary>Every SearchAsync call in order, query included.</summary>
    public List<(string Query, int Page)> SearchRequests { get; } = [];

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

    /// <summary>Canned answer for one page of one feed. Unconfigured pages come back empty and ended.</summary>
    public FakeFeedService SetPage(FeedKind kind, int page, FeedPage result)
    {
        _feedPages[(kind, page)] = result;
        return this;
    }

    /// <summary>Canned answer for one page of one query. Unconfigured pages come back empty and ended.</summary>
    public FakeFeedService SetSearchPage(string query, int page, FeedPage result)
    {
        _searchPages[(query, page)] = result;
        return this;
    }

    /// <summary>Holds every page and search call open until <see cref="Resume"/>, to race two callers.</summary>
    public void Pause() => _pause = new TaskCompletionSource();

    public void Resume()
    {
        var pause = _pause;
        _pause = null;
        pause?.SetResult();
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

    /// <summary>What GetOverviewAsync returns, before <c>perFeed</c> is applied.</summary>
    public IReadOnlyList<FeedGroup> OverviewGroups { get; set; } = [];

    public int OverviewCallCount { get; private set; }

    public Task<IReadOnlyList<FeedGroup>> GetOverviewAsync(int perFeed, CancellationToken cancellationToken = default)
    {
        OverviewCallCount++;

        if (ExceptionToThrow is { } exception)
        {
            throw exception;
        }

        return Task.FromResult<IReadOnlyList<FeedGroup>>(
            OverviewGroups.Select(group => new FeedGroup(group.Kind, group.Items.Take(Math.Max(perFeed, 0)).ToList())).ToList());
    }

    public async Task<FeedPage> GetFeedPageAsync(FeedKind kind, int page, CancellationToken cancellationToken = default)
    {
        PageRequests.Add((kind, page));

        if (ExceptionToThrow is { } exception)
        {
            throw exception;
        }

        await WaitForResumeAsync();

        return _feedPages.TryGetValue((kind, page), out var result)
            ? result
            : FeedPage.End(FeedResultStatus.Fresh);
    }

    public async Task<FeedPage> SearchAsync(string query, int page, CancellationToken cancellationToken = default)
    {
        SearchRequests.Add((query, page));

        if (ExceptionToThrow is { } exception)
        {
            throw exception;
        }

        await WaitForResumeAsync();

        return _searchPages.TryGetValue((query, page), out var result)
            ? result
            : FeedPage.End(FeedResultStatus.Fresh);
    }

    private async Task WaitForResumeAsync()
    {
        if (_pause is { } pause)
        {
            await pause.Task;
        }
    }
}
