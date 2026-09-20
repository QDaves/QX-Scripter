using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Qx.Desktop.Controls;
using Qx.Presentation.ViewModels.Library;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Views.Library;

public sealed partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        Scripts.ContainerPrepared += OnContainerPrepared;
        Scripts.ContainerClearing += OnContainerClearing;
        Scripts.ContextRequested += OnContextRequested;
        Scripts.DoubleTapped += OnRowOpened;
        Scripts.Tapped += OnRowTapped;
        Scripts.AddHandler(KeyDownEvent, OnListKey, RoutingStrategies.Tunnel);
        Search.AddHandler(KeyDownEvent, OnSearchKey, RoutingStrategies.Tunnel);
    }

    public void Release()
    {
        Scripts.ContainerPrepared -= OnContainerPrepared;
        Scripts.ContainerClearing -= OnContainerClearing;
        Scripts.ContextRequested -= OnContextRequested;
        Scripts.DoubleTapped -= OnRowOpened;
        Scripts.Tapped -= OnRowTapped;
        Scripts.RemoveHandler(KeyDownEvent, OnListKey);
        Search.RemoveHandler(KeyDownEvent, OnSearchKey);
    }

    LibraryViewModel? Page => DataContext as LibraryViewModel;

    void OnContainerPrepared(object? sender, ContainerPreparedEventArgs args) =>
        args.Container.Classes.Set("group", args.Container is ListBoxItem { Content: LibraryGroupRow });

    void OnContainerClearing(object? sender, ContainerClearingEventArgs args) =>
        args.Container.Classes.Set("group", false);

    void OnRowOpened(object? sender, TappedEventArgs args)
    {
        if (Page is not { } page || Row(args.Source as Visual) is not LibraryScriptRow row)
            return;
        page.OpenCommand.Execute(row);
        args.Handled = true;
    }

    void OnRowTapped(object? sender, TappedEventArgs args)
    {
        if (Page is not { } page || Row(args.Source as Visual) is not LibraryGroupRow group)
            return;
        page.ToggleGroupCommand.Execute(group);
        args.Handled = true;
    }

    void OnListKey(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || Page is not { } page || Scripts.SelectedItem is not LibraryGroupRow group)
            return;
        page.ToggleGroupCommand.Execute(group);
        args.Handled = true;
    }

    void OnSearchKey(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Down or Key.Enter) || Page is not { } page)
            return;
        if (page.SelectFirstResult() is not { } row)
            return;
        if (Scripts.ContainerFromItem(row) is { } container)
            container.Focus(NavigationMethod.Tab);
        else
            Scripts.Focus(NavigationMethod.Tab);
        args.Handled = true;
    }

    void OnContextRequested(object? sender, ContextRequestedEventArgs args)
    {
        if (Page is not { } page || Container(args.Source as Visual) is not { } container)
            return;
        Scripts.SelectedItem = container.Content;
        container.ContextMenu = container.Content switch
        {
            LibraryScriptRow row => ScriptMenu(page, row),
            LibraryGroupRow group => GroupMenu(page, group),
            _ => null
        };
        container.ContextMenu?.Open(container);
        args.Handled = container.ContextMenu is not null;
    }

    static ContextMenu ScriptMenu(LibraryViewModel page, LibraryScriptRow row)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Open", IconKind.OpenFile, page.OpenCommand, row));
        menu.Items.Add(Item("Rename…", IconKind.Rename, page.RenameCommand, row, new KeyGesture(Key.F2)));
        menu.Items.Add(Item("Category…", IconKind.Category, page.CategoryCommand, row));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Duplicate", IconKind.Duplicate, page.DuplicateCommand, row));
        menu.Items.Add(Item("Show in folder", IconKind.FolderOpen, page.RevealCommand, row));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Delete", IconKind.Delete, page.DeleteCommand, row, new KeyGesture(Key.Delete)));
        return menu;
    }

    static ContextMenu GroupMenu(LibraryViewModel page, LibraryGroupRow group)
    {
        var menu = new ContextMenu();
        if (!group.CanEdit)
        {
            menu.Items.Add(new MenuItem { Header = "No category to change", IsEnabled = false });
            return menu;
        }
        menu.Items.Add(Item("Rename category…", IconKind.Rename, page.RenameCategoryCommand, group));
        menu.Items.Add(Item("Clear category", IconKind.ClearAll, page.ClearCategoryCommand, group));
        return menu;
    }

    static MenuItem Item(string header, IconKind icon, System.Windows.Input.ICommand command, object parameter, KeyGesture? gesture = null) =>
        new()
        {
            Header = header,
            Icon = new Icon { Kind = icon, Size = 16 },
            Command = command,
            CommandParameter = parameter,
            InputGesture = gesture
        };

    static object? Row(Visual? source) => Container(source)?.Content;

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
