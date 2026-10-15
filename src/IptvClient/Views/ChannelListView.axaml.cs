using System;
using Avalonia.Controls;
using Avalonia.Input;
using IptvClient.Core.Models;
using IptvClient.ViewModels;

namespace IptvClient.Views;

public partial class ChannelListView : UserControl
{
    // Below this, the groups list + items list can't sit side by side without both being too
    // cramped to actually read — 240 (groups column) + 16 (gap) + enough for a channel row
    // (44px logo + name + group-title) to not truncate immediately.
    private const double NarrowBreakpoint = 700;

    public ChannelListView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => ApplyNarrowLayout(e.NewSize.Width);
        DataContextChanged += (_, _) => ApplyNarrowLayout(Bounds.Width);
    }

    private void ApplyNarrowLayout(double width)
    {
        if (DataContext is ChannelListViewModel viewModel)
        {
            viewModel.IsNarrowLayout = width < NarrowBreakpoint;
        }
    }

    /// <summary>Gives the channel list keyboard/D-pad focus (shown with the focus ring).</summary>
    public void FocusItems() => ItemsList.Focus(NavigationMethod.Directional);

    // Touch: a single tap plays (double-tap is a desktop mouse idiom).
    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
        {
            PlayFromEvent(e);
        }
    }

    // Remote / keyboard: OK / Enter plays the focused channel.
    private void OnItemsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ItemsList.SelectedItem is Channel channel && DataContext is ChannelListViewModel viewModel)
        {
            viewModel.PlayCommand.Execute(channel);
            e.Handled = true;
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!OperatingSystem.IsAndroid())
        {
            PlayFromEvent(e);
        }
    }

    private void PlayFromEvent(TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: Channel channel } && DataContext is ChannelListViewModel viewModel)
        {
            viewModel.PlayCommand.Execute(channel);
        }
    }
}
