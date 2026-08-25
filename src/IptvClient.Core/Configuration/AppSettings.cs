namespace IptvClient.Core.Configuration;

/// <summary>Local, git-ignored settings — see appsettings.Local.json next to the app.</summary>
public sealed record AppSettings
{
    public string? PlaylistUrl { get; init; }
}
