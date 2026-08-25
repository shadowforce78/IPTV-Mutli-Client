using System.Net.Http;
using IptvClient.Core.Models;

namespace IptvClient.Core.M3u;

/// <summary>Fetches an M3U playlist from an http(s) URL or a local file path, then parses it.</summary>
public static class PlaylistLoader
{
    public static async Task<M3uPlaylist> LoadAsync(
        string source, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            await using var stream = await httpClient.GetStreamAsync(uri, cancellationToken);
            return await M3uParser.ParseAsync(stream, cancellationToken);
        }

        await using var fileStream = File.OpenRead(source);
        return await M3uParser.ParseAsync(fileStream, cancellationToken);
    }
}
