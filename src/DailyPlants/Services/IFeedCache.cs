using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Reads and writes the on-disk JSON cache of parsed feeds.
/// </summary>
public interface IFeedCache
{
    /// <summary>
    /// Returns the cached document for <paramref name="kind"/>, or null when nothing is cached
    /// or the file cannot be read/deserialized. Never throws.
    /// </summary>
    Task<CachedFeed?> ReadAsync(FeedKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the cached document for <paramref name="feed"/>.Kind. Logs and swallows I/O failures
    /// (a cache write failing must never fail the user's request). Never throws.
    /// </summary>
    Task WriteAsync(CachedFeed feed, CancellationToken cancellationToken = default);
}
