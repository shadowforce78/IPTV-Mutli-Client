namespace IptvClient.ViewModels;

/// <summary>One selectable libVLC audio/subtitle track — Id is libVLC's own track id, used
/// as-is with MediaPlayer.SetAudioTrack/SetSpu (not IptvClient.Core.Models.Channel's Id space).</summary>
public sealed record TrackOption(int Id, string Label);
