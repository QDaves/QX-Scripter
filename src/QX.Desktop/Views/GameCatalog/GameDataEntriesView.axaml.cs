using Avalonia.Automation;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.GameCatalog;

namespace Qx.Desktop.Views.GameCatalog;

public sealed partial class GameDataEntriesView : UserControl
{
    public GameDataEntriesView()
    {
        InitializeComponent();
        DataContextChanged += OnContextChanged;
    }

    void OnContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not KeyValueTabViewModel tab)
            return;
        Header("Key", tab.Labels.KeyHeader);
        Header("Value", tab.Labels.ValueHeader);
        AutomationProperties.SetName(EntriesGrid, tab.Labels.ListName);
    }

    void Header(string sort_member, string text)
    {
        foreach (DataGridColumn column in EntriesGrid.Columns)
        {
            if (column.SortMemberPath == sort_member)
                column.Header = text;
        }
    }
}
