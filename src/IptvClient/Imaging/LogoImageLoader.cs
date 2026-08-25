using System;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader.Loaders;
using Avalonia.Media.Imaging;

namespace IptvClient.Imaging;

/// <summary>
/// Wraps the default RAM-cached web loader to also handle tvg-logo values that are inline
/// data: URIs (base64) instead of http(s) links — some IPTV providers embed logos directly
/// rather than hosting them (seen for ~150 channels in real playlists tested against).
/// </summary>
public sealed class LogoImageLoader : RamCachedWebImageLoader
{
    public override Task<Bitmap?> ProvideImageAsync(string url)
    {
        return url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? Task.FromResult(DecodeDataUri(url))
            : base.ProvideImageAsync(url);
    }

    private static Bitmap? DecodeDataUri(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0)
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(dataUri[(commaIndex + 1)..]);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            // Malformed base64 or an image format Skia can't decode — skip the logo rather
            // than take the whole list down over one bad entry.
            return null;
        }
    }
}
