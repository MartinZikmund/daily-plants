using System.Text.Json;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// <see cref="IFeedCache"/> over %LocalAppData%/DailyPlants/FeedCache, one JSON document per feed.
/// </summary>
public sealed class JsonFeedCache : IFeedCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _cacheDirectory;

    public JsonFeedCache()
        : this(GetDefaultCacheDirectory())
    {
    }

    public JsonFeedCache(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        EnsureDirectory();
    }

    public async Task<CachedFeed?> ReadAsync(FeedKind kind, CancellationToken cancellationToken = default)
    {
        var path = GetPath(kind);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<CachedFeed>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Feed cache read failed for {kind}", ex);
            return null;
        }
    }

    public async Task WriteAsync(CachedFeed feed, CancellationToken cancellationToken = default)
    {
        var path = GetPath(feed.Kind);
        var temporaryPath = path + ".tmp";

        try
        {
            EnsureDirectory();

            // Write aside then move: a process killed mid-write must not leave a half-written cache.
            var json = JsonSerializer.Serialize(feed, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Feed cache write failed for {feed.Kind}", ex);
        }
    }

    private string GetPath(FeedKind kind) => Path.Combine(_cacheDirectory, $"{kind.ToString().ToLowerInvariant()}.json");

    /// <summary>Best effort: a location the head will not let us write to has to degrade to "no cache", not crash.</summary>
    private void EnsureDirectory()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"Feed cache directory unavailable: {_cacheDirectory}", ex);
        }
    }

    /// <summary>%LocalAppData%/DailyPlants/FeedCache - same root the SQLite database uses.</summary>
    private static string GetDefaultCacheDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "DailyPlants", "FeedCache");
    }
}
