using System.ComponentModel;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.GameCatalog;

namespace Qx.Desktop.Views.GameCatalog;

public sealed partial class GameDataFurniView : UserControl
{
    FurniTabViewModel? _tab;

    public GameDataFurniView()
    {
        InitializeComponent();
        DataContextChanged += OnContextChanged;
    }

    void OnContextChanged(object? sender, EventArgs e)
    {
        if (_tab is not null)
            _tab.PropertyChanged -= OnTabChanged;
        _tab = DataContext as FurniTabViewModel;
        if (_tab is not null)
            _tab.PropertyChanged += OnTabChanged;
        RefreshColumns();
    }

    void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FurniTabViewModel.ShowKindColumn) or nameof(FurniTabViewModel.ShowPlaceColumn))
            RefreshColumns();
    }

    void RefreshColumns()
    {
        Show("Kind", _tab?.ShowKindColumn ?? false);
        Show("Placement", _tab?.ShowPlaceColumn ?? false);
    }

    void Show(string sort_member, bool visible)
    {
        foreach (DataGridColumn column in FurniGrid.Columns)
        {
            if (column.SortMemberPath == sort_member)
                column.IsVisible = visible;
        }
    }
}
