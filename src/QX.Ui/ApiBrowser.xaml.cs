using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Qx.Scripting;

namespace Qx.Ui;

/// <summary>
/// The whole script surface in one searchable list, sitting beside the editor.
/// </summary>
/// <remarks>
/// The same members the completion list offers, except readable without knowing what to type
/// first, and filterable by what a member returns rather than only by what it is called. Picking
/// one writes it into the editor rather than leaving it to be copied out.
/// </remarks>
public partial class ApiBrowser : UserControl
{
    private readonly List<ToggleButton> _kinds = [];
    private readonly List<ToggleButton> _types = [];
    private ScriptApiKind? _kind;
    private string? _group;
    private string? _returnType;

    public ApiBrowser()
    {
        InitializeComponent();
        BuildKindTabs();
        BuildTypeTabs();
        Refresh();
    }

    /// <summary>Raised when a member was picked, with the text to write and where the caret goes.</summary>
    public event Action<string, int>? Picked;

    /// <summary>Raised when the browser should be put away.</summary>
    public event Action? Closed;

    /// <summary>Puts the caret in the search box, so typing begins a search straight away.</summary>
    public void TakeFocus()
    {
        SearchBox.SelectAll();
        SearchBox.Focus();
    }

    private void BuildKindTabs()
    {
        Add("All", null, null, true);
        Add("State", ScriptApiKind.State, null, false);
        Add("Actions", ScriptApiKind.Action, null, false);
        Add("Events", ScriptApiKind.Event, null, false);
        foreach (string group in ScriptApiCatalog.Groups)
            Add(group, null, group, false);

        void Add(string label, ScriptApiKind? kind, string? group, bool on)
        {
            var tab = new ToggleButton
            {
                Content = label,
                IsChecked = on,
                Style = (Style)FindResource("FilterTab"),
                Tag = (kind, group)
            };
            tab.Checked += KindPicked;
            tab.Unchecked += KindUnpicked;
            _kinds.Add(tab);
            KindTabs.Items.Add(tab);
        }
    }

    private void BuildTypeTabs()
    {
        Add("any type", null, true);
        foreach (string type in ScriptApiCatalog.ReturnTypes)
            Add(type, type, false);

        void Add(string label, string? type, bool on)
        {
            var tab = new ToggleButton
            {
                Content = label,
                IsChecked = on,
                Style = (Style)FindResource("TypeTab"),
                Tag = type,
                ToolTip = type is null ? "Every return type" : $"Members returning {type}"
            };
            tab.Checked += TypePicked;
            tab.Unchecked += TypeUnpicked;
            _types.Add(tab);
            TypeTabs.Items.Add(tab);
        }
    }

    private void KindPicked(object sender, RoutedEventArgs e)
    {
        var picked = (ToggleButton)sender;
        Only(_kinds, picked);
        (_kind, _group) = ((ScriptApiKind?, string?))picked.Tag;
        Refresh();
    }

    private void TypePicked(object sender, RoutedEventArgs e)
    {
        var picked = (ToggleButton)sender;
        Only(_types, picked);
        _returnType = (string?)picked.Tag;
        Refresh();
    }

    // Turning the last one off would show nothing at all, so it falls back to everything.
    private void KindUnpicked(object sender, RoutedEventArgs e) => Restore(_kinds);

    private void TypeUnpicked(object sender, RoutedEventArgs e) => Restore(_types);

    private static void Only(List<ToggleButton> tabs, ToggleButton picked)
    {
        foreach (ToggleButton other in tabs)
        {
            if (!ReferenceEquals(other, picked))
                other.IsChecked = false;
        }
    }

    private static void Restore(List<ToggleButton> tabs)
    {
        if (!tabs.Any(tab => tab.IsChecked == true))
            tabs[0].IsChecked = true;
    }

    private void Refilter(object sender, TextChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        IReadOnlyList<ScriptApiMember> found =
            ScriptApiCatalog.Search(SearchBox.Text, _kind, _group, _returnType);
        Results.ItemsSource = found;
        if (found.Count > 0)
            Results.SelectedIndex = 0;

        Status.Text = found.Count == ScriptApiCatalog.All.Count
            ? $"{found.Count} members"
            : $"{found.Count} of {ScriptApiCatalog.All.Count}";
    }

    private void SearchKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                if (SearchBox.Text.Length > 0)
                {
                    SearchBox.Clear();
                    return;
                }
                Closed?.Invoke();
                return;

            case Key.Enter:
                e.Handled = true;
                Insert();
                return;

            // The list is driven from the search box so the hands never have to leave it.
            case Key.Down:
                e.Handled = true;
                Move(1);
                return;

            case Key.Up:
                e.Handled = true;
                Move(-1);
                return;
        }
    }

    private void ResultKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Insert();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Closed?.Invoke();
        }
    }

    private void Move(int by)
    {
        if (Results.Items.Count == 0)
            return;
        int next = Math.Clamp(Results.SelectedIndex + by, 0, Results.Items.Count - 1);
        Results.SelectedIndex = next;
        Results.ScrollIntoView(Results.Items[next]);
    }

    private void Take(object sender, MouseButtonEventArgs e) => Insert();

    private void Insert()
    {
        if (Results.SelectedItem is ScriptApiMember member)
            Picked?.Invoke(member.Insert, member.CaretOffset);
    }

    private void Close(object sender, RoutedEventArgs e) => Closed?.Invoke();
}
