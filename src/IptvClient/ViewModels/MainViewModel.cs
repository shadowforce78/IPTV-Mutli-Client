using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using IptvClient.Core.M3u;
using IptvClient.Core.Models;

namespace IptvClient.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient = new();

    public ChannelListViewModel Live { get; } = new();

    public ChannelListViewModel Vod { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Aucune playlist chargée.";

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
}
