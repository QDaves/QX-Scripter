using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Qx.Presentation.Services.Panels;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelTableView : Decorator
{
    readonly DataGrid _grid;
    PanelTableNode? _node;
    bool _syncing;
    bool _scroll_pending;

    public PanelTableView()
    {
        _grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            CanUserResizeColumns = true,
            ClipboardCopyMode = DataGridClipboardCopyMode.None,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            RowHeight = Resource("QxRowHeightCompact") as double? ?? 28
        };
        Child = _grid;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Bind(DataContext as PanelTableNode);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Bind(null);
    }

    void Bind(PanelTableNode? node)
    {
        if (ReferenceEquals(_node, node))
            return;
        if (_node is { } previous)
        {
            _grid.SelectionChanged -= OnGridSelectionChanged;
            previous.PropertyChanged -= OnNodeChanged;
            ((INotifyCollectionChanged)previous.Rows).CollectionChanged -= OnRowsChanged;
        }
        _node = node;
        if (node is null)
        {
            _grid.ItemsSource = null;
            return;
        }
        BuildColumns(node);
        _grid.ItemsSource = node.Rows;
        _grid.SelectedItem = node.IsSelectable ? node.Selected : null;
        _grid.SelectionChanged += OnGridSelectionChanged;
        node.PropertyChanged += OnNodeChanged;
        ((INotifyCollectionChanged)node.Rows).CollectionChanged += OnRowsChanged;
    }

    void BuildColumns(PanelTableNode node)
    {
        _grid.Columns.Clear();
        double min = Resource("ScriptPanels.ColumnMinWidth") as double? ?? 72;
        for (int column = 0; column < node.Columns.Count; column++)
        {
            int index = column;
            _grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = node.Columns[index],
                CanUserSort = false,
                MinWidth = min,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellTemplate = new FuncDataTemplate<PanelTableRow>((row, _) => new TextBlock
                {
                    Text = row?.Cell(index) ?? "",
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0)
                })
            });
        }
    }

    void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_node is null)
            return;
        if (!_node.IsSelectable)
        {
            if (_grid.SelectedItem is not null)
                _grid.SelectedItem = null;
            return;
        }
        if (_syncing)
            return;
        _syncing = true;
        _node.Selected = _grid.SelectedItem as PanelTableRow;
        _syncing = false;
    }

    void OnNodeChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_syncing || _node is null || args.PropertyName != nameof(PanelTableNode.Selected))
            return;
        _syncing = true;
        _grid.SelectedItem = _node.IsSelectable ? _node.Selected : null;
        _syncing = false;
    }

    void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action != NotifyCollectionChangedAction.Add || _node is not { Rows.Count: > 0 } node || node.Selected is not null)
            return;
        Trail(node);
    }

    void Trail(PanelTableNode node)
    {
        if (_scroll_pending)
            return;
        _scroll_pending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scroll_pending = false;
            if (_node is { Rows.Count: > 0 } current && current.Selected is null)
                _grid.ScrollIntoView(current.Rows[^1], null);
        }, DispatcherPriority.Background);
    }

    object? Resource(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? value) ? value : null;
}
