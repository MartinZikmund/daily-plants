namespace DailyPlants.Models;

/// <summary>
/// A single entry from a nutritionfacts.org RSS feed.
/// </summary>
public sealed partial record FeedItem
{
    /// <summary>The feed's &lt;guid&gt;. Stable across slug changes; used for identity, dedupe and AutomationId.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Which feed this entry came from.
    /// </summary>
    public FeedKind Kind { get; init; }

    /// <summary>&lt;title&gt;, already entity-decoded by the XML parser.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>&lt;link&gt; - the URL handed to the system browser. Never &lt;enclosure&gt;.</summary>
    public string Link { get; init; } = string.Empty;

    /// <summary>&lt;description&gt;, HTML-decoded, tags stripped, whitespace collapsed. Never truncated here.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>&lt;dc:creator&gt;, HTML-decoded. Empty when absent.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>media:content/@url. Null when the item carries no thumbnail.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Parsed &lt;pubDate&gt;. Null when missing or unparseable.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}
