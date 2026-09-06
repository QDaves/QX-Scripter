using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Qx.Diagnostics;

namespace Qx.Ui;

public partial class LoggingPage : GamePage
{
    private const int Capacity = 5000;
    private readonly ObservableCollection<ApplicationLogEntry> _entries = [];
    private readonly ICollectionView _view;

    public LoggingPage()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_entries);
        _view.Filter = Matches;
        LogBox.ItemsSource = _view;
        UpdateState();
    }

    internal IReadOnlyList<ApplicationLogEntry> Entries => _entries;

    public override bool IsSearching => FilterBox.Text.Length > 0;

    public override void Refresh() => UpdateState();

    public override void Opened()
    {
        base.Opened();
        ScrollToLatest();
    }

    public void Append(DiagLevel level, string message, string? category = null)
    {
        OutputLevel severity = level switch
        {
            DiagLevel.Warn => OutputLevel.Warning,
            DiagLevel.Error => OutputLevel.Error,
            _ => OutputLevel.Info
        };
        string name = level switch
        {
            DiagLevel.Trace => "trace",
            DiagLevel.Debug => "debug",
            DiagLevel.Info => "info",
            DiagLevel.Warn => "warning",
            _ => "error"
        };
        Append(new ApplicationLogEntry(message, name, category, severity, DateTime.Now));
    }

    public void Append(OutputLevel level, string message, string? category = null)
    {
        string name = level switch
        {
            OutputLevel.Warning => "warning",
            OutputLevel.Error => "error",
            _ => "info"
        };
        Append(new ApplicationLogEntry(message, name, category, level, DateTime.Now));
    }

    internal void Clear()
    {
        _entries.Clear();
        UpdateState();
    }

    private void Append(ApplicationLogEntry entry)
    {
        _entries.Add(entry);
        while (_entries.Count > Capacity)
            _entries.RemoveAt(0);
        UpdateState();
        if (Visibility == Visibility.Visible && Matches(entry))
            LogBox.ScrollIntoView(entry);
    }

    private bool Matches(object value)
    {
        if (value is not ApplicationLogEntry entry)
            return false;
        if (ProblemsButton.IsChecked == true && entry.Severity is OutputLevel.Info)
            return false;

        string filter = FilterBox.Text.Trim();
        return filter.Length == 0 ||
               entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               entry.Level.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               (entry.Category?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void FilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshView();

    private void ProblemsChanged(object sender, RoutedEventArgs e) => RefreshView();

    private void RefreshView()
    {
        _view.Refresh();
        UpdateState();
    }

    private void UpdateState()
    {
        int visible = _view.Cast<ApplicationLogEntry>().Count();
        CountText.Text = visible == _entries.Count
            ? $"{_entries.Count:N0} events"
            : $"{visible:N0} of {_entries.Count:N0}";
        EmptyState.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _entries.Count == 0
            ? "No application events yet."
            : "No events match this filter.";
    }

    private void CopyLog(object sender, RoutedEventArgs e) => CopyVisible();

    private void LogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
            return;
        e.Handled = true;
        CopyVisible();
    }

    private void CopyVisible()
    {
        ApplicationLogEntry[] entries = LogBox.SelectedItems.Count > 0
            ? LogBox.SelectedItems.Cast<ApplicationLogEntry>().ToArray()
            : _view.Cast<ApplicationLogEntry>().ToArray();
        if (entries.Length == 0)
            return;

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, entries.Select(entry => entry.CopyText)));
        }
        catch (Exception error)
        {
            Append(OutputLevel.Error, $"Copy failed: {error.Message}", "ui");
        }
    }

    private void ClearLog(object sender, RoutedEventArgs e) => Clear();

    private void ScrollToLatest()
    {
        ApplicationLogEntry? latest = _view.Cast<ApplicationLogEntry>().LastOrDefault();
        if (latest is not null)
            LogBox.ScrollIntoView(latest);
    }
}
