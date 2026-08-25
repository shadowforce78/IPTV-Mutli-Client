using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IptvClient.Core.M3u;
using IptvClient.Core.Models;
using LibVLCSharp.Shared;

namespace IptvClient.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly LibVLC _libVlc;
    private readonly DispatcherTimer _hideControlsTimer;
    private bool _isSyncingPosition;

    public MainViewModel()
    {
        _hideControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideControlsTimer.Tick += (_, _) =>
        {
            _hideControlsTimer.Stop();
            if (IsPlaying)
            {
                IsControlsVisible = false;
            }
        };

        // Tuned down from libVLC's fairly conservative default so a channel switch starts
        // playing quickly — this is the whole point of the app per the README (VLC "nu" and
        // Kodi addons both feel slow to start a stream). Revisit this value against real
        // playback if it turns out too aggressive for a given provider's latency.
        _libVlc = new LibVLC("--network-caching=1500", "--no-video-title-show");
        MediaPlayer = new MediaPlayer(_libVlc);

        // These fire on a libvlc-internal thread, not the UI thread — every handler has to
        // hop back via the dispatcher before touching an [ObservableProperty].
        MediaPlayer.Playing += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = true;
            RefreshTrackInfo();
        });
        MediaPlayer.Paused += (_, _) => Dispatcher.UIThread.Post(() => IsPlaying = false);
        MediaPlayer.Stopped += (_, _) => Dispatcher.UIThread.Post(() => IsPlaying = false);
        MediaPlayer.EndReached += (_, _) => Dispatcher.UIThread.Post(() => IsPlaying = false);
        MediaPlayer.Muted += (_, _) => Dispatcher.UIThread.Post(() => IsMuted = true);
        MediaPlayer.Unmuted += (_, _) => Dispatcher.UIThread.Post(() => IsMuted = false);
        MediaPlayer.ESAdded += (_, _) => Dispatcher.UIThread.Post(RefreshTrackInfo);
        MediaPlayer.TimeChanged += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            _isSyncingPosition = true;
            PositionSeconds = e.Time / 1000.0;
            _isSyncingPosition = false;
        });
        MediaPlayer.LengthChanged += (_, e) => Dispatcher.UIThread.Post(() => DurationSeconds = Math.Max(0, e.Length / 1000.0));
        MediaPlayer.SeekableChanged += (_, e) => Dispatcher.UIThread.Post(() => IsSeekable = e.Seekable != 0);

        Live.PlayRequested += PlayChannel;
        Vod.PlayRequested += PlayChannel;
    }

    public ChannelListViewModel Live { get; } = new();

    public ChannelListViewModel Vod { get; } = new();

    public MediaPlayer MediaPlayer { get; }

    public ObservableCollection<TrackOption> AudioTracks { get; } = [];

    public ObservableCollection<TrackOption> SubtitleTracks { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Aucune playlist chargée.";

    [ObservableProperty]
    public partial Channel? CurrentChannel { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    [ObservableProperty]
    public partial bool IsFullscreen { get; set; }

    [ObservableProperty]
    public partial bool IsControlsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSeekable { get; set; }

    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    [ObservableProperty]
    public partial TrackOption? SelectedAudioTrack { get; set; }

    [ObservableProperty]
    public partial TrackOption? SelectedSubtitleTrack { get; set; }

    [ObservableProperty]
    public partial string? StreamInfoText { get; set; }

    /// <summary>0-100, mirrors libVLC's own range. Two-way bound to the volume slider.</summary>
    [ObservableProperty]
    public partial int Volume { get; set; } = 100;

    /// <summary>Companion negations for the icon-swap bindings in MainView.axaml (two overlaid
    /// icons, each visible for one state) — kept as real properties rather than a `!Binding`
    /// in XAML so there's one obvious place these states live.</summary>
    public bool IsPaused => !IsPlaying;

    public bool IsUnmuted => !IsMuted;

    public bool IsWindowed => !IsFullscreen;

    public string PositionLabel => FormatTime(PositionSeconds);

    public string DurationLabel => FormatTime(DurationSeconds);

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPaused));

        if (value)
        {
            NotifyPointerActivity();
        }
        else
        {
            // Paused: keep the controls up rather than fading them out from under the user.
            _hideControlsTimer.Stop();
            IsControlsVisible = true;
        }
    }

    partial void OnIsMutedChanged(bool value) => OnPropertyChanged(nameof(IsUnmuted));

    partial void OnIsFullscreenChanged(bool value) => OnPropertyChanged(nameof(IsWindowed));

    partial void OnVolumeChanged(int value) => MediaPlayer.Volume = value;

    partial void OnPositionSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(PositionLabel));

        // Only push back to the player when this change came from the user dragging the
        // slider — not when we're the ones setting it to mirror MediaPlayer.TimeChanged.
        if (!_isSyncingPosition && MediaPlayer.IsSeekable)
        {
            MediaPlayer.Time = (long)(value * 1000);
        }
    }

    partial void OnDurationSecondsChanged(double value) => OnPropertyChanged(nameof(DurationLabel));

    partial void OnSelectedAudioTrackChanged(TrackOption? value)
    {
        if (value is not null)
        {
            MediaPlayer.SetAudioTrack(value.Id);
        }
    }

    partial void OnSelectedSubtitleTrackChanged(TrackOption? value)
    {
        if (value is not null)
        {
            MediaPlayer.SetSpu(value.Id);
        }
    }

    public async Task LoadPlaylistAsync(string source)
    {
        IsLoading = true;
        StatusMessage = "Chargement de la playlist…";

        try
        {
            var playlist = await PlaylistLoader.LoadAsync(source, _httpClient);

            var live = new List<Channel>();
            var vod = new List<Channel>();
            foreach (var channel in playlist.Channels)
            {
                var bucket = ContentClassifier.Classify(channel) == ContentKind.Live ? live : vod;
                bucket.Add(channel);
            }

            Live.SetItems(live);
            Vod.SetItems(vod);
            StatusMessage = $"{live.Count} chaînes live · {vod.Count} films/séries";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Échec du chargement : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PlayChannel(Channel channel)
    {
        CurrentChannel = channel;
        StatusMessage = $"Lecture : {channel.Name}";

        PositionSeconds = 0;
        DurationSeconds = 0;
        IsSeekable = false;
        AudioTracks.Clear();
        SubtitleTracks.Clear();
        StreamInfoText = null;
        NotifyPointerActivity();

        using var media = new Media(_libVlc, new Uri(channel.Url));
        MediaPlayer.Play(media);
    }

    /// <summary>Called from the View on any pointer movement over the video area — shows the
    /// overlay controls immediately and restarts the auto-hide countdown.</summary>
    public void NotifyPointerActivity()
    {
        IsControlsVisible = true;
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (MediaPlayer.IsPlaying)
        {
            MediaPlayer.Pause();
        }
        else
        {
            MediaPlayer.Play();
        }
    }

    [RelayCommand]
    private void ToggleMute() => MediaPlayer.Mute = !MediaPlayer.Mute;

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void SkipBack() => Skip(-10);

    [RelayCommand]
    private void SkipForward() => Skip(10);

    private void Skip(int deltaSeconds)
    {
        // Live streams are usually not seekable at all (or only within a small live-edge
        // buffer) — IsSeekable is libVLC's own per-stream signal for that.
        if (!MediaPlayer.IsSeekable)
        {
            return;
        }

        var target = Math.Max(0, MediaPlayer.Time + deltaSeconds * 1000L);
        var length = MediaPlayer.Length;
        if (length > 0)
        {
            target = Math.Min(target, length);
        }

        MediaPlayer.Time = target;
    }

    /// <summary>Rebuilds the audio/subtitle pickers and the on-screen stream info from
    /// whatever libVLC has demuxed so far — called once playback starts and again whenever a
    /// new elementary stream is discovered, since not everything is known immediately.</summary>
    private void RefreshTrackInfo()
    {
        var media = MediaPlayer.Media;

        var previousAudio = SelectedAudioTrack?.Id;
        AudioTracks.Clear();
        foreach (var description in MediaPlayer.AudioTrackDescription)
        {
            AudioTracks.Add(new TrackOption(description.Id, description.Name));
        }

        SelectedAudioTrack = AudioTracks.FirstOrDefault(t => t.Id == MediaPlayer.AudioTrack)
            ?? AudioTracks.FirstOrDefault(t => t.Id == previousAudio);

        var previousSubtitle = SelectedSubtitleTrack?.Id;
        SubtitleTracks.Clear();
        foreach (var description in MediaPlayer.SpuDescription)
        {
            SubtitleTracks.Add(new TrackOption(description.Id, description.Name));
        }

        SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(t => t.Id == MediaPlayer.Spu)
            ?? SubtitleTracks.FirstOrDefault(t => t.Id == previousSubtitle);

        StreamInfoText = media is null ? null : BuildStreamInfoText(media);
    }

    private static string BuildStreamInfoText(Media media)
    {
        var tracks = media.Tracks;
        var lines = new List<string>();

        foreach (var track in tracks.Where(t => t.TrackType == TrackType.Video))
        {
            var codec = media.CodecDescription(TrackType.Video, track.Codec);
            var width = track.Data.Video.Width;
            var height = track.Data.Video.Height;
            var fps = track.Data.Video.FrameRateDen > 0
                ? (double)track.Data.Video.FrameRateNum / track.Data.Video.FrameRateDen
                : 0;
            lines.Add(fps > 0
                ? $"Vidéo : {width}x{height} · {fps:0.##} fps · {codec}"
                : $"Vidéo : {width}x{height} · {codec}");
        }

        foreach (var track in tracks.Where(t => t.TrackType == TrackType.Audio))
        {
            var label = !string.IsNullOrWhiteSpace(track.Description) ? track.Description
                : !string.IsNullOrWhiteSpace(track.Language) ? track.Language
                : "inconnue";
            var codec = media.CodecDescription(TrackType.Audio, track.Codec);
            lines.Add($"Audio : {label} · {codec} · {track.Data.Audio.Channels}ch · {track.Data.Audio.Rate}Hz");
        }

        foreach (var track in tracks.Where(t => t.TrackType == TrackType.Text))
        {
            var label = !string.IsNullOrWhiteSpace(track.Description) ? track.Description
                : !string.IsNullOrWhiteSpace(track.Language) ? track.Language
                : "inconnue";
            lines.Add($"Sous-titres : {label}");
        }

        return lines.Count > 0 ? string.Join('\n', lines) : "Aucune info de flux disponible pour l'instant.";
    }

    private static string FormatTime(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        _hideControlsTimer.Stop();
        MediaPlayer.Dispose();
        _libVlc.Dispose();
        _httpClient.Dispose();
    }
}
