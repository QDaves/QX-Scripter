using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Qx.Desktop.Controls;

public sealed class DataGridGutterColumn : DataGridColumn
{
    public DataGridGutterColumn()
    {
        MinWidth = 0;
        Width = new DataGridLength(12);
        CanUserResize = false;
        CanUserSort = false;
        CanUserReorder = false;
        IsReadOnly = true;
    }

    protected override Control GenerateElement(DataGridCell cell, object data_item) => new Panel();

    protected override Control GenerateEditingElement(DataGridCell cell, object data_item, out BindingExpressionBase? binding)
    {
        binding = null;
        return new Panel();
    }

    protected override object? PrepareCellForEdit(Control editing_element, RoutedEventArgs editing_event_args) => null;
}
