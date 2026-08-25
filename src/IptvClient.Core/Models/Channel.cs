namespace IptvClient.Core.Models;

/// <summary>
/// One playable entry from an M3U playlist (a channel, VOD item, etc.).
/// </summary>
public sealed record Channel
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    /// <summary>Duration in seconds from #EXTINF. Live streams conventionally use -1.</summary>
    public double? DurationSeconds { get; init; }

    public string? GroupTitle { get; init; }

    public string? TvgId { get; init; }

    public string? TvgName { get; init; }

    public string? TvgLogo { get; init; }

    /// <summary>
    /// Every key="value" pair read off the #EXTINF line, tvg-id/tvg-name/tvg-logo/group-title
    /// included — the properties above are just convenience accessors into this bag. Keyed
    /// case-insensitively since providers are inconsistent about casing.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Any other #EXT... directive line seen between #EXTINF and the stream URL
    /// (#EXTVLCOPT, #EXTGRP, #KODIPROP, ...), kept verbatim. Nothing is parsed out of these
    /// yet — revisit once we've seen which tags real playlists actually rely on.
    /// </summary>
    public IReadOnlyList<string> ExtraTags { get; init; } = [];
}
