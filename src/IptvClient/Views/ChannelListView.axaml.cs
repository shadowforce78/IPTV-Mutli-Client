using Avalonia.Controls;
using Avalonia.Input;
using IptvClient.Core.Models;
using IptvClient.ViewModels;

namespace IptvClient.Views;

public partial class ChannelListView : UserControl
{
    public ChannelListView()
    {
        InitializeComponent();
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: Channel channel } && DataContext is ChannelListViewModel viewModel)
        {
            viewModel.PlayCommand.Execute(channel);
        }
    }
}
