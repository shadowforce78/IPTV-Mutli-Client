using Avalonia.Controls;
using Avalonia.Input;
using IptvClient.ViewModels;

namespace IptvClient.Views;

public partial class PlayerControlsView : UserControl
{
    public PlayerControlsView()
    {
        InitializeComponent();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e) =>
        (DataContext as MainViewModel)?.NotifyPointerActivity();
}
