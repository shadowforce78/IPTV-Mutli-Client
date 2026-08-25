namespace IptvClient.Core.Models;

/// <summary>Result of parsing an M3U/M3U8 file: its entries plus any header-level metadata.</summary>
public sealed record M3uPlaylist
{
    /// <summary>From an optional #PLAYLIST: line. Null when the source didn't set one.</summary>
    public string? Name { get; init; }

    /// <summary>Attributes found on the #EXTM3U header line itself (e.g. url-tvg / x-tvg-url for EPG).</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Channel> Channels { get; init; } = [];
}
