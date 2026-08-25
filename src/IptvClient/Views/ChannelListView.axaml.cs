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

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: Channel channel } && DataContext is ChannelListViewModel viewModel)
        {
            viewModel.PlayCommand.Execute(channel);
        }
    }
}
