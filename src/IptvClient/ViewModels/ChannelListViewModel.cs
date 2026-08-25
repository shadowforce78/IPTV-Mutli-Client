using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using IptvClient.Core.Models;

namespace IptvClient.ViewModels;

/// <summary>
/// Groups a flat list of channels by group-title and exposes a searchable, filtered view —
/// shared between the Live and VOD tabs, which only differ in which channels they're given.
/// </summary>
public partial class ChannelListViewModel : ViewModelBase
{
    private const string UngroupedLabel = "(Sans catégorie)";

    private IReadOnlyList<Channel> _allItems = [];

    public ObservableCollection<ChannelGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Channel> Items { get; set; } = [];

    [ObservableProperty]
    public partial ChannelGroup? SelectedGroup { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public void SetItems(IReadOnlyList<Channel> items)
    {
        _allItems = items;

        Groups.Clear();
        foreach (var group in items.GroupBy(GroupKey).OrderByDescending(g => g.Count()))
        {
            Groups.Add(new ChannelGroup(group.Key, group.Count()));
        }

        SelectedGroup = Groups.Count > 0 ? Groups[0] : null;
        RefreshItems();
    }

    partial void OnSelectedGroupChanged(ChannelGroup? value) => RefreshItems();

    partial void OnSearchTextChanged(string value) => RefreshItems();

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
