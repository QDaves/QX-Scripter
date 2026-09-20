using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Qx.Desktop.Editor;
using Qx.Presentation.Services.Files;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Desktop.Views.Editor;

public sealed partial class WorkspaceView : UserControl
{
    readonly RowDefinition _console_row;
    readonly ColumnDefinition _api_column;
    RoslynHostProvider? _hosts;
    WorkspaceViewModel? _workspace;
    OutputConsoleViewModel? _console;
    bool _applying_view;
    bool _applying_console;
    bool _applying_api;

    public WorkspaceView()
    {
        InitializeComponent();
        _console_row = Layout.RowDefinitions[3];
        _api_column = Body.ColumnDefinitions[2];
        ViewToggle.SelectionChanged += OnViewToggled;
        _console_row.PropertyChanged += OnConsoleRowChanged;
        _api_column.PropertyChanged += OnApiColumnChanged;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);
    }

    public void Use(RoslynHostProvider hosts)
    {
        _hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        Adopt();
    }

    public void Release()
    {
        ViewToggle.SelectionChanged -= OnViewToggled;
        _console_row.PropertyChanged -= OnConsoleRowChanged;
        _api_column.PropertyChanged -= OnApiColumnChanged;
        RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        RemoveHandler(DragDrop.DropEvent, OnDrop);
        if (_workspace is { } workspace)
            workspace.PropertyChanged -= OnWorkspaceChanged;
        if (_console is { } console)
            console.PropertyChanged -= OnConsoleChanged;
        OutputConsole.Release();
        _workspace = null;
        _console = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_workspace is { } previous)
            previous.PropertyChanged -= OnWorkspaceChanged;
        _workspace = DataContext as WorkspaceViewModel;
        if (_workspace is { } workspace)
            workspace.PropertyChanged += OnWorkspaceChanged;
        Adopt();
        BindConsole();
        ApplyView();
        ApplyConsole();
        ApplyApiBrowser();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Surface.Retire();
    }

    void Adopt()
    {
        if (_workspace is not { } workspace)
            return;
        OutputConsole.Use(workspace);
        if (_hosts is { } hosts)
            Surface.Use(hosts, workspace.Editor);
    }

    void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(WorkspaceViewModel.ActiveView):
            case nameof(WorkspaceViewModel.IsPanelView):
                ApplyView();
                ApplyConsole();
                break;
            case nameof(WorkspaceViewModel.Console):
                BindConsole();
                ApplyConsole();
                break;
            case nameof(WorkspaceViewModel.Active):
            case nameof(WorkspaceViewModel.HasDocuments):
                Surface.Retire();
                break;
            case nameof(WorkspaceViewModel.IsApiBrowserOpen):
                ApplyApiBrowser();
                break;
        }
    }

    void OnConsoleChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(OutputConsoleViewModel.IsCollapsed) or nameof(OutputConsoleViewModel.ExpandedHeight))
            ApplyConsole();
    }

    void OnConsoleRowChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != RowDefinition.HeightProperty || _applying_console || _console is not { IsCollapsed: false } console)
            return;
        if (_console_row.Height.IsAbsolute && _console_row.Height.Value > 0)
            console.ExpandedHeight = _console_row.Height.Value;
    }

    void OnApiColumnChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != ColumnDefinition.WidthProperty || _applying_api || _workspace is not { IsApiBrowserOpen: true } workspace)
            return;
        if (_api_column.Width.IsAbsolute && _api_column.Width.Value > 0)
            workspace.ApiBrowserWidth = _api_column.Width.Value;
    }

    void OnViewToggled(object? sender, SelectionChangedEventArgs args)
    {
        if (_applying_view || _workspace is not { } workspace)
            return;
        if (ViewToggle.SelectedIndex == 1)
            workspace.SelectPanelMode();
        else
            workspace.SelectCodeMode();
    }

    void OnDragOver(object? sender, DragEventArgs args)
    {
        args.DragEffects = Dropped(args).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        args.Handled = true;
    }

    void OnDrop(object? sender, DragEventArgs args)
    {
        IReadOnlyList<string> paths = Dropped(args);
        if (paths.Count > 0)
            _workspace?.OpenDroppedCommand.Execute(paths);
        args.Handled = true;
    }

    static IReadOnlyList<string> Dropped(DragEventArgs args)
    {
        if (args.DataTransfer.TryGetFiles() is not { } files)
            return [];
        var paths = new List<string>();
        foreach (IStorageItem file in files)
        {
            if (file.TryGetLocalPath() is { Length: > 0 } path && ScriptFileName.IsScript(path))
                paths.Add(path);
        }
        return paths;
    }

    void BindConsole()
    {
        if (_console is { } previous)
            previous.PropertyChanged -= OnConsoleChanged;
        _console = _workspace?.Console;
        if (_console is { } console)
            console.PropertyChanged += OnConsoleChanged;
    }

    void ApplyView()
    {
        _applying_view = true;
        try
        {
            ViewToggle.SelectedIndex = _workspace is { IsPanelView: true } ? 1 : 0;
        }
        finally
        {
            _applying_view = false;
        }
    }

    void ApplyConsole()
    {
        bool visible = _workspace?.ConsoleVisible == true;
        bool expanded = visible && _console is { IsCollapsed: false };
        OutputConsole.IsVisible = visible;
        ConsoleSplitter.IsVisible = expanded;
        _applying_console = true;
        try
        {
            double collapsed = Resource("QxConsoleCollapsedHeight");
            double minimum = Resource("QxConsoleMinHeight");
            double preferred = _console?.ExpandedHeight ?? Resource("QxConsoleDefaultHeight");
            _console_row.MinHeight = expanded ? minimum : 0;
            _console_row.Height = new GridLength(!visible ? 0 : expanded ? Math.Max(minimum, preferred) : collapsed);
        }
        finally
        {
            _applying_console = false;
        }
    }

    void ApplyApiBrowser()
    {
        bool open = _workspace?.IsApiBrowserOpen == true;
        _applying_api = true;
        try
        {
            double minimum = Resource("QxApiBrowserMinWidth");
            double maximum = Resource("QxApiBrowserMaxWidth");
            double preferred = _workspace?.ApiBrowserWidth ?? Resource("QxApiBrowserWidth");
            _api_column.MinWidth = open ? minimum : 0;
            _api_column.MaxWidth = open ? maximum : double.PositiveInfinity;
            _api_column.Width = new GridLength(open ? Math.Clamp(preferred, minimum, maximum) : 0);
        }
        finally
        {
            _applying_api = false;
        }
    }

    double Resource(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out object? own) && own is double mine)
            return mine;
        if (Application.Current is { } application && application.TryFindResource(key, ActualThemeVariant, out object? shared) && shared is double number)
            return number;
        throw new InvalidOperationException($"The layout token {key} is missing.");
    }
}
