using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.GameCatalog;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.GameCatalog;

public sealed partial class KeyValueRowViewModel : ObservableObject
{
    public KeyValueRowViewModel(KeyValueEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Key = entry.Key;
        Value = entry.Value;
    }

    public string Key { get; }

    [ObservableProperty]
    public partial string Value { get; private set; }

    public void Apply(KeyValueEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Value = entry.Value;
    }
}

public sealed record KeyValueLabels(string Singular, string Plural, string Placeholder, string ListName, string KeyHeader, string ValueHeader);

public sealed partial class KeyValueTabViewModel : ViewModelBase
{
    static readonly KeyComparer _order = new();

    readonly KeyedRows<string, KeyValueRowViewModel> _all = new(StringComparer.Ordinal);
    readonly NoticeLine _notices;
    readonly CancellationToken _lifetime;
    readonly Debouncer _filter_delay;
    bool _reset_after_apply;
    bool _loaded;
    string _missing = "";

    public KeyValueTabViewModel(
        KeyValueLabels labels,
        IClipboardService clipboard,
        NoticeLine notices,
        IUiDispatcher dispatcher,
        TimeProvider time,
        CancellationToken lifetime)
    {
        Labels = labels ?? throw new ArgumentNullException(nameof(labels));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _lifetime = lifetime;
        ArgumentNullException.ThrowIfNull(dispatcher);
        _filter_delay = Own(new Debouncer(dispatcher, time, FurniTabViewModel.FilterDelay, ApplyFilter));
        Copy = Own(new CopyAction(clipboard, dispatcher, time, SelectedText, Failed));
        Rows.Applied += OnRowsApplied;
        Own(() => Rows.Applied -= OnRowsApplied);
        RefreshState();
    }

    public KeyValueLabels Labels { get; }

    public FilteredRows<KeyValueRowViewModel> Rows { get; } = new();

    public SelectionList<KeyValueRowViewModel> Selection { get; } = new();

    public ScrollRequest Scroll { get; } = new();

    public ViewState State { get; } = new();

    public CopyAction Copy { get; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    public void Load(IReadOnlyList<KeyValueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _loaded = true;
        _missing = "";
        _all.Sync(entries, entry => entry.Key, entry => new KeyValueRowViewModel(entry), (row, entry) => row.Apply(entry));
        ApplyFilter();
    }

    public void ShowMissing(string reason)
    {
        _loaded = false;
        _missing = reason;
        _all.Clear();
        ApplyFilter();
    }

    public bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        SearchText = "";
        return true;
    }

    [RelayCommand]
    void ClearSearch() => SearchText = "";

    partial void OnSearchTextChanged(string value)
    {
        _reset_after_apply = true;
        _filter_delay.Trigger();
    }

    void ApplyFilter()
    {
        if (IsDisposed)
            return;
        string term = SearchText.Trim();
        Rows.ApplyAsync(_all.Rows, row => Matches(row, term), _order, _lifetime).Observe("ui");
    }

    static bool Matches(KeyValueRowViewModel row, string term) =>
        term.Length == 0 ||
        row.Key.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
        row.Value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    void OnRowsApplied()
    {
        Selection.Prune(Rows.Visible);
        int total = _all.Count;
        int shown = Rows.Visible.Count;
        CountText = shown == total
            ? total == 1 ? $"{total:N0} {Labels.Singular}" : $"{total:N0} {Labels.Plural}"
            : $"{shown:N0} of {total:N0} {Labels.Plural}";
        RefreshState();
        if (!_reset_after_apply)
            return;
        _reset_after_apply = false;
        Scroll.Reset();
    }

    void RefreshState()
    {
        if (!_loaded)
        {
            State.ShowEmpty(IconKind.GameData, "The hotel's game data has not arrived yet.", _missing);
            return;
        }
        if (Rows.Visible.Count > 0)
        {
            State.ShowReady();
            return;
        }
        string term = SearchText.Trim();
        if (term.Length == 0)
        {
            State.ShowEmpty(IconKind.GameData, $"There are no {Labels.Plural}.");
            return;
        }
        State.ShowEmpty(IconKind.Search, "No matches", $"Nothing matches “{term}”.", ClearSearchCommand, "Clear filters");
    }

    string? SelectedText()
    {
        IReadOnlyList<KeyValueRowViewModel> rows = Selection.HasAny ? Selection.Items : [.. Rows.Visible];
        return rows.Count == 0
            ? null
            : string.Join(Environment.NewLine, rows.Select(row => $"{row.Key}\t{row.Value}"));
    }

    void Failed(string message) => _notices.Show(NoticeSeverity.Warning, message);

    sealed class KeyComparer : IComparer<KeyValueRowViewModel>
    {
        public int Compare(KeyValueRowViewModel? left, KeyValueRowViewModel? right) =>
            string.CompareOrdinal(left?.Key, right?.Key);
    }
}
