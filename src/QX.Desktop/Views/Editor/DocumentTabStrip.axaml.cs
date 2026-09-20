using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Desktop.Views.Editor;

public sealed partial class DocumentTabStrip : UserControl
{
    public const double DragThreshold = 4;
    public const double NewButtonSlack = 36;

    ScriptDocument? _dragging;
    ScrollViewer? _scrolling;
    Point _origin;
    bool _moving;

    public DocumentTabStrip()
    {
        InitializeComponent();
        Tabs.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        Tabs.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Bubble);
        Tabs.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble);
        Tabs.AddHandler(Thumb.DragStartedEvent, OnScrollStarted);
        Tabs.AddHandler(Thumb.DragCompletedEvent, OnScrollCompleted);
        Tabs.ContextRequested += OnContextRequested;
        Tabs.SelectionChanged += OnSelectionChanged;
        Tabs.Tapped += OnTapped;
        Tabs.AddHandler(KeyDownEvent, OnTabKey, RoutingStrategies.Tunnel);
        SizeChanged += OnResized;
    }

    public void Release()
    {
        Tabs.RemoveHandler(PointerPressedEvent, OnPressed);
        Tabs.RemoveHandler(PointerMovedEvent, OnMoved);
        Tabs.RemoveHandler(PointerReleasedEvent, OnReleased);
        Tabs.RemoveHandler(Thumb.DragStartedEvent, OnScrollStarted);
        Tabs.RemoveHandler(Thumb.DragCompletedEvent, OnScrollCompleted);
        EndScrollDrag();
        Tabs.ContextRequested -= OnContextRequested;
        Tabs.SelectionChanged -= OnSelectionChanged;
        Tabs.Tapped -= OnTapped;
        Tabs.RemoveHandler(KeyDownEvent, OnTabKey);
        SizeChanged -= OnResized;
    }

    WorkspaceViewModel? Workspace => DataContext as WorkspaceViewModel;

    void OnResized(object? sender, SizeChangedEventArgs args) =>
        Tabs.MaxWidth = Math.Max(0, args.NewSize.Width - NewButtonSlack);

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (Tabs.SelectedItem is ScriptDocument document)
            Tabs.ScrollIntoView(document);
    }

    void OnPressed(object? sender, PointerPressedEventArgs args)
    {
        if (Container(args.Source as Visual) is not { } container || container.DataContext is not ScriptDocument document)
            return;
        PointerPointProperties point = args.GetCurrentPoint(this).Properties;
        if (point.IsMiddleButtonPressed)
        {
            Workspace?.CloseCommand.Execute(document);
            args.Handled = true;
            return;
        }
        if (!point.IsLeftButtonPressed)
            return;
        _dragging = document;
        _origin = args.GetPosition(Tabs);
        _moving = false;
    }

    void OnTapped(object? sender, TappedEventArgs args)
    {
        if (_moving || args.Source is not Visual source)
            return;
        for (Visual? visual = source; visual is not null && visual is not ListBoxItem; visual = visual.GetVisualParent())
            if (visual is Button)
                return;
        if (Container(source)?.DataContext is ScriptDocument document)
            Workspace?.ShowDocument(document);
    }

    void OnTabKey(object? sender, KeyEventArgs args)
    {
        if (args.Source is Button)
            return;
        if (args.Key == Key.Enter && Tabs.SelectedItem is ScriptDocument document)
        {
            Workspace?.ShowDocument(document);
            args.Handled = true;
        }
    }

    void OnMoved(object? sender, PointerEventArgs args)
    {
        if (_dragging is not { } document || Workspace is not { } workspace)
            return;
        Point now = args.GetPosition(Tabs);
        if (!_moving && Point.Distance(now, _origin) < DragThreshold)
            return;
        _moving = true;
        if (Container(args.Source as Visual) is not { } container || container.DataContext is not ScriptDocument over || ReferenceEquals(over, document))
            return;
        int from = workspace.Documents.IndexOf(document);
        int to = workspace.Documents.IndexOf(over);
        if (from < 0 || to < 0)
            return;
        workspace.MoveDocument(from, to);
        Tabs.SelectedItem = document;
    }

    void OnReleased(object? sender, PointerReleasedEventArgs args)
    {
        _dragging = null;
    }

    void OnScrollStarted(object? sender, VectorEventArgs args)
    {
        if (args.Source is not Thumb thumb || !thumb.GetVisualAncestors().OfType<ScrollBar>().Any())
            return;
        EndScrollDrag();
        _scrolling = thumb.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        _scrolling?.Classes.Add("scroll-drag");
    }

    void OnScrollCompleted(object? sender, VectorEventArgs args) => EndScrollDrag();

    void EndScrollDrag()
    {
        _scrolling?.Classes.Remove("scroll-drag");
        _scrolling = null;
    }

    void OnContextRequested(object? sender, ContextRequestedEventArgs args)
    {
        if (Workspace is not { } workspace)
            return;
        if (Container(args.Source as Visual) is not { } container || container.DataContext is not ScriptDocument document)
            return;
        workspace.Active = document;
        container.ContextMenu = BuildMenu(workspace, document);
        container.ContextMenu.Open(container);
        args.Handled = true;
    }

    static ContextMenu BuildMenu(WorkspaceViewModel workspace, ScriptDocument document)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Row("Close", workspace.CloseCommand, document));
        menu.Items.Add(Row("Close others", workspace.CloseOthersCommand, document));
        menu.Items.Add(Row("Close to the right", workspace.CloseToTheRightCommand, document));
        menu.Items.Add(new Separator());
        menu.Items.Add(Row("Duplicate", workspace.DuplicateCommand, document));
        menu.Items.Add(Row("Show in folder", workspace.RevealCommand, document));
        menu.Items.Add(new Separator());
        menu.Items.Add(Row("Rename", workspace.RenameCommand, document));
        return menu;
    }

    static MenuItem Row(string header, System.Windows.Input.ICommand command, ScriptDocument document) =>
        new() { Header = header, Command = command, CommandParameter = document };

    static ListBoxItem? Container(Visual? source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ListBoxItem container)
                return container;
        }
        return null;
    }
}
