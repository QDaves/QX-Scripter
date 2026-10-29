using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;
using Qx.Presentation.Services.Panels;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelTableCell : TextBlock
{
    readonly int _index;
    PanelTableRow? _row;

    public PanelTableCell(int index)
    {
        _index = index;
        TextTrimming = TextTrimming.CharacterEllipsis;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(12, 0);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (VisualRoot is not null)
            Watch(DataContext as PanelTableRow);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(DataContext as PanelTableRow);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    void Watch(PanelTableRow? row)
    {
        if (!ReferenceEquals(_row, row))
        {
            if (_row is not null)
                _row.PropertyChanged -= OnRowChanged;
            _row = row;
            if (row is not null)
                row.PropertyChanged += OnRowChanged;
        }
        Show();
    }

    void OnRowChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PanelTableRow.Cells))
            Show();
    }

    void Show() => Text = _row?.Cell(_index) ?? "";
}
