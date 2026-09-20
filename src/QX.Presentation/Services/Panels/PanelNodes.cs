using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Mvvm;
using Qx.Scripting;

namespace Qx.Presentation.Services.Panels;

public abstract partial class PanelNode : ObservableObject
{
    protected PanelNode(UiNode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Tooltip = source.Attr.Text("tooltip") is { Length: > 0 } tip ? tip : null;
        Width = source.Width is { } width && double.IsFinite(width) && width > 0 ? width : null;
        Grow = source.Grow is { } grow && double.IsFinite(grow) && grow > 0 ? grow : null;
    }

    public virtual string? Name => null;

    public string? Tooltip { get; }

    public double? Width { get; }

    public double? Grow { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsWanted { get; set; } = true;

    public virtual void CarryFrom(PanelNode previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        IsVisible = previous.IsVisible;
        IsWanted = previous.IsWanted;
    }
}

public abstract partial class PanelFieldNode : PanelNode
{
    readonly Action<string, string> _edited;
    bool _writing;

    protected PanelFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source)
    {
        _edited = edited ?? throw new ArgumentNullException(nameof(edited));
        Field = source.Field;
        Name = source.Field.Name;
        Label = source.Field.Label;
        Placeholder = source.Attr.Text("placeholder") is { Length: > 0 } hint ? hint : null;
        Help = source.Attr.Text("help") is { Length: > 0 } help ? help : null;
    }

    public UiField Field { get; }

    public override string Name { get; }

    public string Label { get; }

    public string? Placeholder { get; protected init; }

    public string? Help { get; protected init; }

    public abstract string ReadValue();

    public void WriteValue(string value)
    {
        _writing = true;
        try
        {
            Apply(value ?? "");
        }
        finally
        {
            _writing = false;
        }
    }

    protected abstract void Apply(string value);

    protected void Seed(Action seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _writing = true;
        try
        {
            seed();
        }
        finally
        {
            _writing = false;
        }
    }

    protected void Edited()
    {
        if (!_writing)
            _edited(Name, ReadValue());
    }
}

public sealed partial class PanelTextFieldNode : PanelFieldNode
{
    public PanelTextFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        IsMultiline = source.Field.Kind == UiFieldKind.Text;
        WriteValue(source.Field.Default);
    }

    public bool IsMultiline { get; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    public override string ReadValue() => Text;

    protected override void Apply(string value) => Text = value;

    partial void OnTextChanged(string value) => Edited();
}

public sealed partial class PanelNumberFieldNode : PanelFieldNode
{
    public PanelNumberFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        IsWhole = source.Field.Kind == UiFieldKind.Int;
        if (Help is null && source.Field.Min is { } min && source.Field.Max is { } max)
            Help = string.Create(CultureInfo.InvariantCulture, $"{min:0.##} – {max:0.##}");
        WriteValue(source.Field.Default);
    }

    public bool IsWhole { get; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    public override string ReadValue() => IsWhole ? PanelValueFormats.Whole(Text) : PanelValueFormats.Number(Text);

    protected override void Apply(string value) => Text = value;

    partial void OnTextChanged(string value) => Edited();
}

public sealed partial class PanelBoolFieldNode : PanelFieldNode
{
    public PanelBoolFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited) => WriteValue(source.Field.Default);

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public override string ReadValue() => PanelValueFormats.Bool(IsChecked);

    protected override void Apply(string value) => IsChecked = PanelValueFormats.ParseBool(value);

    partial void OnIsCheckedChanged(bool value) => Edited();
}

public sealed partial class PanelSelectFieldNode : PanelFieldNode
{
    public PanelSelectFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        Options = source.Field.Options.Count == 0 ? [] : [.. source.Field.Options];
        string wanted = source.Field.Default;
        Seed(() => Selected = Options.FirstOrDefault(option => string.Equals(option, wanted, StringComparison.Ordinal)) ?? Options.FirstOrDefault());
    }

    public IReadOnlyList<string> Options { get; }

    [ObservableProperty]
    public partial string? Selected { get; set; }

    public override string ReadValue() => Selected ?? "";

    protected override void Apply(string value)
    {
        if (Options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) is { } match)
            Selected = match;
    }

    partial void OnSelectedChanged(string? value) => Edited();
}

public sealed partial class PanelSliderFieldNode : PanelFieldNode
{
    public PanelSliderFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        double low = PanelValueFormats.Bound(source.Field.Min, 0);
        double high = PanelValueFormats.Bound(source.Field.Max, 100);
        Minimum = Math.Min(low, high);
        Maximum = Math.Max(low, high);
        IsWhole = source.Field.Attr.Flag("whole") ?? (source.Field.Default.IndexOf('.') < 0 && Minimum == Math.Floor(Minimum));
        Seed(() => Value = Minimum);
        WriteValue(source.Field.Default);
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public bool IsWhole { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Readout))]
    public partial double Value { get; set; }

    public string Readout => PanelValueFormats.Slider(Value, IsWhole);

    public override string ReadValue() => PanelValueFormats.Slider(Value, IsWhole);

    protected override void Apply(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || !double.IsFinite(parsed))
            return;
        Value = Math.Clamp(parsed, Minimum, Maximum);
    }

    partial void OnValueChanged(double value) => Edited();
}

public sealed partial class PanelColorFieldNode : PanelFieldNode
{
    public PanelColorFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        Placeholder ??= "#RRGGBB";
        WriteValue(source.Field.Default.Length > 0 ? source.Field.Default : PanelValueFormats.DefaultColor);
    }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    public override string ReadValue() => Text;

    protected override void Apply(string value) => Text = value;

    partial void OnTextChanged(string value) => Edited();
}

public sealed partial class PanelFileFieldNode : PanelFieldNode
{
    public PanelFileFieldNode(UiFieldNode source, Action<string, string> edited)
        : base(source, edited)
    {
        Placeholder ??= "No file selected";
        WriteValue(source.Field.Default);
    }

    [ObservableProperty]
    public partial string Path { get; set; } = "";

    public override string ReadValue() => Path;

    protected override void Apply(string value) => Path = value;

    partial void OnPathChanged(string value) => Edited();
}

public enum PanelButtonLook
{
    Normal,
    Primary,
    Quiet,
    Danger
}

public sealed partial class PanelButtonNode : PanelNode
{
    readonly Action<string> _pressed;

    public PanelButtonNode(UiButtonNode source, bool first, Action<string> pressed)
        : base(source)
    {
        _pressed = pressed ?? throw new ArgumentNullException(nameof(pressed));
        Name = source.Button.Name;
        Label = source.Button.Label;
        Look = source.Button.Style switch
        {
            UiButtonStyle.Primary => PanelButtonLook.Primary,
            UiButtonStyle.Quiet => PanelButtonLook.Quiet,
            UiButtonStyle.Danger => PanelButtonLook.Danger,
            UiButtonStyle.Normal => PanelButtonLook.Normal,
            _ => first ? PanelButtonLook.Primary : PanelButtonLook.Normal
        };
    }

    public override string Name { get; }

    public string Label { get; }

    public PanelButtonLook Look { get; }

    public bool IsPrimary => Look == PanelButtonLook.Primary;

    public bool IsQuiet => Look == PanelButtonLook.Quiet;

    public bool IsDanger => Look == PanelButtonLook.Danger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PressCommand))]
    public partial bool IsPanelBusy { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.PropertyName == nameof(IsWanted))
            PressCommand.NotifyCanExecuteChanged();
    }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is PanelButtonNode button)
            IsBusy = button.IsBusy;
    }

    bool CanPress() => IsWanted && !IsPanelBusy;

    [RelayCommand(CanExecute = nameof(CanPress))]
    void Press() => _pressed(Name);
}

public sealed class PanelButtonStripNode(UiNode anchor, IReadOnlyList<PanelButtonNode> buttons) : PanelNode(anchor)
{
    public IReadOnlyList<PanelButtonNode> Buttons { get; } = buttons ?? throw new ArgumentNullException(nameof(buttons));
}

public readonly record struct PanelOutputChange(bool Reset, string Text);

public sealed partial class PanelOutputNode : PanelNode
{
    readonly StringBuilder _buffer = new();

    public PanelOutputNode(UiOutputNode source, CopyAction copy)
        : base(source)
    {
        Name = source.Output.Name;
        Label = source.Output.Label;
        Height = PanelValueFormats.Extent(source.Output.Height, PanelValueFormats.DefaultOutputHeight);
        Wrap = source.Output.Wrap;
        Monospace = source.Output.Monospace;
        HasToolbar = source.Output.Toolbar;
        Copy = copy ?? throw new ArgumentNullException(nameof(copy));
    }

    public override string Name { get; }

    public string Label { get; }

    public double Height { get; }

    public bool Wrap { get; }

    public bool Monospace { get; }

    public bool HasToolbar { get; }

    public CopyAction Copy { get; }

    public string Text => _buffer.ToString();

    public bool IsEmpty => _buffer.Length == 0;

    public event Action<PanelOutputChange>? Changed;

    public void Append(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int before = _buffer.Length;
        bool truncated = PanelValueFormats.AppendLine(_buffer, line);
        Changed?.Invoke(truncated ? new PanelOutputChange(true, _buffer.ToString()) : new PanelOutputChange(false, _buffer.ToString(before, _buffer.Length - before)));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    public void Clear()
    {
        _buffer.Clear();
        Changed?.Invoke(new PanelOutputChange(true, ""));
        OnPropertyChanged(nameof(IsEmpty));
    }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is not PanelOutputNode output)
            return;
        _buffer.Clear().Append(output._buffer);
        Changed?.Invoke(new PanelOutputChange(true, _buffer.ToString()));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed class PanelTableRow(IReadOnlyList<string> cells)
{
    public IReadOnlyList<string> Cells { get; } = cells ?? throw new ArgumentNullException(nameof(cells));

    public string Cell(int index) => index >= 0 && index < Cells.Count ? Cells[index] : "";

    public string Joined => string.Join('\t', Cells);
}

public sealed partial class PanelTableNode : PanelNode
{
    public PanelTableNode(UiTableNode source, CopyAction copy)
        : base(source)
    {
        Name = source.Name;
        Label = source.Label;
        Columns = source.Columns.Count == 0 ? ["Value"] : source.Columns;
        Height = PanelValueFormats.Extent(source.Height, PanelValueFormats.DefaultTableHeight);
        IsSelectable = source.Selectable;
        HasToolbar = source.Toolbar;
        Copy = copy ?? throw new ArgumentNullException(nameof(copy));
    }

    public override string Name { get; }

    public string Label { get; }

    public IReadOnlyList<string> Columns { get; }

    public double Height { get; }

    public bool IsSelectable { get; }

    public bool HasToolbar { get; }

    public CopyAction Copy { get; }

    public ObservableCollection<PanelTableRow> Rows { get; } = [];

    [ObservableProperty]
    public partial PanelTableRow? Selected { get; set; }

    public string SelectedValue => IsSelectable && Selected is { } row ? row.Joined : "";

    public string RowCountText => Rows.Count == 1 ? "1 row" : $"{Rows.Count} rows";

    [RelayCommand]
    public void Clear()
    {
        Rows.Clear();
        OnPropertyChanged(nameof(RowCountText));
    }

    public void AddRow(IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Rows.Add(new PanelTableRow([.. cells.Select(PanelValueFormats.Cell)]));
        OnPropertyChanged(nameof(RowCountText));
    }

    public string Tabulate()
    {
        var text = new StringBuilder();
        text.Append(string.Join('\t', Columns)).Append('\n');
        foreach (PanelTableRow row in Rows)
            text.Append(row.Joined).Append('\n');
        return text.ToString();
    }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is not PanelTableNode table)
            return;
        foreach (PanelTableRow row in table.Rows)
            Rows.Add(row);
        OnPropertyChanged(nameof(RowCountText));
        if (IsSelectable)
            Selected = table.Selected;
    }

    partial void OnSelectedChanged(PanelTableRow? value)
    {
        if (!IsSelectable && value is not null)
            Selected = null;
    }
}

public sealed class PanelLabelNode(UiLabelNode source) : PanelNode(source)
{
    public string Text { get; } = source.Text;
}

public sealed class PanelSeparatorNode(UiSeparatorNode source) : PanelNode(source);

public sealed class PanelSpacerNode(UiSpacerNode source) : PanelNode(source)
{
    public double Height { get; } = PanelValueFormats.Extent(source.Height, PanelValueFormats.DefaultSpacerHeight);
}

public sealed class PanelSectionNode(UiSectionNode source) : PanelNode(source)
{
    public string Title { get; } = source.Title;
}

public sealed class PanelRowNode(UiRowNode source, IReadOnlyList<PanelNode> children) : PanelNode(source)
{
    public IReadOnlyList<PanelNode> Children { get; } = children ?? throw new ArgumentNullException(nameof(children));

    public double Gap { get; } = source.Gap is var gap && double.IsFinite(gap) && gap > 0 ? gap : 0;

    public UiRowAlign Align { get; } = source.Align;
}

public sealed partial class PanelGroupNode : PanelNode
{
    public PanelGroupNode(UiGroupNode source, IReadOnlyList<PanelNode> children)
        : base(source)
    {
        Title = source.Title;
        Children = children ?? throw new ArgumentNullException(nameof(children));
        IsExpanded = !source.Collapsed;
    }

    public string Title { get; }

    public IReadOnlyList<PanelNode> Children { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is PanelGroupNode group && string.Equals(group.Title, Title, StringComparison.Ordinal))
            IsExpanded = group.IsExpanded;
    }
}

public sealed partial class PanelProgressNode : PanelNode
{
    public PanelProgressNode(UiProgressNode source)
        : base(source)
    {
        Name = source.Name;
        Label = source.Label;
    }

    public override string Name { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial double Fraction { get; set; }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is PanelProgressNode progress)
            Fraction = progress.Fraction;
    }
}

public sealed partial class PanelStatusNode : PanelNode
{
    public PanelStatusNode(UiStatusNode source)
        : base(source)
    {
        Name = source.Name;
        Label = source.Label;
        Text = source.Initial;
    }

    public override string Name { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial string Text { get; set; }

    public override void CarryFrom(PanelNode previous)
    {
        base.CarryFrom(previous);
        if (previous is PanelStatusNode status)
            Text = status.Text;
    }
}
