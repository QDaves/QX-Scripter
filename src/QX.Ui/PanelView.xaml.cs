using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Qx.Scripting;

namespace Qx.Ui;

/// <summary>
/// Draws the panel a script declared and hands back what the user entered.
/// </summary>
/// <remarks>
/// The panel is a tree, not a list: a row lays its children out side by side and a group folds
/// them away, and both can hold the other. Everything a script can address later — a field, an
/// output box, a bar, a status line, a button — is registered by name while it is built, so the
/// writers below are dictionary lookups rather than another walk of the tree.
/// </remarks>
public partial class PanelView : UserControl
{
    private const double BlockGap = 24;
    private const double LineGap = 16;
    private const double ButtonHeight = 36;
    private const double DefaultOutputHeight = 160;
    private const double DefaultTableHeight = 220;
    private const double DefaultSpacerHeight = 12;
    private const double MinColumnWidth = 72;
    private const int VisibleToasts = 4;

    private readonly Dictionary<string, Func<string>> _getters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action<string>> _setters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> _outputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (ProgressBar Bar, TextBlock Percent)> _bars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TablePane> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameworkElement> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FrameworkElement, RowLayout> _rowSlots = [];
    private readonly Dictionary<Button, bool> _wanted = [];
    private readonly Dictionary<Button, (UIElement Face, ProgressBar Spinner)> _busyParts = [];
    private readonly List<DispatcherTimer> _timers = [];
    private readonly List<Button> _buttons = [];
    private readonly List<string> _outputOrder = [];
    private int _declaredButtons;
    private bool _busy;

    /// <summary>Raised when a button is pressed. The name is null for the stand-in Run button.</summary>
    public event Action<string?>? RunRequested;

    /// <summary>
    /// Raised when the user empties an output box from the box's own toolbar, with its name.
    /// </summary>
    /// <remarks>
    /// The panel holds the text but the host holds the snapshot it restores from and appends to, so
    /// a clear the host never hears about comes straight back on the next redraw or append.
    /// </remarks>
    public event Action<string>? OutputCleared;

    /// <summary>What a row child owns of its row's grid, so hiding it can take the space back.</summary>
    private sealed record RowSlot(FrameworkElement Element, int Column, int GapColumn, GridLength Width, GridLength Gap);

    private sealed class RowLayout(Grid grid)
    {
        public Grid Grid { get; } = grid;

        public List<RowSlot> Slots { get; } = [];
    }

    /// <summary>One row of a table.</summary>
    /// <remarks>
    /// The columns bind to the indexer rather than to <see cref="Cells"/> so a row shorter than the
    /// header draws an empty cell instead of binding past the end of its own list, which WPF answers
    /// with a swallowed failure per cell per redraw. Cells beyond the last column are still carried,
    /// because the script wrote them and both the clipboard and the selection hand them back.
    /// </remarks>
    private sealed class TableRow(IReadOnlyList<string> cells)
    {
        public IReadOnlyList<string> Cells { get; } = cells;

        public string this[int index] => index >= 0 && index < Cells.Count ? Cells[index] : "";
    }

    /// <summary>A table and everything a writer needs to reach it.</summary>
    private sealed class TablePane(
        ListView list,
        ObservableCollection<TableRow> rows,
        IReadOnlyList<string> columns,
        DispatcherTimer trail)
    {
        public ListView List { get; } = list;

        public ObservableCollection<TableRow> Rows { get; } = rows;

        public IReadOnlyList<string> Columns { get; } = columns;

        /// <summary>Waits for the grid to be left alone before scrolling to the newest row.</summary>
        public DispatcherTimer Trail { get; } = trail;
    }

    public PanelView() => InitializeComponent();

    /// <summary>The output boxes the panel declares, in the order they were written.</summary>
    public IReadOnlyList<string> OutputNames => _outputOrder;

    /// <summary>Whether the panel has anywhere to write to.</summary>
    public bool HasOutputs => _outputs.Count > 0;

    public void Build(UiSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        foreach (DispatcherTimer timer in _timers)
            timer.Stop();

        FormPanel.Children.Clear();
        ToastHost.Children.Clear();
        _getters.Clear();
        _setters.Clear();
        _outputs.Clear();
        _bars.Clear();
        _statuses.Clear();
        _tables.Clear();
        _named.Clear();
        _rowSlots.Clear();
        _wanted.Clear();
        _busyParts.Clear();
        _timers.Clear();
        _buttons.Clear();
        _outputOrder.Clear();
        _declaredButtons = 0;
        _busy = false;

        if (spec.Title.Length > 0)
        {
            TextBlock title = MakeText(spec.Title, 22, FontWeights.SemiBold, 1);
            title.TextWrapping = TextWrapping.Wrap;
            title.Margin = new Thickness(0, 0, 0, spec.Description.Length > 0 ? 5 : BlockGap);
            AutomationProperties.SetName(title, spec.Title);
            FormPanel.Children.Add(title);
        }

        if (spec.Description.Length > 0)
        {
            TextBlock description = MakeText(spec.Description, 12.5, FontWeights.Normal, 0.62);
            description.TextWrapping = TextWrapping.Wrap;
            description.LineHeight = 19;
            description.Margin = new Thickness(0, 0, 0, 26);
            FormPanel.Children.Add(description);
        }

        Render(spec.Nodes, FormPanel);
    }

    /// <summary>
    /// Draws a list of nodes into a stack, keeping neighbouring buttons on one line.
    /// </summary>
    /// <remarks>
    /// Buttons written one after another are a toolbar even when the script did not wrap them in a
    /// row, and stacking them would leave a column of full-width buttons.
    /// </remarks>
    private void Render(IReadOnlyList<UiNode> nodes, Panel target)
    {
        var run = new List<UiButtonNode>();

        void Flush()
        {
            if (run.Count == 0)
                return;
            target.Children.Add(MakeButtonStrip(run));
            run.Clear();
        }

        foreach (UiNode node in nodes)
        {
            if (node is UiButtonNode button)
            {
                run.Add(button);
                continue;
            }

            Flush();
            target.Children.Add(BuildNode(node, inRow: false));
        }

        Flush();
    }

    private FrameworkElement BuildNode(UiNode node, bool inRow)
    {
        FrameworkElement element = node switch
        {
            UiFieldNode field => BuildField(field.Field),
            UiOutputNode output => BuildOutput(output.Output),
            UiButtonNode button => BuildButton(button.Button),
            UiRowNode row => BuildRow(row),
            UiGroupNode group => BuildGroup(group),
            UiProgressNode bar => BuildProgress(bar),
            UiStatusNode status => BuildStatus(status),
            UiTableNode table => BuildTable(table),
            UiSectionNode section => MakeSection(section.Title),
            UiLabelNode label => BuildLabel(label.Text),
            UiSeparatorNode => MakeRule(new Thickness(0, 4, 0, 20)),
            UiSpacerNode spacer => new Border { Height = Extent(spacer.Height, DefaultSpacerHeight) },
            _ => new Border()
        };

        if (node.Attr.Text("tooltip") is { Length: > 0 } tip)
            element.ToolTip = tip;

        if (inRow)
        {
            // A nested row has already placed itself from its own align=, so stretching it here
            // would render `align=end` inside a row left-packed. Everything else only has to fill
            // the column that carries its width.
            if (node is not UiRowNode)
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
            if (element is Button)
                element.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else if (Extent(node.Width, 0) is > 0 and var width)
        {
            element.Width = width;
            element.HorizontalAlignment = HorizontalAlignment.Left;
        }

        return element;
    }

    private FrameworkElement MakeButtonStrip(IReadOnlyList<UiButtonNode> nodes)
    {
        var strip = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, LineGap)
        };

        foreach (UiButtonNode node in nodes)
        {
            FrameworkElement button = BuildNode(node, inRow: false);
            button.Margin = new Thickness(0, 0, 8, 8);
            button.VerticalAlignment = VerticalAlignment.Center;
            strip.Children.Add(button);
        }

        return strip;
    }

    /// <summary>
    /// Lays a row out as a grid: fixed widths become pixel columns, <c>grow</c> becomes a share of
    /// the star space, and anything else keeps the width it asks for.
    /// </summary>
    private FrameworkElement BuildRow(UiRowNode row)
    {
        var grid = new Grid();
        var layout = new RowLayout(grid);
        bool shares = false;
        double gap = Extent(row.Gap, 0);

        for (int i = 0; i < row.Children.Count; i++)
        {
            UiNode child = row.Children[i];

            var gapWidth = new GridLength(gap);
            int gapColumn = -1;
            if (i > 0 && gap > 0)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = gapWidth });
                gapColumn = grid.ColumnDefinitions.Count - 1;
            }

            double fixedWidth = Extent(child.Width, 0);
            double grow = Extent(child.Grow, 0);

            GridLength width;
            if (fixedWidth > 0)
            {
                width = new GridLength(fixedWidth);
            }
            else if (grow > 0)
            {
                width = new GridLength(grow, GridUnitType.Star);
                shares = true;
            }
            else if (row.Align == UiRowAlign.Stretch)
            {
                width = new GridLength(1, GridUnitType.Star);
                shares = true;
            }
            else
            {
                width = GridLength.Auto;
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            int column = grid.ColumnDefinitions.Count - 1;

            FrameworkElement element = BuildNode(child, inRow: true);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);

            layout.Slots.Add(new RowSlot(element, column, gapColumn, width, gapWidth));
            _rowSlots[element] = layout;
        }

        // Star columns only mean anything when the row is allowed to fill the form, so a row that
        // shares its width stretches whatever its alignment says.
        grid.HorizontalAlignment = shares
            ? HorizontalAlignment.Stretch
            : row.Align switch
            {
                UiRowAlign.Center => HorizontalAlignment.Center,
                UiRowAlign.End => HorizontalAlignment.Right,
                UiRowAlign.Stretch => HorizontalAlignment.Stretch,
                _ => HorizontalAlignment.Left
            };

        return grid;
    }

    /// <summary>
    /// Re-sizes a row's columns from what is currently visible in it.
    /// </summary>
    /// <remarks>
    /// Collapsing the child is not enough: its column keeps its pixel width or its share of the star
    /// space and the gap beside it stays, so a hidden control leaves a hole where the panel promised
    /// it would take no space. A gap only earns its width when the child after it is visible and
    /// something visible comes before it, which is what keeps a hidden first child from leaving the
    /// row indented.
    /// </remarks>
    private static void Relayout(RowLayout layout)
    {
        bool anyBefore = false;

        foreach (RowSlot slot in layout.Slots)
        {
            bool shown = slot.Element.Visibility == Visibility.Visible;
            layout.Grid.ColumnDefinitions[slot.Column].Width = shown ? slot.Width : new GridLength(0);

            if (slot.GapColumn >= 0)
                layout.Grid.ColumnDefinitions[slot.GapColumn].Width =
                    shown && anyBefore ? slot.Gap : new GridLength(0);

            anyBefore |= shown;
        }
    }

    /// <summary>A length WPF will accept, or the renderer's own when the directive asked for junk.</summary>
    /// <remarks>
    /// <see cref="FrameworkElement.Height"/>, <see cref="FrameworkElement.Width"/> and
    /// <see cref="GridLength"/> all throw <see cref="ArgumentException"/> on a negative or infinite
    /// value, and a throw from a builder escapes <see cref="Build"/> unhandled and ends the process.
    /// A panel is user-written text, so <c>height=-1</c> has to draw something instead.
    /// </remarks>
    private static double Extent(double? asked, double fallback) =>
        asked is { } value && double.IsFinite(value) && value > 0 ? value : fallback;

    /// <summary>A slider bound WPF will accept. Zero and negatives are a legitimate range.</summary>
    /// <remarks>
    /// <see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/> and
    /// <see cref="RangeBase.Value"/> reject NaN and infinity, and <c>min=NaN</c> parses, so the
    /// range has to be finite before it is handed over.
    /// </remarks>
    private static double Bound(double? asked, double fallback) =>
        asked is { } value && double.IsFinite(value) ? value : fallback;

    private FrameworkElement BuildGroup(UiGroupNode group)
    {
        var body = new StackPanel { Margin = new Thickness(14, 2, 14, 0) };
        Render(group.Children, body);

        // The last child inside a group already carries its own bottom margin, so the box only
        // needs enough padding underneath to stop that margin reading as a missing edge.
        var content = new Border
        {
            Child = body,
            Padding = new Thickness(0, 0, 0, 2),
            Visibility = group.Collapsed ? Visibility.Collapsed : Visibility.Visible
        };

        var header = new ToggleButton { IsChecked = !group.Collapsed };
        header.SetResourceReference(StyleProperty, "PanelGroupHeader");
        TextBlock title = MakeText(group.Title, 12.5, FontWeights.Medium, 0.86);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        header.Content = title;
        header.Checked += (_, _) => content.Visibility = Visibility.Visible;
        header.Unchecked += (_, _) => content.Visibility = Visibility.Collapsed;
        MakeAccessible(header, group.Title, "Folds this group away");

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(content);

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(0, 0, 0, BlockGap),
            Padding = new Thickness(4, 4, 4, 4),
            Child = stack
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Divider");
        return frame;
    }

    private static FrameworkElement BuildLabel(string text)
    {
        TextBlock block = MakeText(text, 12.5, FontWeights.Normal, 0.72);
        block.TextWrapping = TextWrapping.Wrap;
        block.LineHeight = 19;
        block.Margin = new Thickness(2, 0, 0, LineGap);
        return block;
    }

    private FrameworkElement BuildProgress(UiProgressNode node)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, LineGap) };

        var header = new DockPanel { Margin = new Thickness(2, 0, 0, 7) };
        TextBlock percent = MakeText("0%", 12, FontWeights.SemiBold, 0.9);
        percent.HorizontalAlignment = HorizontalAlignment.Right;
        percent.SetResourceReference(TextBlock.ForegroundProperty, "QxAccentBrush");
        DockPanel.SetDock(percent, Dock.Right);
        header.Children.Add(percent);
        header.Children.Add(MakeText(node.Label, 12.5, FontWeights.Medium, 0.78));
        container.Children.Add(header);

        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 5 };
        MakeAccessible(bar, node.Label, "Script progress");
        AutomationProperties.SetLiveSetting(bar, AutomationLiveSetting.Polite);
        container.Children.Add(bar);

        _bars[node.Name] = (bar, percent);
        _named[node.Name] = container;
        return container;
    }

    private FrameworkElement BuildStatus(UiStatusNode node)
    {
        var line = new DockPanel { Margin = new Thickness(2, 0, 0, LineGap) };

        TextBlock label = MakeText(node.Label, 12.5, FontWeights.Medium, 0.78);
        label.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(label, Dock.Left);
        line.Children.Add(label);

        var value = new TextBlock
        {
            // The quoted text on a status directive is its caption, so a starting value is written
            // as a default. Without this the line sat blank next to its label until the script got
            // round to writing, which read as something not working.
            Text = node.Initial,
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "QxTextSecondaryBrush");
        MakeAccessible(value, node.Label, "Script status");
        AutomationProperties.SetLiveSetting(value, AutomationLiveSetting.Polite);
        line.Children.Add(value);

        _statuses[node.Name] = value;
        _named[node.Name] = line;
        return line;
    }

    /// <summary>
    /// Draws a table: a header with its own actions over a grid that only realises what is on
    /// screen.
    /// </summary>
    /// <remarks>
    /// The rows are an observable list behind a virtualising panel, so appending one costs a single
    /// notification and a row that has scrolled away costs nothing at all. That is what lets a
    /// script write thousands of them into a panel that stays responsive.
    /// </remarks>
    private FrameworkElement BuildTable(UiTableNode node)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, BlockGap) };

        // A table declared without a column list still has to put its rows somewhere.
        IReadOnlyList<string> columns = node.Columns.Count > 0 ? node.Columns : ["Value"];

        // The heading is a template over the column's text rather than a control put there directly:
        // a header presenter builds its own copy, and an element handed to more than one of them is
        // an element with two parents, which WPF answers with an exception out of the builder.
        DataTemplate heading = MakeHeading();

        var view = new GridView { AllowsColumnReorder = false };
        for (int i = 0; i < columns.Count; i++)
        {
            view.Columns.Add(new GridViewColumn
            {
                Header = columns[i],
                HeaderTemplate = heading,
                DisplayMemberBinding = new Binding(FormattableString.Invariant($"[{i}]"))
            });
        }

        var rows = new ObservableCollection<TableRow>();
        var list = new ListView
        {
            View = view,
            ItemsSource = rows,
            SelectionMode = SelectionMode.Single,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            FontSize = 12
        };

        // Rows are data, and data in columns only lines up in the code face. The headings above them
        // are prose, so they keep the interface face they were built with.
        list.SetResourceReference(FontFamilyProperty, "FontCode");
        ScrollViewer.SetCanContentScroll(list, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        MakeAccessible(list, node.Label, "Rows the script wrote");
        AutomationProperties.SetLiveSetting(list, AutomationLiveSetting.Polite);
        SpreadColumns(list, view);

        if (node.Selectable)
        {
            _getters[node.Name] = () =>
                list.SelectedItem is TableRow row ? string.Join('\t', row.Cells) : "";
        }
        else
        {
            // A row that lights up when it is clicked promises the script is watching. Where nothing
            // reads the selection back, it says nothing.
            list.SelectionChanged += (_, _) =>
            {
                if (list.SelectedIndex >= 0)
                    list.SelectedIndex = -1;
            };
        }

        var header = new DockPanel { Margin = new Thickness(2, 0, 0, 7) };
        if (node.Toolbar)
        {
            // Emptied from its own toolbar this is the user's doing, not the host's, and the host
            // keeps the snapshot it would otherwise write straight back — the same bargain an output
            // box makes.
            header.Children.Add(MakeToolbar(
                node.Label,
                () =>
                {
                    ClearTable(node.Name);
                    OutputCleared?.Invoke(node.Name);
                },
                () => Tabulate(columns, rows)));
        }
        TextBlock label = MakeText(node.Label, 12.5, FontWeights.Medium, 0.78);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        header.Children.Add(label);
        container.Children.Add(header);

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Height = Extent(node.Height, DefaultTableHeight),
            Padding = new Thickness(1),
            Child = list
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Divider");
        frame.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        container.Children.Add(frame);

        // Following the newest row is what makes a table readable while it fills, but scrolling to
        // it forces a layout pass, and a script adding rows in a loop would pay for one per row.
        // Waiting for the dispatcher to go quiet spends a single pass on however many arrived in
        // between. A selected row means someone is reading rather than watching, and the table holds
        // still for them.
        var trail = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.Zero };
        trail.Tick += (_, _) =>
        {
            trail.Stop();
            if (rows.Count > 0 && list.SelectedIndex < 0)
                list.ScrollIntoView(rows[^1]);
        };
        _timers.Add(trail);

        _tables[node.Name] = new TablePane(list, rows, columns, trail);
        _named[node.Name] = container;
        return container;
    }

    /// <summary>
    /// How a column heading is drawn: the caption face over rows written in the code face.
    /// </summary>
    private static DataTemplate MakeHeading()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding());
        text.SetResourceReference(FontFamilyProperty, "FontUI");
        text.SetValue(TextBlock.FontSizeProperty, 11.5);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        text.SetValue(OpacityProperty, 0.82);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

        var template = new DataTemplate { VisualTree = text };
        template.Seal();
        return template;
    }

    /// <summary>
    /// Keeps a table's columns sharing its width, until the user says otherwise.
    /// </summary>
    /// <remarks>
    /// A <see cref="GridView"/> has no star widths: a column either has a number or measures itself
    /// against the rows that happen to be realised, which with virtualisation means the columns
    /// resize as the grid is scrolled. Sharing the width is steady instead, and a column the user has
    /// dragged is theirs from that point on — that is what the remembered widths are compared
    /// against.
    /// </remarks>
    private static void SpreadColumns(ListView list, GridView view)
    {
        double[] given = new double[view.Columns.Count];
        bool dragged = false;

        list.SizeChanged += (_, _) =>
        {
            if (dragged || view.Columns.Count == 0)
                return;

            for (int i = 0; i < view.Columns.Count; i++)
            {
                if (given[i] > 0 && Math.Abs(view.Columns[i].Width - given[i]) > 0.5)
                {
                    dragged = true;
                    return;
                }
            }

            double room = list.ActualWidth - SystemParameters.VerticalScrollBarWidth - 6;
            double each = Math.Max(MinColumnWidth, room / view.Columns.Count);
            if (!double.IsFinite(each))
                return;

            for (int i = 0; i < view.Columns.Count; i++)
                view.Columns[i].Width = given[i] = each;
        };
    }

    /// <summary>A table as tab-separated text with its headings first, ready for a spreadsheet.</summary>
    /// <remarks>
    /// Every cell a row carries is written, including any past the last column: the script put them
    /// there, and a copy that quietly drops half a row is worse than one that pastes wider than its
    /// heading.
    /// </remarks>
    private static string Tabulate(IReadOnlyList<string> columns, IReadOnlyList<TableRow> rows)
    {
        var text = new StringBuilder();
        text.AppendJoin('\t', columns).Append('\n');
        foreach (TableRow row in rows)
            text.AppendJoin('\t', row.Cells).Append('\n');
        return text.ToString();
    }

    /// <summary>A cell that cannot break the row it sits in.</summary>
    /// <remarks>
    /// A tab or a newline inside a cell would split the row on its way to the clipboard and stretch
    /// it on screen, so a cell is one line by the time it is stored. The grid, the copy and the
    /// selection then all say the same thing.
    /// </remarks>
    private static string Flatten(string? cell)
    {
        if (string.IsNullOrEmpty(cell))
            return "";
        return cell.IndexOfAny(['\t', '\r', '\n']) < 0
            ? cell
            : cell.Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
    }

    private static void UseStyle(FrameworkElement element, string key) =>
        element.SetResourceReference(StyleProperty, key);

    private static TextBlock MakeText(string text, double size, FontWeight weight, double opacity)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Opacity = opacity
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "MaterialDesign.Brush.Foreground");
        return block;
    }

    private static Border MakeRule(Thickness margin)
    {
        var rule = new Border
        {
            Height = 1,
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center
        };
        rule.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Divider");
        return rule;
    }

    private static FrameworkElement MakeSection(string section)
    {
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        TextBlock label = MakeText(section.ToUpperInvariant(), 10.5, FontWeights.SemiBold, 0.76);
        label.SetResourceReference(TextBlock.ForegroundProperty, "QxAccentBrush");
        AutomationProperties.SetName(label, section);
        grid.Children.Add(label);

        Border line = MakeRule(new Thickness(12, 0, 0, 0));
        Grid.SetColumn(line, 1);
        grid.Children.Add(line);
        return grid;
    }

    private static TextBlock MakeFieldLabel(string label)
    {
        TextBlock block = MakeText(label, 12.5, FontWeights.Medium, 0.78);
        block.Margin = new Thickness(2, 0, 0, 7);
        return block;
    }

    private static TextBlock MakeHelper(string text)
    {
        TextBlock block = MakeText(text, 11, FontWeights.Normal, 0.52);
        block.TextWrapping = TextWrapping.Wrap;
        block.Margin = new Thickness(3, 6, 0, 0);
        return block;
    }

    private static void MakeAccessible(FrameworkElement element, string name, string? helpText = null)
    {
        AutomationProperties.SetName(element, name);
        if (!string.IsNullOrWhiteSpace(helpText))
            AutomationProperties.SetHelpText(element, helpText);
    }

    /// <summary>
    /// Draws one input with its label above it, and registers the reader and writer for its name.
    /// </summary>
    /// <remarks>
    /// Every labelled kind draws its label whether or not a <c>placeholder</c> was written — a
    /// checkbox carries its own and a slider keeps it in the header, and those are the only
    /// exceptions. Drawing it only for the fields that had a placeholder made <c>placeholder</c> a
    /// layout attribute: two fields side by side in a row started at different heights depending on
    /// which of them happened to have one.
    /// </remarks>
    private FrameworkElement BuildField(UiField field)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, BlockGap) };
        string? placeholder = field.Attr.Text("placeholder");
        string? help = field.Attr.Text("help");

        switch (field.Kind)
        {
            case UiFieldKind.Bool:
            {
                var check = new CheckBox
                {
                    Content = field.Label,
                    IsChecked = field.Default is "true" or "True" or "1"
                };
                MakeAccessible(check, field.Label, help ?? $"Boolean field {field.Name}");
                _getters[field.Name] = () => check.IsChecked == true ? "true" : "false";
                _setters[field.Name] = value =>
                    check.IsChecked = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                container.Children.Add(check);
                break;
            }
            case UiFieldKind.Slider:
            {
                double min = Bound(field.Min, 0);
                double max = Bound(field.Max, 100);
                if (max < min)
                    (min, max) = (max, min);
                double value = double.TryParse(field.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    ? Bound(parsed, min)
                    : min;
                bool whole = field.Default.IndexOf('.') < 0 && (field.Min is null || min == Math.Floor(min));

                var header = new DockPanel { Margin = new Thickness(2, 0, 0, 7) };
                TextBlock valueText = MakeText("", 12.5, FontWeights.SemiBold, 0.9);
                valueText.HorizontalAlignment = HorizontalAlignment.Right;
                valueText.SetResourceReference(TextBlock.ForegroundProperty, "QxAccentBrush");
                DockPanel.SetDock(valueText, Dock.Right);
                header.Children.Add(valueText);
                header.Children.Add(MakeText(field.Label, 12.5, FontWeights.Medium, 0.78));
                container.Children.Add(header);

                var slider = new Slider
                {
                    Minimum = min,
                    Maximum = max,
                    Value = Math.Clamp(value, min, max),
                    IsSnapToTickEnabled = whole,
                    TickFrequency = whole ? 1 : 0.01
                };
                MakeAccessible(slider, field.Label, help ?? $"Range {min:0.##} to {max:0.##}");
                string Show(double v) => whole
                    ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                    : v.ToString("0.##", CultureInfo.InvariantCulture);
                slider.ValueChanged += (_, _) => valueText.Text = Show(slider.Value);
                valueText.Text = Show(slider.Value);

                // A whole slider has to read back as a whole number: Ui.Int parses with int.TryParse,
                // and "50.0" — or "50,0" on a comma machine — would silently fall back to the default.
                _getters[field.Name] = () => whole
                    ? ((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture)
                    : slider.Value.ToString(CultureInfo.InvariantCulture);
                // Ui.Set hands over whatever the script wrote, "NaN" parses, and Slider.Value throws
                // on it — from a script that would take the window down with it.
                _setters[field.Name] = text =>
                {
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double restored) &&
                        double.IsFinite(restored))
                        slider.Value = Math.Clamp(restored, slider.Minimum, slider.Maximum);
                };
                container.Children.Add(slider);
                break;
            }
            case UiFieldKind.Color:
            {
                container.Children.Add(MakeFieldLabel(field.Label));
                var dock = new DockPanel();
                var swatch = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderThickness = new Thickness(1)
                };
                swatch.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Divider");
                var box = new TextBox
                {
                    Text = field.Default.Length > 0 ? field.Default : "#6E8BFF",
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                UseStyle(box, "MaterialDesignOutlinedTextBox");
                HintAssist.SetHint(box, placeholder is { Length: > 0 } ? placeholder : "#RRGGBB");
                MakeAccessible(box, field.Label, help ?? "Enter a hexadecimal color value");
                void Paint()
                {
                    try
                    {
                        swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(box.Text)!);
                    }
                    catch (FormatException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
                box.TextChanged += (_, _) => Paint();
                Paint();
                DockPanel.SetDock(swatch, Dock.Left);
                dock.Children.Add(swatch);
                dock.Children.Add(box);
                _getters[field.Name] = () => box.Text;
                _setters[field.Name] = value => box.Text = value;
                container.Children.Add(dock);
                break;
            }
            case UiFieldKind.Select:
            {
                container.Children.Add(MakeFieldLabel(field.Label));
                var combo = new ComboBox
                {
                    ItemsSource = field.Options,
                    SelectedItem = field.Options.FirstOrDefault(o => o == field.Default) ?? field.Options.FirstOrDefault()
                };
                UseStyle(combo, "MaterialDesignOutlinedComboBox");
                ComboBoxPopupBackground.Apply(combo);
                if (placeholder is { Length: > 0 })
                    HintAssist.SetHint(combo, placeholder);
                MakeAccessible(combo, field.Label, help ?? $"Select a value for {field.Name}");
                _getters[field.Name] = () => combo.SelectedItem?.ToString() ?? "";
                _setters[field.Name] = value =>
                {
                    string? option = field.Options.FirstOrDefault(candidate =>
                        candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (option is not null)
                        combo.SelectedItem = option;
                };
                container.Children.Add(combo);
                break;
            }
            case UiFieldKind.File:
            {
                container.Children.Add(MakeFieldLabel(field.Label));
                var dock = new DockPanel();
                var pathBox = new TextBox { IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center };
                UseStyle(pathBox, "MaterialDesignOutlinedTextBox");
                HintAssist.SetHint(pathBox, placeholder is { Length: > 0 } ? placeholder : "No file selected");
                var browse = new Button { Content = "Browse", Height = 34, Margin = new Thickness(8, 0, 0, 0) };
                UseStyle(browse, "MaterialDesignOutlinedButton");
                MakeAccessible(pathBox, field.Label, help ?? "Selected file path");
                MakeAccessible(browse, $"Browse for {field.Label}", "Opens a file picker");
                browse.Click += (_, _) =>
                {
                    var dialog = new OpenFileDialog();
                    if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                        pathBox.Text = dialog.FileName;
                };
                DockPanel.SetDock(browse, Dock.Right);
                dock.Children.Add(browse);
                dock.Children.Add(pathBox);
                _getters[field.Name] = () => pathBox.Text;
                _setters[field.Name] = value => pathBox.Text = value;
                container.Children.Add(dock);
                break;
            }
            case UiFieldKind.Text:
            {
                container.Children.Add(MakeFieldLabel(field.Label));
                var text = new TextBox
                {
                    Text = field.Default,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 78,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalContentAlignment = VerticalAlignment.Top
                };
                UseStyle(text, "MaterialDesignOutlinedTextBox");
                if (placeholder is { Length: > 0 })
                    HintAssist.SetHint(text, placeholder);
                MakeAccessible(text, field.Label, help ?? $"Multiline text field {field.Name}");
                _getters[field.Name] = () => text.Text;
                _setters[field.Name] = value => text.Text = value;
                container.Children.Add(text);
                break;
            }
            default:
            {
                container.Children.Add(MakeFieldLabel(field.Label));
                var box = new TextBox { Text = field.Default };
                UseStyle(box, "MaterialDesignOutlinedTextBox");
                if (placeholder is { Length: > 0 })
                    HintAssist.SetHint(box, placeholder);
                MakeAccessible(box, field.Label, help ?? $"{field.Kind} field {field.Name}");
                container.Children.Add(box);

                if (help is null && field.Kind is UiFieldKind.Int or UiFieldKind.Number &&
                    field.Min is { } low && field.Max is { } high)
                    container.Children.Add(MakeHelper($"{low:0.##} – {high:0.##}"));

                _getters[field.Name] = field.Kind switch
                {
                    UiFieldKind.Int => () => Whole(box.Text),
                    UiFieldKind.Number => () => Fractional(box.Text),
                    _ => () => box.Text
                };
                _setters[field.Name] = value => box.Text = value;
                break;
            }
        }

        if (help is { Length: > 0 })
            container.Children.Add(MakeHelper(help));

        _named[field.Name] = container;
        return container;
    }

    /// <summary>
    /// Rewrites a typed number the way the scripting side reads it back.
    /// </summary>
    /// <remarks>
    /// <see cref="ScriptUi.Number"/> parses invariantly, so <c>1,5</c> typed on a comma-decimal
    /// machine has to be normalised here rather than silently falling back to a default. Group
    /// separators are not accepted and the invariant reading is tried first, because both would turn
    /// <c>1.5</c> into fifteen on a de-DE machine: the box would still read <c>1.5</c> while the
    /// script was handed <c>15</c>, which is the worst of the two failures by far. Text that is not
    /// a number at all is handed over untouched, so the script sees what was written instead of a
    /// zero it cannot explain.
    /// </remarks>
    private static string Fractional(string text)
    {
        if (text.Trim().Length == 0)
            return "";
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return value.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    /// <inheritdoc cref="Fractional"/>
    private static string Whole(string text)
    {
        if (text.Trim().Length == 0)
            return "";
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ||
            long.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            return value.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    private FrameworkElement BuildOutput(UiOutput output)
    {
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, BlockGap) };

        var box = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 12,
            Padding = new Thickness(11, 9, 11, 9),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = output.Wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
            TextWrapping = output.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        box.SetResourceReference(FontFamilyProperty, output.Monospace ? "FontCode" : "FontUI");
        box.SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Foreground");
        TextFieldAssist.SetDecorationVisibility(box, Visibility.Collapsed);
        MakeAccessible(box, output.Label, "Read-only script output");
        AutomationProperties.SetLiveSetting(box, AutomationLiveSetting.Polite);

        var header = new DockPanel { Margin = new Thickness(2, 0, 0, 7) };
        if (output.Toolbar)
        {
            // Emptying the box alone lasts only until the next redraw or truncating append, both of
            // which write the host's snapshot back over it, so the host has to be told it was
            // emptied.
            header.Children.Add(MakeToolbar(
                output.Label,
                () =>
                {
                    box.Clear();
                    OutputCleared?.Invoke(output.Name);
                },
                () => box.Text));
        }
        TextBlock label = MakeText(output.Label, 12.5, FontWeights.Medium, 0.78);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        header.Children.Add(label);
        container.Children.Add(header);

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Height = Extent(output.Height, DefaultOutputHeight),
            Child = box
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "MaterialDesign.Brush.Divider");
        frame.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        container.Children.Add(frame);

        _outputs[output.Name] = box;
        _outputOrder.Add(output.Name);
        _named[output.Name] = container;
        return container;
    }

    /// <summary>The clear and copy pair a box or a table carries in its header.</summary>
    /// <param name="label">What the pair belongs to, for the screen reader.</param>
    /// <param name="clear">Empties it.</param>
    /// <param name="take">What copying puts on the clipboard.</param>
    private FrameworkElement MakeToolbar(string label, Action clear, Func<string> take)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(actions, Dock.Right);
        actions.Children.Add(MakeClearButton(label, clear));
        actions.Children.Add(MakeCopyButton(label, take));
        return actions;
    }

    private static Button MakeClearButton(string label, Action clear)
    {
        var button = new Button
        {
            Content = new PackIcon { Kind = PackIconKind.DeleteSweepOutline, Width = 16, Height = 16 },
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = "Clear"
        };
        UseStyle(button, "OutputActionButton");
        MakeAccessible(button, $"Clear {label}");
        button.Click += (_, _) => clear();
        return button;
    }

    private Button MakeCopyButton(string label, Func<string> take)
    {
        var icon = new PackIcon { Kind = PackIconKind.ContentCopy, Width = 15, Height = 15 };
        var copy = new Button { Content = icon, ToolTip = "Copy" };
        UseStyle(copy, "OutputActionButton");
        MakeAccessible(copy, $"Copy {label}");

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            icon.Kind = PackIconKind.ContentCopy;
            icon.ClearValue(ForegroundProperty);
            copy.ToolTip = "Copy";
        };
        _timers.Add(timer);

        copy.Click += (_, _) =>
        {
            string text = take();
            if (text.Length == 0)
                return;
            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return;
            }

            // A copy that says nothing looks like a copy that failed.
            timer.Stop();
            icon.Kind = PackIconKind.Check;
            icon.SetResourceReference(ForegroundProperty, "QxSuccessBrush");
            copy.ToolTip = "Copied";
            timer.Start();
        };

        return copy;
    }

    private Button BuildButton(UiButton spec)
    {
        // Without a style of its own the first button is the one the panel is for, and the rest
        // are the alternatives to it.
        UiButtonStyle style = spec.Style ?? (_declaredButtons == 0 ? UiButtonStyle.Primary : UiButtonStyle.Normal);
        _declaredButtons++;
        Button button = MakeButton(spec.Label, spec.Name, style);
        _named[spec.Name] = button;
        return button;
    }

    private Button MakeButton(string label, string? name, UiButtonStyle style)
    {
        var button = new Button
        {
            Content = MakeButtonFace(label, out UIElement face, out ProgressBar spinner),
            Height = ButtonHeight,
            MinWidth = 96,
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(0, 0, 0, BlockGap),
            VerticalAlignment = VerticalAlignment.Bottom,
            IsEnabled = !_busy
        };
        _busyParts[button] = (face, spinner);

        UseStyle(button, style switch
        {
            UiButtonStyle.Primary => "MaterialDesignRaisedButton",
            UiButtonStyle.Quiet => "MaterialDesignFlatButton",
            _ => "MaterialDesignOutlinedButton"
        });

        if (style == UiButtonStyle.Danger)
        {
            button.SetResourceReference(ForegroundProperty, "QxDangerBrush");
            button.SetResourceReference(BorderBrushProperty, "QxDangerBrush");
        }

        MakeAccessible(button, label, "Runs this panel action");
        button.Click += (_, _) => RunRequested?.Invoke(name);
        _buttons.Add(button);
        _wanted[button] = true;
        return button;
    }

    /// <summary>
    /// A button's caption with a spinner waiting behind it in the same place.
    /// </summary>
    /// <remarks>
    /// The two share one cell and the caption is hidden rather than collapsed while the spinner
    /// shows, so the button keeps the width its longest state asks for and the row it sits in does
    /// not shuffle every time work starts or stops. The spinner takes its colour from the button so
    /// it is right on a filled one and on an outlined one without either being named here.
    /// </remarks>
    private static FrameworkElement MakeButtonFace(string label, out UIElement face, out ProgressBar spinner)
    {
        var caption = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var working = new ProgressBar
        {
            Width = 17,
            Height = 17,
            Value = 0,
            IsIndeterminate = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UseStyle(working, "MaterialDesignCircularProgressBar");
        working.SetBinding(ForegroundProperty, new Binding(nameof(Control.Foreground))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Button) }
        });

        var stack = new Grid();
        stack.Children.Add(caption);
        stack.Children.Add(working);

        face = caption;
        spinner = working;
        return stack;
    }

    public IReadOnlyDictionary<string, string> Values() =>
        _getters.ToDictionary(getter => getter.Key, getter => getter.Value(), StringComparer.OrdinalIgnoreCase);

    public void Restore(
        IEnumerable<KeyValuePair<string, string>> values,
        IEnumerable<KeyValuePair<string, string>> outputs)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(outputs);

        foreach ((string name, string value) in values)
            if (_setters.TryGetValue(name, out Action<string>? setter))
                setter(value);

        foreach ((string name, string value) in outputs)
            if (_outputs.TryGetValue(name, out TextBox? output))
                output.Text = value;
    }

    public void ClearOutputs()
    {
        foreach (TextBox box in _outputs.Values)
            box.Clear();
        foreach (TablePane table in _tables.Values)
            table.Rows.Clear();
    }

    /// <summary>Empties one box or table. An empty name means the panel's first box.</summary>
    /// <param name="box">The box's or table's name.</param>
    /// <remarks>
    /// This is the host clearing the box, so it deliberately does not raise
    /// <see cref="OutputCleared"/>: only the box's own button does, and echoing the host's own call
    /// back at it would put the two in a loop.
    /// </remarks>
    public void ClearOutput(string box)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (_outputs.TryGetValue(box, out TextBox? target))
            target.Clear();
        else if (_tables.TryGetValue(box, out TablePane? table))
            table.Rows.Clear();
        else if (box.Length == 0)
            _outputs.Values.FirstOrDefault()?.Clear();
    }

    public void Append(string box, string text)
    {
        TextBox? target = _outputs.TryGetValue(box, out TextBox? known) ? known : _outputs.Values.FirstOrDefault();
        if (target is null)
            return;
        target.AppendText(text + "\n");
        target.ScrollToEnd();
    }

    public void SetOutput(string box, string text)
    {
        if (!_outputs.TryGetValue(box, out TextBox? target))
            return;
        target.Text = text;
        target.ScrollToEnd();
    }

    /// <summary>Changes what a control shows.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="value">The new value.</param>
    public void SetFieldValue(string name, string value)
    {
        if (_setters.TryGetValue(name, out Action<string>? setter))
            setter(value ?? "");
    }

    /// <summary>Moves a progress bar.</summary>
    /// <param name="name">The bar's name.</param>
    /// <param name="value">How far along, from 0 to 1.</param>
    public void SetProgress(string name, double value)
    {
        if (!_bars.TryGetValue(name, out (ProgressBar Bar, TextBlock Percent) bar))
            return;
        double share = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1) * 100;
        bar.Bar.Value = share;
        bar.Percent.Text = share.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Replaces a status line.</summary>
    /// <param name="name">The line's name.</param>
    /// <param name="text">What it should say.</param>
    public void SetStatus(string name, string text)
    {
        if (_statuses.TryGetValue(name, out TextBlock? line))
            line.Text = text ?? "";
    }

    /// <summary>Enables or disables a control.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="enabled">Whether it can be used.</param>
    public void SetEnabled(string name, bool enabled)
    {
        if (!_named.TryGetValue(name, out FrameworkElement? element))
            return;

        // A button the script disabled has to stay disabled when the run ends and the panel is
        // handed back, so the wish is remembered and the busy state is applied on top of it.
        if (element is Button button && _wanted.ContainsKey(button))
        {
            _wanted[button] = enabled;
            button.IsEnabled = enabled && !_busy;
            return;
        }

        element.IsEnabled = enabled;
    }

    /// <summary>Shows or hides a control. A hidden control takes no space.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="visible">Whether it is shown.</param>
    public void SetVisible(string name, bool visible)
    {
        if (!_named.TryGetValue(name, out FrameworkElement? element))
            return;

        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        // Collapsing an element inside a row leaves its column standing, so the row it lives in has
        // to be measured out again.
        if (_rowSlots.TryGetValue(element, out RowLayout? layout))
            Relayout(layout);
    }

    /// <summary>
    /// Turns the panel's buttons off while a run is starting, and hands them back afterwards.
    /// </summary>
    /// <param name="busy">Whether a run is on its way up.</param>
    /// <remarks>
    /// Busy is not the same as running. A script that registered click handlers keeps its run alive
    /// precisely so its buttons can be pressed, and the host says so by clearing this as soon as
    /// the handlers are up — leaving them off for as long as the run lasts would make the panel
    /// dead in exactly the case it was written for. What a script asked for with <c>Ui.Enable</c>
    /// survives either way.
    /// </remarks>
    public void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (Button button in _buttons)
            button.IsEnabled = !busy && _wanted.GetValueOrDefault(button, true);
    }

    /// <summary>Shows, or stops showing, that one button is working.</summary>
    /// <param name="name">The button's name.</param>
    /// <param name="busy">Whether it is working.</param>
    /// <remarks>
    /// Deliberately independent of <see cref="SetEnabled"/>: a button can be working and still worth
    /// pressing — a stop button most of all — so this changes what it shows and nothing else. The
    /// spinner only turns while it is on screen, because an indeterminate bar animates whether or
    /// not anyone can see it.
    /// </remarks>
    public void SetButtonBusy(string name, bool busy)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_named.GetValueOrDefault(name) is not Button button ||
            !_busyParts.TryGetValue(button, out (UIElement Face, ProgressBar Spinner) parts))
            return;

        parts.Spinner.IsIndeterminate = busy;
        parts.Spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        parts.Face.Visibility = busy ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>Appends a row to a table.</summary>
    /// <param name="table">The table's name.</param>
    /// <param name="cells">The row, left to right. Cells past the last column are kept, not shown.</param>
    public void AddRow(string table, IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(cells);
        if (!_tables.TryGetValue(table, out TablePane? pane))
            return;

        pane.Rows.Add(new TableRow([.. cells.Select(Flatten)]));

        if (pane.List.SelectedIndex < 0)
            pane.Trail.Start();
    }

    /// <summary>Empties a table.</summary>
    /// <param name="table">The table's name.</param>
    public void ClearTable(string table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (_tables.TryGetValue(table, out TablePane? pane))
            pane.Rows.Clear();
    }

    /// <summary>
    /// Shows a short message over the panel that fades on its own.
    /// </summary>
    /// <param name="text">What to say.</param>
    /// <param name="problem">Whether this reports something going wrong.</param>
    /// <remarks>
    /// Messages stack instead of replacing one another, each keeping its own three seconds, so two
    /// things happening at once are both readable. Only the last few are kept: a script in a loop
    /// would otherwise paper over the panel it is reporting on. Nothing here can be clicked or
    /// focused — see the host in the markup.
    /// </remarks>
    public void Toast(string text, bool problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Trim().Length == 0)
            return;

        var stripe = new Border { Width = 3, CornerRadius = new CornerRadius(2, 0, 0, 2) };
        stripe.SetResourceReference(Border.BackgroundProperty, problem ? "QxDangerBrush" : "QxAccentBrush");
        DockPanel.SetDock(stripe, Dock.Left);

        var icon = new PackIcon
        {
            Kind = problem ? PackIconKind.AlertCircleOutline : PackIconKind.InformationOutline,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 1, 9, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        icon.SetResourceReference(ForegroundProperty, problem ? "QxDangerBrush" : "QxAccentBrush");
        DockPanel.SetDock(icon, Dock.Left);

        TextBlock message = MakeText(text, 12.5, FontWeights.Normal, 0.92);
        message.SetResourceReference(FontFamilyProperty, "FontUI");
        message.TextWrapping = TextWrapping.Wrap;
        message.LineHeight = 18;
        message.MaxWidth = 320;

        var body = new DockPanel { Margin = new Thickness(13, 10, 15, 10) };
        body.Children.Add(icon);
        body.Children.Add(message);

        var tint = new Border { Child = body };
        tint.SetResourceReference(Border.BackgroundProperty, "QxSurfaceSubtleBrush");

        var layers = new DockPanel();
        layers.Children.Add(stripe);
        layers.Children.Add(tint);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(0, 8, 0, 0),
            MinWidth = 170,
            Opacity = 0,
            Child = layers
        };
        card.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        card.SetResourceReference(Border.BorderBrushProperty, problem ? "QxDangerBrush" : "MaterialDesign.Brush.Divider");
        MakeAccessible(card, text);
        AutomationProperties.SetLiveSetting(card, problem ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);

        ToastHost.Children.Add(card);
        while (ToastHost.Children.Count > VisibleToasts)
            ToastHost.Children.RemoveAt(0);

        var rise = new TranslateTransform();
        card.RenderTransform = rise;
        rise.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(190))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        // One timeline rather than an appearance and a disappearance racing for the same property:
        // the whole life of the message is written out, and its end is where it leaves the panel.
        var life = new DoubleAnimationUsingKeyFrames();
        life.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3000))));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3420))));
        life.Completed += (_, _) => ToastHost.Children.Remove(card);
        card.BeginAnimation(OpacityProperty, life);
    }
}
