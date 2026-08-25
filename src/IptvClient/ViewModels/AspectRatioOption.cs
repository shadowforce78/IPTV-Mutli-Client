using System.Collections.Generic;

namespace IptvClient.ViewModels;

/// <summary>Value is passed straight to MediaPlayer.AspectRatio (libVLC's own "W:H" string
/// format); null means "auto" — let libVLC use the source's own aspect ratio.</summary>
public sealed record AspectRatioOption(string Label, string? Value)
{
    public static readonly AspectRatioOption Auto = new("Auto", null);

    public static readonly IReadOnlyList<AspectRatioOption> All =
    [
        Auto,
        new("16:9", "16:9"),
        new("4:3", "4:3"),
        new("21:9", "21:9"),
        new("1:1", "1:1"),
    ];
}
