using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;
using Qx.Scripting;

namespace Qx.Presentation.Services.Panels;

public sealed record PanelToast(long Id, string Text, bool IsProblem);

public sealed partial class PanelDocument : ObservableObject, IPanelRunTarget, IDisposable
{
    public const int ToastCapacity = 4;
    public static readonly TimeSpan ToastLifetime = TimeSpan.FromMilliseconds(3420);

    readonly ScriptDocument _document;
    readonly IScriptPrompts _prompts;
    readonly IClipboardService _clipboard;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly Dictionary<string, PanelFieldNode> _fields = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PanelOutputNode> _outputs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PanelTableNode> _tables = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PanelButtonNode> _buttons = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _button_names = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PanelProgressNode> _progress = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PanelStatusNode> _statuses = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<PanelNode>> _named = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    readonly List<CopyAction> _copies = [];
    string _directive_key = "\0";
    PanelOutputNode? _first_output;
    long _run_epoch;
    bool _built;
    bool _run_busy;
    long _toast_id;

    public PanelDocument(ScriptDocument document, IScriptPrompts prompts, IClipboardService clipboard, IUiDispatcher dispatcher, TimeProvider time)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        Toasts = new ToastQueue<PanelToast>(dispatcher, time, ToastCapacity, ToastLifetime);
    }

    public ObservableCollection<PanelNode> Nodes { get; } = [];

    public ToastQueue<PanelToast> Toasts { get; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string Description { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsStarting { get; private set; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    public IReadOnlySet<string> DeclaredButtons => _button_names;

    public event Action<string, string>? FieldEdited;

    public event Action<string?>? ButtonPressed;

    public event Action<string>? Warned;

    public bool Rebuild(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string key = PanelValueFormats.DirectiveKey(code);
        if (string.Equals(key, _directive_key, StringComparison.Ordinal))
            return false;
        _directive_key = key;
        UiSpec spec = UiSpec.Parse(code);
        Dictionary<string, List<PanelNode>> previous = new(_named, StringComparer.OrdinalIgnoreCase);
        ClearRegistries();
        Title = spec.Title;
        Description = spec.Description;
        bool first_button = true;
        List<PanelNode> built = Build(spec.Nodes, in_row: false, ref first_button);
        Dictionary<string, string> kept = new(_values, StringComparer.OrdinalIgnoreCase);
        _values.Clear();
        foreach ((string name, PanelFieldNode field) in _fields)
        {
            if (kept.TryGetValue(name, out string? value))
                field.WriteValue(value);
            _values[name] = field.ReadValue();
        }
        _built = true;
        foreach ((string name, List<PanelNode> nodes) in _named)
        {
            if (nodes.GroupBy(node => node.GetType()).Any(kind => kind.Count() > 1))
                Warned?.Invoke($"warning: //@ui name \"{name}\" is declared more than once; the last one is used");
            if (!previous.TryGetValue(name, out List<PanelNode>? old))
                continue;
            foreach (PanelNode node in nodes)
            {
                if (old.FirstOrDefault(candidate => candidate.GetType() == node.GetType()) is { } match)
                    node.CarryFrom(match);
            }
        }
        Nodes.Clear();
        foreach (PanelNode node in built)
            Nodes.Add(node);
        ApplyBusy();
        return true;
    }

    public IReadOnlyDictionary<string, string> Values()
    {
        var values = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
        foreach ((string name, PanelTableNode table) in _tables)
        {
            if (table.IsSelectable)
                values[name] = table.SelectedValue;
        }
        return values;
    }

    public IReadOnlyDictionary<string, string> PersistedValues() =>
        new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);

    public void Restore(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
            return;
        foreach ((string name, string value) in values)
            SetFieldValue(name, value);
    }

    public void SetFieldValue(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_fields.TryGetValue(name, out PanelFieldNode? field))
        {
            field.WriteValue(value ?? "");
            _values[name] = field.ReadValue();
            return;
        }
        if (!_built)
            _values[name] = value ?? "";
    }

    public void AppendOutput(string box, string text)
    {
        if (Resolve(box) is { } output)
            output.Append(text);
    }

    public void ClearByScript(string name)
    {
        if (_tables.TryGetValue(name, out PanelTableNode? table))
            table.Clear();
        if (Resolve(name) is { } output)
            output.Clear();
    }

    public void SetProgress(string name, double fraction)
    {
        if (_progress.TryGetValue(name, out PanelProgressNode? progress))
            progress.Fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
    }

    public void SetStatus(string name, string text)
    {
        if (_statuses.TryGetValue(name, out PanelStatusNode? status))
            status.Text = text;
    }

    public void SetEnabled(string name, bool enabled)
    {
        if (!_named.TryGetValue(name, out List<PanelNode>? nodes))
            return;
        foreach (PanelNode node in nodes)
            node.IsWanted = enabled;
    }

    public void SetVisible(string name, bool visible)
    {
        if (!_named.TryGetValue(name, out List<PanelNode>? nodes))
            return;
        foreach (PanelNode node in nodes)
            node.IsVisible = visible;
    }

    public void AddRow(string table, IReadOnlyList<string> cells)
    {
        if (_tables.TryGetValue(table, out PanelTableNode? node))
            node.AddRow(cells);
    }

    public void Toast(string text, bool problem)
    {
        if (IsShown && !string.IsNullOrWhiteSpace(text))
            Toasts.Show(new PanelToast(++_toast_id, text, problem));
    }

    public void SetButtonBusy(string button, bool busy)
    {
        if (_buttons.TryGetValue(button, out PanelButtonNode? node))
            node.IsBusy = busy;
    }

    public void BeginStarting(string? pressed_button)
    {
        IsStarting = true;
        if (pressed_button is { Length: > 0 })
            SetButtonBusy(pressed_button, true);
    }

    public void EndStarting(string? pressed_button)
    {
        IsStarting = false;
        if (pressed_button is { Length: > 0 })
            SetButtonBusy(pressed_button, false);
    }

    public void SetRunBusy(bool busy)
    {
        _run_busy = busy;
        ApplyBusy();
    }

    public IDisposable Attach(ScriptUi ui, long run_epoch, string file_name, string? pressed_button, CancellationToken run_token)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _run_epoch = run_epoch;
        return new PanelRunLink(this, ui, _dispatcher, pressed_button, run_token);
    }

    public void Dispose()
    {
        Toasts.Dispose();
        foreach (CopyAction copy in _copies)
            copy.Dispose();
    }

    internal Task<bool> ConfirmAsync(string title, string message, CancellationToken run_token) =>
        _prompts.ConfirmAsync(_document, _run_epoch, title, message, run_token);

    internal Task<string?> PromptAsync(string title, string initial, CancellationToken run_token) =>
        _prompts.PromptAsync(_document, _run_epoch, title, initial, run_token);

    internal Task DownloadAsync(string file_name, string content, CancellationToken run_token) =>
        _prompts.DownloadAsync(_document, _run_epoch, file_name, content, run_token);

    PanelOutputNode? Resolve(string box) =>
        box.Length == 0 ? _first_output : _outputs.GetValueOrDefault(box);

    void ApplyBusy()
    {
        foreach (PanelButtonNode button in _buttons.Values)
            button.IsPanelBusy = _run_busy;
    }

    void ClearRegistries()
    {
        _fields.Clear();
        _outputs.Clear();
        _tables.Clear();
        _buttons.Clear();
        _button_names.Clear();
        _progress.Clear();
        _statuses.Clear();
        _named.Clear();
        _first_output = null;
        foreach (CopyAction copy in _copies)
            copy.Dispose();
        _copies.Clear();
    }

    List<PanelNode> Build(IReadOnlyList<UiNode> source, bool in_row, ref bool first_button)
    {
        var built = new List<PanelNode>();
        List<PanelButtonNode>? strip = null;
        UiNode? strip_anchor = null;
        foreach (UiNode node in source)
        {
            PanelNode? created = Create(node, ref first_button);
            if (created is null)
                continue;
            if (!in_row && created is PanelButtonNode button)
            {
                strip ??= [];
                strip_anchor ??= node;
                strip.Add(button);
                continue;
            }
            if (strip is not null && strip_anchor is not null)
            {
                built.Add(new PanelButtonStripNode(strip_anchor, strip));
                strip = null;
                strip_anchor = null;
            }
            built.Add(created);
        }
        if (strip is not null && strip_anchor is not null)
            built.Add(new PanelButtonStripNode(strip_anchor, strip));
        return built;
    }

    PanelNode? Create(UiNode node, ref bool first_button)
    {
        switch (node)
        {
            case UiRowNode row:
                return new PanelRowNode(row, Build(row.Children, in_row: true, ref first_button));
            case UiGroupNode group:
                return new PanelGroupNode(group, Build(group.Children, in_row: false, ref first_button));
            case UiLabelNode label:
                return new PanelLabelNode(label);
            case UiSeparatorNode separator:
                return new PanelSeparatorNode(separator);
            case UiSpacerNode spacer:
                return new PanelSpacerNode(spacer);
            case UiSectionNode section:
                return new PanelSectionNode(section);
            case UiButtonNode button:
                var pressed = new PanelButtonNode(button, first_button, name => ButtonPressed?.Invoke(name));
                first_button = false;
                _button_names.Add(pressed.Name);
                return Register(_buttons, pressed.Name, pressed);
            case UiOutputNode output:
                var box = new PanelOutputNode(output, CopyOf(() => _outputs.GetValueOrDefault(output.Output.Name)?.Text));
                _first_output ??= box;
                return Register(_outputs, box.Name, box);
            case UiTableNode table:
                var rows = new PanelTableNode(table, CopyOf(() => _tables.GetValueOrDefault(table.Name)?.Tabulate()));
                return Register(_tables, rows.Name, rows);
            case UiProgressNode progress:
                return Register(_progress, progress.Name, new PanelProgressNode(progress));
            case UiStatusNode status:
                return Register(_statuses, status.Name, new PanelStatusNode(status));
            case UiFieldNode field:
                PanelFieldNode made = field.Field.Kind switch
                {
                    UiFieldKind.Int or UiFieldKind.Number => new PanelNumberFieldNode(field, OnFieldEdited),
                    UiFieldKind.Bool => new PanelBoolFieldNode(field, OnFieldEdited),
                    UiFieldKind.Select => new PanelSelectFieldNode(field, OnFieldEdited),
                    UiFieldKind.Slider => new PanelSliderFieldNode(field, OnFieldEdited),
                    UiFieldKind.Color => new PanelColorFieldNode(field, OnFieldEdited),
                    UiFieldKind.File => new PanelFileFieldNode(field, OnFieldEdited),
                    _ => new PanelTextFieldNode(field, OnFieldEdited)
                };
                return Register(_fields, made.Name, made);
            default:
                return null;
        }
    }

    T Register<T>(Dictionary<string, T> registry, string name, T node) where T : PanelNode
    {
        registry[name] = node;
        if (!_named.TryGetValue(name, out List<PanelNode>? nodes))
            _named[name] = nodes = [];
        nodes.Add(node);
        return node;
    }

    CopyAction CopyOf(Func<string?> text)
    {
        var copy = new CopyAction(_clipboard, _dispatcher, _time, text);
        _copies.Add(copy);
        return copy;
    }

    void OnFieldEdited(string name, string value)
    {
        _values[name] = value;
        FieldEdited?.Invoke(name, value);
    }
}
