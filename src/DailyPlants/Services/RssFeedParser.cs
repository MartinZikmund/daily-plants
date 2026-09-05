using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DailyPlants.Models;

namespace DailyPlants.Services;

/// <summary>
/// Parses a nutritionfacts.org RSS document. Pure: no I/O, no clock, no statics beyond the namespaces.
/// </summary>
public static class RssFeedParser
{
    /// <summary>Every sampled item uses exactly this RFC 822 shape; invariant so a Czech phone parses it too.</summary>
    private const string PubDateFormat = "ddd, dd MMM yyyy HH:mm:ss zzz";

    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="xml"/> into feed items, newest first as the feed orders them.
    /// Returns an empty list for a well-formed document with no items.
    /// Throws <see cref="System.Xml.XmlException"/> for malformed XML; callers catch and fall back to cache.
    /// </summary>
    public static IReadOnlyList<FeedItem> Parse(string xml, FeedKind kind) => ParseItems(xml, kind);

    /// <summary>
    /// Parses a document whose items are of mixed kinds - the site-wide search feed - deriving each
    /// item's <see cref="FeedItem.Kind"/> from its link. Same failure modes as <see cref="Parse"/>.
    /// </summary>
    public static IReadOnlyList<FeedItem> ParseMixed(string xml) => ParseItems(xml, kind: null);

    /// <summary>
    /// The kind a search hit belongs to, read off its permalink: /blog/ is a post, /video/ a video,
    /// /audio/ a podcast episode. Anything else - /questions/ today - is
    /// <see cref="FeedKind.Other"/>; guessing Blog would mislabel the card.
    /// </summary>
    public static FeedKind KindFromLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return FeedKind.Other;
        }

        if (link.Contains("/blog/", StringComparison.OrdinalIgnoreCase))
        {
            return FeedKind.Blog;
        }

        if (link.Contains("/video/", StringComparison.OrdinalIgnoreCase))
        {
            return FeedKind.Videos;
        }

        if (link.Contains("/audio/", StringComparison.OrdinalIgnoreCase))
        {
            return FeedKind.Podcast;
        }

        return FeedKind.Other;
    }

    /// <param name="kind">Null stamps each item with <see cref="KindFromLink"/> instead.</param>
    private static IReadOnlyList<FeedItem> ParseItems(string xml, FeedKind? kind)
    {
        XDocument document = XDocument.Parse(xml);

        // Only rss/channel/item: the channel carries its own title/link/description and a
        // document-wide search would yield a spurious extra "item".
        var elements = document.Root?.Element("channel")?.Elements("item");
        if (elements is null)
        {
            return [];
        }

        List<FeedItem> items = new();
        foreach (var element in elements)
        {
            if (ParseItem(element, kind) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static FeedItem? ParseItem(XElement element, FeedKind? kind)
    {
        var link = Value(element.Element("link"));

        // guid survives slug changes, so it is the identity; link is only the fallback.
        var id = Value(element.Element("guid"));
        if (id.Length == 0)
        {
            id = link;
        }

        if (id.Length == 0)
        {
            return null;
        }

        return new FeedItem
        {
            Id = id,
            Kind = kind ?? KindFromLink(link),
            Title = Value(element.Element("title")),
            Link = link,
            Summary = CleanSummary(ReadSummarySource(element)),
            Author = WebUtility.HtmlDecode(Value(element.Element(Dc + "creator"))),
            ThumbnailUrl = ReadThumbnailUrl(element),
            PublishedAt = ParsePublishedAt(Value(element.Element("pubDate")))
        };
    }

    private static string ReadSummarySource(XElement element)
    {
        var description = Value(element.Element("description"));
        return description.Length > 0 ? description : Value(element.Element(Content + "encoded"));
    }

    /// <summary>Element lookup, not string scanning: media:keywords can butt against media:content with no whitespace.</summary>
    private static string? ReadThumbnailUrl(XElement element)
        => element.Elements(Media + "content").FirstOrDefault()?.Attribute("url")?.Value is { Length: > 0 } url
            ? url
            : null;

    /// <summary>The feed's description arrives as CDATA, so its entities and markup are still literal text.</summary>
    private static string CleanSummary(string raw)
    {
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(raw);
        var withoutTags = HtmlTagRegex.Replace(decoded, " ");
        return WhitespaceRegex.Replace(withoutTags, " ").Trim();
    }

    private static DateTimeOffset? ParsePublishedAt(string value)
        => DateTimeOffset.TryParseExact(value, PubDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var published)
            ? published
            : null;

    private static string Value(XElement? element) => element?.Value.Trim() ?? string.Empty;
}
