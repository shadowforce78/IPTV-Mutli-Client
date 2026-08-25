using IptvClient.Core.Models;

namespace IptvClient.Core.M3u;

/// <summary>
/// Best-effort Live/Movie/Series split. This isn't part of the M3U spec — it recognizes the
/// /movie/ and /series/ URL path convention Xtream Codes panels use, which is what most IPTV
/// reseller playlists are generated from. Anything that doesn't match defaults to Live.
/// </summary>
public static class ContentClassifier
{
    public static ContentKind Classify(Channel channel)
    {
        var path = Uri.TryCreate(channel.Url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : channel.Url;

        if (path.Contains("/movie/", StringComparison.OrdinalIgnoreCase))
        {
            return ContentKind.Movie;
        }

        if (path.Contains("/series/", StringComparison.OrdinalIgnoreCase))
        {
            return ContentKind.Series;
        }

        return ContentKind.Live;
    }
}
