using System.Windows;
using System.Windows.Controls;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Ui;

/// <summary>
/// The outfits you keep, on a shelf you own.
/// </summary>
/// <remarks>
/// <para>
/// The hotel's own wardrobe is ten slots and lives on its server; this is as many as you like and
/// lives on this machine. The in-game ten can be pulled in with one press, and what you are wearing
/// can be kept without visiting the clothes shop, which is the whole reason for the page.
/// </para>
/// <para>
/// Each tile is the whole figure rather than a head, because an outfit is mostly below the neck,
/// and it turns, because a figure seen only from the front hides half of what was chosen.
/// </para>
/// </remarks>
public partial class WardrobePage : GamePage
{
    private readonly OutfitStore _store = OutfitStore.Shared;
    private IReadOnlyList<OutfitTile> _tiles = [];

    public WardrobePage()
    {
        InitializeComponent();

        // The shelf is shared, so an outfit kept from a room or from the friend list lands here
        // while this page is already open. Without this it only appeared on the next restart.
        _store.Changed += OnShelfChanged;
        Unloaded += (_, _) => _store.Changed -= OnShelfChanged;
        Rebuild();
    }

    private void OnShelfChanged() => OnUi(() =>
    {
        Rebuild();
        Apply();
    });

    public override bool IsSearching => Filter.Text.Length > 0;

    public override void Opened()
    {
        Rebuild();
        Refresh();
    }

    public override void Refresh() => Apply();

    private void Rebuild() =>
        _tiles = [.. _store.Outfits.Select(outfit => new OutfitTile(outfit))];

    private void Apply()
    {
        string term = Filter.Text.Trim();
        List<OutfitTile> shown = term.Length == 0
            ? [.. _tiles]
            : [.. _tiles.Where(tile =>
                tile.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                tile.Figure.Contains(term, StringComparison.OrdinalIgnoreCase))];

        Rows.ItemsSource = shown;

        Subheading.Text = _tiles.Count == 0
            ? "Outfits you keep here, however many you like."
            : $"{_tiles.Count:N0} kept" + (shown.Count == _tiles.Count ? "" : $", {shown.Count:N0} shown");

        EmptyNotice.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = Game is null
            ? "Nothing kept yet. Connect, then add what you are wearing or pull in the hotel's ten slots."
            : "Nothing kept yet. Add what you are wearing, or import the hotel's ten slots.";

        RemoveButton.IsEnabled = Rows.SelectedItems.Count > 0;
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => Apply();

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RemoveButton.IsEnabled = Rows.SelectedItems.Count > 0;

    private OutfitTile[] Selection() => [.. Rows.SelectedItems.OfType<OutfitTile>()];

    private static OutfitTile? Sender(object sender) =>
        (sender as FrameworkElement)?.Tag as OutfitTile;

    private void TurnLeft(object sender, RoutedEventArgs e)
    {
        if (Sender(sender) is { } tile)
            tile.Turn(-1);
    }

    private void TurnRight(object sender, RoutedEventArgs e)
    {
        if (Sender(sender) is { } tile)
            tile.Turn(1);
    }

    private void WearOutfit(object sender, RoutedEventArgs e)
    {
        if (Sender(sender) is { } tile)
            Wear(tile);
    }

    private void WearSelected(object sender, RoutedEventArgs e)
    {
        if (Selection() is [OutfitTile only, ..])
            Wear(only);
    }

    /// <summary>
    /// Puts an outfit on.
    /// </summary>
    /// <remarks>
    /// The two clients write the gender differently — a character on Unity, a string on Flash —
    /// which is exactly the sort of thing the scripting path already knows, so the command goes out
    /// through the sender the window supplies rather than being written here.
    /// </remarks>
    private void Wear(OutfitTile tile)
    {
        if (Application is not { } application)
        {
            Status.Text = "Connect to wear an outfit.";
            return;
        }

        string gender = tile.Gender is { Length: > 0 } value ? value[..1].ToUpperInvariant() : "M";

        try
        {
            application.Invoke<ProfileFigureSetRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileFigureSet,
                new ProfileFigureSetRequest(gender, tile.Figure));

            Status.Text = $"Wearing {tile.Title}.";
        }
        catch (Exception error)
        {
            Status.Text = $"Could not wear it: {error.Message}";
        }
    }

    private void AddCurrentFigure(object sender, RoutedEventArgs e)
    {
        if (Application is not { } application ||
            application.Invoke<ProfileStateRequest, ProfileStateView>(
                ApplicationMemberIds.ProfileState,
                new ProfileStateRequest()).Identity is not { } me)
        {
            Status.Text = "Connect first — there is nothing to copy yet.";
            return;
        }

        if (string.IsNullOrWhiteSpace(me.Figure))
        {
            Status.Text = "The hotel has not said what you are wearing yet.";
            return;
        }

        string gender = me.Gender.ToString() is { Length: > 0 } value
            ? value[..1].ToUpperInvariant()
            : "M";

        Status.Text = _store.Add(new SavedOutfit(me.Figure, gender))
            ? "Kept what you are wearing."
            : "You are already keeping that one.";

        Rebuild();
        Apply();
    }

    /// <summary>
    /// Pulls the hotel's own ten slots onto this shelf.
    /// </summary>
    /// <remarks>
    /// Asked for rather than waited on: the wardrobe is only sent when the clothes shop is opened,
    /// so a session that never opened it has never seen one.
    /// </remarks>
    private void ImportWardrobe(object sender, RoutedEventArgs e) => Observe(ImportWardrobeAsync);

    private async Task ImportWardrobeAsync()
    {
        if (Application is not { } application)
        {
            Status.Text = "Connect to import the in-game wardrobe.";
            return;
        }

        Status.Text = "Asking the hotel for your wardrobe…";

        try
        {
            ProfileWardrobePage wardrobe = await ReadWardrobeAsync(application).ConfigureAwait(true);
            ProfileStateView profile = application.Invoke<ProfileStateRequest, ProfileStateView>(
                ApplicationMemberIds.ProfileState,
                new ProfileStateRequest());
            if (!profile.Connected ||
                profile.Client != wardrobe.Client ||
                profile.Generation != wardrobe.Generation)
                throw new InvalidOperationException("The hotel session changed while the wardrobe was loading.");

            int added = _store.AddRange(wardrobe.Outfits
                .Where(outfit => !string.IsNullOrWhiteSpace(outfit.Figure))
                .Select(outfit => new SavedOutfit(
                    outfit.Figure,
                    outfit.Gender is { Length: > 0 } gender ? gender[..1].ToUpperInvariant() : "M",
                    $"Slot {outfit.SlotId}")));

            Status.Text = added switch
            {
                0 when wardrobe.Outfits.Count == 0 => "The hotel's wardrobe is empty.",
                0 => "Nothing new — you are already keeping all of them.",
                1 => "Kept one outfit from the hotel.",
                _ => $"Kept {added} outfits from the hotel."
            };
        }
        catch (Exception error)
        {
            Status.Text = $"Could not read the wardrobe: {error.Message}";
            return;
        }

        Rebuild();
        Apply();
    }

    private static async Task<ProfileWardrobePage> ReadWardrobeAsync(
        IApplicationRuntime application)
    {
        ProfileWardrobePage first_page = await application
            .InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
                ApplicationMemberIds.ProfileWardrobeGet,
                new ProfileWardrobeRequest(Limit: 500))
            .ConfigureAwait(false);
        var outfits = new List<WardrobeOutfit>(first_page.Total);
        outfits.AddRange(first_page.Outfits);
        int? next_offset = first_page.NextOffset;

        while (next_offset is int offset)
        {
            ProfileWardrobePage page = await application
                .InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
                    ApplicationMemberIds.ProfileWardrobeGet,
                    new ProfileWardrobeRequest(
                        offset,
                        500,
                        SnapshotRevision: first_page.SnapshotRevision))
                .ConfigureAwait(false);
            if (page.Client != first_page.Client ||
                page.Generation != first_page.Generation ||
                page.Revision != first_page.Revision ||
                page.SnapshotRevision != first_page.SnapshotRevision ||
                page.State != first_page.State ||
                page.Total != first_page.Total ||
                page.Offset != offset ||
                page.NextOffset is int following && following <= offset)
            {
                throw new InvalidOperationException("The wardrobe snapshot changed while it was being read.");
            }

            outfits.AddRange(page.Outfits);
            next_offset = page.NextOffset;
        }

        if (outfits.Count != first_page.Total)
            throw new InvalidOperationException("The wardrobe returned an incomplete result.");
        return first_page with
        {
            Offset = 0,
            NextOffset = null,
            Outfits = Array.AsReadOnly(outfits.ToArray())
        };
    }

    private void RemoveSelected(object sender, RoutedEventArgs e)
    {
        OutfitTile[] picked = Selection();
        if (picked.Length == 0)
            return;

        int removed = _store.RemoveRange(picked.Select(tile => tile.Outfit));
        Status.Text = removed == 1 ? "Deleted one outfit." : $"Deleted {removed} outfits.";

        Rebuild();
        Apply();
    }

    private void CopyFigure(object sender, RoutedEventArgs e)
    {
        string figures = string.Join(Environment.NewLine, Selection().Select(tile => tile.Figure));
        if (figures.Length == 0)
            return;

        try
        {
            Clipboard.SetText(figures);
            Status.Text = "Figure copied.";
        }
        catch
        {
            // Another process can hold the clipboard open. Not worth interrupting anyone over.
        }
    }
}
