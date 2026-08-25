using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IptvClient.Core.Models;

namespace IptvClient.ViewModels;

/// <summary>
/// Groups a flat list of channels by group-title and exposes a searchable, filtered view —
/// shared between the Live and VOD tabs, which only differ in which channels they're given.
///
/// Also owns the narrow-screen navigation state (groups panel vs. items panel, like a phone's
/// drill-down list) — the view (ChannelListView) measures its own width and just tells us
/// <see cref="IsNarrowLayout"/>; everything about which panel that implies is derived here so
/// there's one place that owns "what does narrow vs. wide actually look like".
/// </summary>
public partial class ChannelListViewModel : ViewModelBase
{
    private const string UngroupedLabel = "(Sans catégorie)";

    private IReadOnlyList<Channel> _allItems = [];
    private bool _suppressNavigation;

    public ObservableCollection<ChannelGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Channel> Items { get; set; } = [];

    [ObservableProperty]
    public partial ChannelGroup? SelectedGroup { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Set from the view based on its measured width — below the breakpoint there's
    /// not enough room for the groups and items lists side by side.</summary>
    [ObservableProperty]
    public partial bool IsNarrowLayout { get; set; }

    /// <summary>Narrow-layout navigation state only: true once the user has drilled into a
    /// group (or searched — search "overrides" whichever panel is showing). Irrelevant when
    /// !IsNarrowLayout, since both panels are simply shown side by side there.</summary>
    [ObservableProperty]
    public partial bool IsShowingItems { get; set; }

    public bool IsGroupsPanelVisible => !IsNarrowLayout || !IsShowingItems;

    public bool IsItemsPanelVisible => !IsNarrowLayout || IsShowingItems;

    public bool ShowBackButton => IsNarrowLayout && IsShowingItems;

    public int GroupsColumnSpan => IsNarrowLayout ? 3 : 1;

    public int ItemsColumn => IsNarrowLayout ? 0 : 2;

    public int ItemsColumnSpan => IsNarrowLayout ? 3 : 1;

    /// <summary>Raised when the view asks to play a channel (e.g. double-click) — the owner
    /// (MainViewModel) supplies the actual playback, this view-model only knows about browsing.</summary>
    public event Action<Channel>? PlayRequested;

    [RelayCommand]
    private void Play(Channel? channel)
    {
        if (channel is not null)
        {
            PlayRequested?.Invoke(channel);
        }
    }

    [RelayCommand]
    private void NavigateBack() => IsShowingItems = false;

    public void SetItems(IReadOnlyList<Channel> items)
    {
        _allItems = items;

        Groups.Clear();
        foreach (var group in items.GroupBy(GroupKey).OrderByDescending(g => g.Count()))
        {
            Groups.Add(new ChannelGroup(group.Key, group.Count()));
        }

        // Auto-selecting the first group here shouldn't itself count as the user "drilling in"
        // on a narrow screen — only an actual tap on a group (OnSelectedGroupChanged below,
        // with this flag clear) should navigate to the items panel.
        _suppressNavigation = true;
        SelectedGroup = Groups.Count > 0 ? Groups[0] : null;
        _suppressNavigation = false;

        IsShowingItems = false;
        RefreshItems();
    }

    partial void OnSelectedGroupChanged(ChannelGroup? value)
    {
        RefreshItems();

        if (!_suppressNavigation)
        {
            IsShowingItems = true;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshItems();

        // Search overrides whichever panel is currently showing — always jump straight to
        // results, even from the groups panel.
        if (!string.IsNullOrWhiteSpace(value))
        {
            IsShowingItems = true;
        }
    }

    partial void OnIsNarrowLayoutChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGroupsPanelVisible));
        OnPropertyChanged(nameof(IsItemsPanelVisible));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(GroupsColumnSpan));
        OnPropertyChanged(nameof(ItemsColumn));
        OnPropertyChanged(nameof(ItemsColumnSpan));
    }

    partial void OnIsShowingItemsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGroupsPanelVisible));
        OnPropertyChanged(nameof(IsItemsPanelVisible));
        OnPropertyChanged(nameof(ShowBackButton));
    }

    private void RefreshItems()
    {
        IEnumerable<Channel> source = _allItems;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            source = source.Where(c => c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }
        else if (SelectedGroup is not null)
        {
            source = source.Where(c => GroupKey(c) == SelectedGroup.Name);
        }

        Items = source.ToList();
    }

    private static string GroupKey(Channel channel) =>
        string.IsNullOrWhiteSpace(channel.GroupTitle) ? UngroupedLabel : channel.GroupTitle;
}
