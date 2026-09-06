using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Qx.Ui;

/// <summary>
/// One thing the palette can run.
/// </summary>
/// <param name="Title">What it is called, and what is matched against.</param>
/// <param name="Group">Which part of the window it belongs to, shown as a lead-in.</param>
/// <param name="Gesture">Its keyboard shortcut, or empty when it has none.</param>
/// <param name="Run">What it does. Runs after the palette has closed.</param>
/// <param name="IsAvailable">
/// Whether it can run right now. Unavailable commands are left out rather than shown disabled:
/// a list you can only look at is worse than a shorter one.
/// </param>
public sealed record PaletteCommand(
    string Title,
    string Group,
    string Gesture,
    Action Run,
    Func<bool>? IsAvailable = null);

/// <summary>
/// A searchable list of everything the window can do.
/// </summary>
/// <remarks>
/// Exists so that a new action does not need a new key or a new button. The shortcut list was
/// already the only place most of these were discoverable, and it cannot run anything.
/// </remarks>
public partial class CommandPalette : Window
{
    private readonly List<PaletteCommand> _commands;

    /// <summary>
    /// Whether a close is already under way.
    /// </summary>
    /// <remarks>
    /// Closing deactivates the window, which raises <c>Deactivated</c> while the close is still
    /// running. Calling <see cref="Window.Close"/> from there throws — WPF refuses a close during
    /// a close — so every way out goes through <see cref="Dismiss"/> and the second one is a no-op.
    /// </remarks>
    private bool _closing;

    private CommandPalette(IEnumerable<PaletteCommand> commands)
    {
        InitializeComponent();
        _commands = commands.Where(command => command.IsAvailable?.Invoke() ?? true).ToList();
        Apply("");
        Loaded += (_, _) => Query.Focus();
    }

    /// <summary>The palette currently on screen, so a second press does not stack another one.</summary>
    private static CommandPalette? _open;

    /// <summary>
    /// Opens the palette over a window.
    /// </summary>
    /// <remarks>
    /// Shown rather than shown modally. A modal palette blocks input to the window behind it, so
    /// clicking the editor to back out did nothing at all — the click never reached the main
    /// window and the palette never lost activation, which is what dismisses it. Modeless costs
    /// the guard below and nothing else: commands are handed back through their own action, so
    /// there is no result to wait for.
    /// </remarks>
    public static void Show(Window owner, IEnumerable<PaletteCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(commands);

        if (_open is { _closing: false } existing)
        {
            existing.Activate();
            existing.Query.Focus();
            return;
        }

        var palette = new CommandPalette(commands) { Owner = owner };
        _open = palette;
        palette.Closed += (_, _) =>
        {
            if (ReferenceEquals(_open, palette))
                _open = null;
        };
        palette.Show();
        palette.Activate();
    }

    /// <summary>
    /// Filters on every typed character being present in order, not as one run.
    /// </summary>
    /// <remarks>
    /// So "cop out" reaches "Copy output" without the user knowing the exact wording. A plain
    /// substring match would need the space to be in the right place, which defeats the point of
    /// typing three letters and pressing Enter.
    /// </remarks>
    private static bool Matches(string title, string query)
    {
        if (query.Length == 0)
            return true;

        // Scanned by hand rather than through IndexOf: there is no case-insensitive overload that
        // takes a char and a start, and turning each character into a string to get one would
        // allocate once per keystroke per command.
        int at = 0;
        foreach (char wanted in query)
        {
            if (wanted == ' ')
                continue;

            char target = char.ToLowerInvariant(wanted);
            while (at < title.Length && char.ToLowerInvariant(title[at]) != target)
                at++;
            if (at == title.Length)
                return false;
            at++;
        }
        return true;
    }

    private void Apply(string query)
    {
        PaletteCommand[] matching = _commands
            .Where(command => Matches(command.Title, query))
            .ToArray();

        Results.ItemsSource = matching;
        if (matching.Length > 0)
            Results.SelectedIndex = 0;

        Results.Visibility = matching.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoResults.Visibility = matching.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e) => Apply(Query.Text.Trim());

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Dismiss();
                return;

            // The list is driven from the box, which never loses the focus: taking the arrows here
            // is what lets a command be found and run without the hands leaving the keyboard.
            case Key.Down:
                Move(1);
                e.Handled = true;
                return;

            case Key.Up:
                Move(-1);
                e.Handled = true;
                return;

            case Key.Enter:
                e.Handled = true;
                Invoke();
                return;
        }
    }

    private void Move(int delta)
    {
        int count = Results.Items.Count;
        if (count == 0)
            return;

        int next = Results.SelectedIndex + delta;
        // Wrapping, because a list this short is faster to cycle than to reverse direction in.
        Results.SelectedIndex = (next % count + count) % count;
        Results.ScrollIntoView(Results.SelectedItem);
    }

    private void OnResultClicked(object sender, MouseButtonEventArgs e)
    {
        if (TreeLookup.Ancestor<ListBoxItem>(e.OriginalSource) is null)
            return;
        Invoke();
    }

    /// <summary>Closes first, then runs: a command that opens its own dialog must not be owned by this one.</summary>
    private void Invoke()
    {
        if (Results.SelectedItem is not PaletteCommand command)
            return;
        Dismiss();
        command.Run();
    }

    /// <summary>The one way out, so a close raised during a close is ignored rather than fatal.</summary>
    private void Dismiss()
    {
        if (_closing)
            return;
        _closing = true;
        Close();
    }

    /// <summary>Whether a palette is on screen and not already on its way out.</summary>
    public static bool IsOpen => _open is { _closing: false };

    /// <summary>
    /// Closes the palette from outside, before the click that asked for it is delivered.
    /// </summary>
    /// <remarks>
    /// Called by the main window while it is deciding what to do with an activating click. Doing
    /// it there rather than from <c>Deactivated</c> is what makes the dismissal ordered: waiting
    /// to be deactivated means racing the very press that deactivates us, and losing that race
    /// let the click through to whatever was underneath.
    /// </remarks>
    public static void DismissOpen() => _open?.Dismiss();

    /// <summary>
    /// Closes when focus goes elsewhere.
    /// </summary>
    /// <remarks>
    /// Only reached when another application takes the focus. A click on the window behind this
    /// one never activates it — the main window eats that message — so this is not the path that
    /// handles clicking back into the editor.
    /// </remarks>
    private void OnDeactivated(object? sender, EventArgs e) => Dismiss();
}
