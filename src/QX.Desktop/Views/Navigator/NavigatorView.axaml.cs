using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Qx.Presentation.ViewModels.Navigator;

namespace Qx.Desktop.Views.Navigator;

public sealed partial class NavigatorView : UserControl
{
    public NavigatorView()
    {
        InitializeComponent();
        RoomGrid.ContextRequested += OnContextRequested;
        RoomGrid.DoubleTapped += OnRoomTapped;
        RoomGrid.AddHandler(KeyDownEvent, OnGridKey, RoutingStrategies.Bubble);
        QueryBox.AddHandler(KeyDownEvent, OnQueryKey, RoutingStrategies.Bubble);
    }

    public void Release()
    {
        RoomGrid.ContextRequested -= OnContextRequested;
        RoomGrid.DoubleTapped -= OnRoomTapped;
        RoomGrid.RemoveHandler(KeyDownEvent, OnGridKey);
        QueryBox.RemoveHandler(KeyDownEvent, OnQueryKey);
    }

    void OnContextRequested(object? sender, ContextRequestedEventArgs args)
    {
        if (DataContext is NavigatorViewModel page)
            page.RefreshMenu();
    }

    void OnRoomTapped(object? sender, TappedEventArgs args)
    {
        if (DataContext is not NavigatorViewModel page)
            return;
        if ((args.Source as Visual)?.FindAncestorOfType<DataGridRow>(true)?.DataContext is not RoomRow row)
            return;
        args.Handled = true;
        Enter(page, row);
    }

    void OnGridKey(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || DataContext is not NavigatorViewModel page)
            return;
        if (page.Selection.First is not { } row)
            return;
        args.Handled = true;
        Enter(page, row);
    }

    void OnQueryKey(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || DataContext is not NavigatorViewModel page)
            return;
        args.Handled = true;
        if (page.SearchCommand.CanExecute(null))
            page.SearchCommand.Execute(null);
    }

    static void Enter(NavigatorViewModel page, RoomRow row)
    {
        if (page.EnterCommand.CanExecute(row))
            page.EnterCommand.Execute(row);
    }
}
