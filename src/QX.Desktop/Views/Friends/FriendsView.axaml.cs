using Avalonia.Controls;
using Avalonia.Input;
using Qx.Presentation.ViewModels.Friends;

namespace Qx.Desktop.Views.Friends;

public sealed partial class FriendsView : UserControl
{
    public FriendsView()
    {
        InitializeComponent();
        Rows.ContextRequested += OnContextRequested;
    }

    public void Release() => Rows.ContextRequested -= OnContextRequested;

    void OnContextRequested(object? sender, ContextRequestedEventArgs args)
    {
        if (DataContext is FriendsViewModel page)
            page.RefreshMenu();
    }
}
