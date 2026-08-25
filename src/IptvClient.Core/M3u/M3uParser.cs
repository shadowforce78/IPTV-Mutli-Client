using System.Globalization;
using System.Text.RegularExpressions;
using IptvClient.Core.Models;

namespace IptvClient.Core.M3u;

/// <summary>Parses the extended M3U format IPTV playlists use (#EXTM3U / #EXTINF).</summary>
public static partial class M3uParser
{
    [GeneratedRegex("([A-Za-z0-9_-]+)=\"([^\"]*)\"")]
    private static partial Regex AttributePattern();

    public static M3uPlaylist Parse(string content)
    {
        using var reader = new StringReader(content);
        return Parse(reader);
    }

    public static async Task<M3uPlaylist> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return Parse(content);
    }

    public static M3uPlaylist Parse(TextReader reader)
    {
        string? playlistName = null;
        var playlistAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var channels = new List<Channel>();

        double? pendingDuration = null;
        string? pendingName = null;
        Dictionary<string, string>? pendingAttributes = null;
        List<string>? pendingExtraTags = null;

        var isFirstLine = true;
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            var line = isFirstLine ? rawLine.TrimStart('﻿') : rawLine;
            isFirstLine = false;

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (key, value) in ParseAttributes(trimmed))
                {
                    playlistAttributes[key] = value;
                }

                continue;
            }

            if (trimmed.StartsWith("#PLAYLIST:", StringComparison.OrdinalIgnoreCase))
            {
                playlistName = trimmed["#PLAYLIST:".Length..].Trim();
                continue;
            }

            if (trimmed.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                (pendingDuration, pendingAttributes, pendingName) = ParseExtInf(trimmed);
                pendingExtraTags = null;
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                // Directive between #EXTINF and the URL: #EXTVLCOPT, #EXTGRP, #KODIPROP, ...
                // Kept verbatim rather than dropped, in case downstream code wants it later.
                if (pendingName is not null)
                {
                    (pendingExtraTags ??= []).Add(trimmed);

                    if (trimmed.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase)
                        && pendingAttributes is not null
                        && !pendingAttributes.ContainsKey("group-title"))
                    {
                        pendingAttributes["group-title"] = trimmed["#EXTGRP:".Length..].Trim();
                    }
                }

                continue;
            }

            // Anything else non-blank is the stream URL, closing out the pending entry.
            // A bare URL with no preceding #EXTINF (plain, non-extended M3U) is also valid —
            // fall back to the URL itself as the display name.
            pendingAttributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            channels.Add(new Channel
            {
                Name = string.IsNullOrEmpty(pendingName) ? trimmed : pendingName,
                Url = trimmed,
                DurationSeconds = pendingDuration,
                GroupTitle = pendingAttributes.GetValueOrDefault("group-title"),
                TvgId = pendingAttributes.GetValueOrDefault("tvg-id"),
                TvgName = pendingAttributes.GetValueOrDefault("tvg-name"),
                TvgLogo = pendingAttributes.GetValueOrDefault("tvg-logo"),
                Attributes = pendingAttributes,
                ExtraTags = (IReadOnlyList<string>?)pendingExtraTags ?? [],
            });

            pendingDuration = null;
            pendingName = null;
            pendingAttributes = null;
            pendingExtraTags = null;
        }

        return new M3uPlaylist
        {
            Name = playlistName,
            Attributes = playlistAttributes,
            Channels = channels,
        };
    }

    private static (double? Duration, Dictionary<string, string> Attributes, string Name) ParseExtInf(string line)
    {
        var body = line["#EXTINF:".Length..];

        // The display name is everything after the comma that follows the last attribute's
        // closing quote — not the first comma in the line, since a quoted attribute value
        // (or the name itself) may contain commas of its own.
        var lastQuote = body.LastIndexOf('"');
        var commaIndex = body.IndexOf(',', Math.Max(lastQuote, 0));

        string head;
        string name;
        if (commaIndex < 0)
        {
            head = body;
            name = string.Empty;
        }
        else
        {
            head = body[..commaIndex];
            name = body[(commaIndex + 1)..].Trim();
        }

        var durationEnd = 0;
        while (durationEnd < head.Length && (char.IsAsciiDigit(head[durationEnd]) || head[durationEnd] is '-' or '.'))
        {
            durationEnd++;
        }

        double? duration = durationEnd > 0
            && double.TryParse(head[..durationEnd], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

        return (duration, ParseAttributes(head), name);
    }

    private static Dictionary<string, string> ParseAttributes(string text)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in AttributePattern().Matches(text).Cast<Match>())
        {
            attributes[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return attributes;
    }
}
