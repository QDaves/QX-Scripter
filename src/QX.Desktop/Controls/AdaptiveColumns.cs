using Avalonia;
using Avalonia.Controls;

namespace Qx.Desktop.Controls;

public sealed class AdaptiveColumns : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<AdaptiveColumns, double>(nameof(MinColumnWidth), 440);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<AdaptiveColumns, int>(nameof(MaxColumns), 2);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<AdaptiveColumns, double>(nameof(ColumnSpacing), 24);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<AdaptiveColumns, double>(nameof(RowSpacing), 24);

    static AdaptiveColumns()
    {
        AffectsMeasure<AdaptiveColumns>(MinColumnWidthProperty, MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size available_size)
    {
        List<Control> visible = Children.Where(child => child.IsVisible).ToList();
        int columns = ColumnsFor(available_size.Width, visible.Count);
        double column_width = double.IsInfinity(available_size.Width)
            ? double.PositiveInfinity
            : Math.Max(0, (available_size.Width - ColumnSpacing * (columns - 1)) / columns);
        double total_height = 0;
        double widest = 0;
        for (int row_start = 0; row_start < visible.Count; row_start += columns)
        {
            double row_height = 0;
            for (int index = row_start; index < Math.Min(row_start + columns, visible.Count); index++)
            {
                visible[index].Measure(new Size(column_width, double.PositiveInfinity));
                row_height = Math.Max(row_height, visible[index].DesiredSize.Height);
                widest = Math.Max(widest, visible[index].DesiredSize.Width);
            }
            total_height += row_height + (row_start > 0 ? RowSpacing : 0);
        }
        double width = double.IsInfinity(available_size.Width) ? widest * columns + ColumnSpacing * (columns - 1) : available_size.Width;
        return new Size(width, total_height);
    }

    protected override Size ArrangeOverride(Size final_size)
    {
        List<Control> visible = Children.Where(child => child.IsVisible).ToList();
        int columns = ColumnsFor(final_size.Width, visible.Count);
        double column_width = Math.Max(0, (final_size.Width - ColumnSpacing * (columns - 1)) / columns);
        double top = 0;
        for (int row_start = 0; row_start < visible.Count; row_start += columns)
        {
            double row_height = 0;
            for (int index = row_start; index < Math.Min(row_start + columns, visible.Count); index++)
                row_height = Math.Max(row_height, visible[index].DesiredSize.Height);
            for (int index = row_start; index < Math.Min(row_start + columns, visible.Count); index++)
            {
                double left = (index - row_start) * (column_width + ColumnSpacing);
                visible[index].Arrange(new Rect(left, top, column_width, visible[index].DesiredSize.Height));
            }
            top += row_height + RowSpacing;
        }
        return final_size;
    }

    int ColumnsFor(double width, int count)
    {
        int limit = Math.Max(1, Math.Min(count, MaxColumns));
        if (count == 0 || double.IsInfinity(width))
            return limit;
        int fit = (int)Math.Floor((width + ColumnSpacing) / (MinColumnWidth + ColumnSpacing));
        return Math.Clamp(fit, 1, limit);
    }
}
