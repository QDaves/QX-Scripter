using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia;
using Qx.Desktop.Controls;
using Qx.Presentation.ViewModels.Library;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Views.Library;

public sealed partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        Scripts.ContextRequested += context_requested;
        Categories.ContextRequested += category_requested;
        Scripts.DoubleTapped += row_opened;
        Search.AddHandler(KeyDownEvent, search_key, RoutingStrategies.Tunnel);
    }

    public void Release()
    {
        Scripts.ContextRequested -= context_requested;
        Categories.ContextRequested -= category_requested;
        Scripts.DoubleTapped -= row_opened;
        Search.RemoveHandler(KeyDownEvent, search_key);
    }

    LibraryViewModel? Page => DataContext as LibraryViewModel;

    void row_opened(object? sender, TappedEventArgs args)
    {
        if (args.Source is not Visual source || source.GetSelfAndVisualAncestors().OfType<Button>().Any())
            return;
        if (Page is not { } page || container_for(source)?.Content is not LibraryScriptRow row)
            return;
        page.OpenCommand.Execute(row);
        args.Handled = true;
    }

    void search_key(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Down or Key.Enter) || Page is not { } page)
            return;
        if (page.SelectFirstResult() is not { } row)
            return;
        Scripts.ScrollIntoView(row);
        if (Scripts.ContainerFromItem(row) is { } container)
            container.Focus(NavigationMethod.Tab);
        else
            Scripts.Focus(NavigationMethod.Tab);
        args.Handled = true;
    }

    void context_requested(object? sender, ContextRequestedEventArgs args)
    {
        if (Page is not { } page || container_for(args.Source as Visual) is not { Content: LibraryScriptRow row } container)
            return;
        Scripts.SelectedItem = row;
        container.ContextMenu = script_menu(page, row);
        container.ContextMenu.Open(container);
        args.Handled = true;
    }

    void open_menu(object? sender, RoutedEventArgs args)
    {
        if (Page is not { } page || sender is not Button { DataContext: LibraryScriptRow row } button)
            return;
        Scripts.SelectedItem = row;
        button.ContextMenu = script_menu(page, row);
        button.ContextMenu.Open(button);
        args.Handled = true;
    }

    void category_requested(object? sender, ContextRequestedEventArgs args)
    {
        if (Page is not { } page || container_for(args.Source as Visual) is not { Content: LibraryFilterOption { CanEdit: true } category } container)
            return;
        var menu = new ContextMenu();
        menu.Items.Add(menu_item("Rename category…", IconKind.Rename, page.RenameCategoryCommand, category));
        menu.Items.Add(menu_item("Clear category", IconKind.ClearAll, page.ClearCategoryCommand, category));
        container.ContextMenu = menu;
        menu.Open(container);
        args.Handled = true;
    }

    static ContextMenu script_menu(LibraryViewModel page, LibraryScriptRow row)
    {
        var menu = new ContextMenu();
        menu.Items.Add(menu_item("Open", IconKind.OpenFile, page.OpenCommand, row));
        menu.Items.Add(menu_item("Rename…", IconKind.Rename, page.RenameCommand, row, new KeyGesture(Key.F2)));
        menu.Items.Add(menu_item("Category…", IconKind.Category, page.CategoryCommand, row));
        menu.Items.Add(new Separator());
        menu.Items.Add(menu_item("Duplicate", IconKind.Duplicate, page.DuplicateCommand, row));
        menu.Items.Add(menu_item("Show in folder", IconKind.FolderOpen, page.RevealCommand, row));
        menu.Items.Add(new Separator());
        var delete = menu_item("Delete", IconKind.Delete, page.DeleteCommand, row, new KeyGesture(Key.Delete));
        delete.IsEnabled = row.CanDelete;
        menu.Items.Add(delete);
        return menu;
    }

    static MenuItem menu_item(string header, IconKind icon, System.Windows.Input.ICommand command, object parameter, KeyGesture? gesture = null) =>
        new()
        {
            Header = header,
            Icon = new Icon { Kind = icon, Size = 16 },
            Command = command,
            CommandParameter = parameter,
            InputGesture = gesture
        };

    static ListBoxItem? container_for(Visual? source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            if (visual is ListBoxItem container)
                return container;
        return null;
    }
}
