using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;

namespace Qx.Ui;

/// <summary>
/// Everything in your hand: furni, and the pets that came with it.
/// </summary>
/// <remarks>
/// Two views of the same thing. The list is a table — sortable by name, by identifier, by how many
/// you own — because that is how you find one particular thing. The grid is icons, because that is
/// how you see what you have at all.
/// </remarks>
public partial class InventoryPage : GamePage
{
    private IReadOnlyList<GameRow> _items = [];

    /// <summary>Stops the filters from running against half-built controls during construction.</summary>
    private bool _ready;

    /// <summary>Drops the running price scan when the inventory underneath it changes.</summary>
    private CancellationTokenSource? _scan;

    /// <summary>
    /// How long the inventory has to hold still before the marketplace is asked anything.
    /// </summary>
    /// <remarks>
    /// Every item picked up is a change, and each one rebuilds the rows. Restarting the scan on each
    /// of them means a handful of items in quick succession cancels the scan over and over and it
    /// never reaches the end. Waiting for the last one costs a moment and finishes.
    /// </remarks>
    private readonly System.Windows.Threading.DispatcherTimer _scan_delay = new()
    {
        Interval = TimeSpan.FromMilliseconds(750)
    };

    private int _asked;
    private int _toAsk;
    private int _shown;
    private IDisposable? _furni_changes;
    private IDisposable? _pet_changes;
    private long _subscription_generation;
    private long _pending_refresh_generation;

    public InventoryPage()
    {
        InitializeComponent();
        _scan_delay.Tick += BeginPriceScan;
        _ready = true;
    }

    public override bool IsSearching => Filter.Text.Length > 0;

    protected override void AttachApplication(IApplicationRuntime application)
    {
        long generation = Interlocked.Increment(ref _subscription_generation);
        IDisposable furni_changes = application.Subscribe<InventoryFurniChanged>(
            ApplicationMemberIds.InventoryFurniChanged,
            _ => QueueRefresh(generation));
        try
        {
            _pet_changes = application.Subscribe<InventoryPetChanged>(
                ApplicationMemberIds.InventoryPetsChanged,
                _ => QueueRefresh(generation));
            _furni_changes = furni_changes;
        }
        catch
        {
            furni_changes.Dispose();
            throw;
        }
    }

    protected override void DetachApplication(IApplicationRuntime application)
    {
        Interlocked.Increment(ref _subscription_generation);
        Interlocked.Exchange(ref _pending_refresh_generation, 0);
        _furni_changes?.Dispose();
        _pet_changes?.Dispose();
        _furni_changes = null;
        _pet_changes = null;
    }

    private void QueueRefresh(long generation)
    {
        if (Volatile.Read(ref _subscription_generation) != generation ||
            Interlocked.CompareExchange(
                ref _pending_refresh_generation,
                generation,
                0) != 0)
        {
            return;
        }

        PostOnUi(() =>
        {
            if (Interlocked.CompareExchange(
                    ref _pending_refresh_generation,
                    0,
                    generation) != generation ||
                Volatile.Read(ref _subscription_generation) != generation ||
                Visibility != Visibility.Visible)
            {
                return;
            }
            Refresh();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Asks for the inventory instead of telling you to open it in the game.
    /// </summary>
    /// <remarks>
    /// Furni and pets are two separate lists on the wire and are asked for together, because nobody
    /// thinks of their pets as living somewhere other than their inventory.
    /// </remarks>
    protected override async Task FetchAsync()
    {
        if (Application is not { } application)
            return;

        InventoryStateView state = await application
            .InvokeAsync<InventoryStateRequest, InventoryStateView>(
                ApplicationMemberIds.InventoryState,
                new InventoryStateRequest())
            .ConfigureAwait(true);
        var loads = new List<Task>(2);
        if (!state.Furni.Loaded || state.Furni.Stale || state.Furni.RecoveryPending)
        {
            loads.Add(application
                .InvokeAsync<InventoryFurniRefreshRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniRefresh,
                    new InventoryFurniRefreshRequest(Limit: 500))
                .AsTask());
        }
        if (!state.Pets.Loaded || state.Pets.Stale || state.Pets.RecoveryPending)
        {
            loads.Add(application
                .InvokeAsync<InventoryPetRefreshRequest, InventoryPetPage>(
                    ApplicationMemberIds.InventoryPetsRefresh,
                    new InventoryPetRefreshRequest(Limit: 500))
                .AsTask());
        }
        await Task.WhenAll(loads).ConfigureAwait(true);
    }

    protected override void Fetching(string? message)
    {
        if (message is { Length: > 0 })
            Status.Text = message;
    }

    public override void Refresh()
    {
        if (Game is null || Application is not { } application)
        {
            ShowUnavailable("Connect to see your inventory.");
            return;
        }

        InventoryFurniPage inventory;
        InventoryPetPage pet_inventory;
        try
        {
            inventory = InventoryApplicationPages.ReadFurni(application);
            pet_inventory = InventoryApplicationPages.ReadPets(application);
        }
        catch (InvalidOperationException)
        {
            ShowUnavailable("The inventory changed. Reload it before using these items.");
            return;
        }
        if (!inventory.Connected ||
            !pet_inventory.Connected ||
            inventory.SessionGeneration != pet_inventory.SessionGeneration ||
            inventory.Client != pet_inventory.Client ||
            inventory.Revision != pet_inventory.Revision ||
            inventory.Stale ||
            pet_inventory.Stale ||
            inventory.RecoveryPending ||
            pet_inventory.RecoveryPending)
        {
            ShowUnavailable(inventory.Connected && pet_inventory.Connected
                ? "The inventory changed. Reload it before using these items."
                : "Connect to see your inventory.");
            return;
        }
        FurniData? data = Game.GameData.Furni;

        // Folded by kind. An inventory of four hundred separate rows saying the same three words is
        // a list nobody reads; what anyone wants from it is what they own and how many.
        List<GameRow> furni =
        [
            .. inventory.Items
                .GroupBy(item => (Type: ParseItemType(item.Type), item.Kind))
                .Select(group =>
                {
                    InventoryItemSnapshot first = group.First();
                    FurniInfo? info = data?.GetInfo(group.Key.Type, first.Kind);
                    return new GameRow(HabboImages.FurniIconUrl(info?.Revision ?? 0, info?.Identifier))
                    {
                        Name = info?.Name is { Length: > 0 } name ? name : $"Furni {first.Kind}",
                        Detail = info?.Identifier ?? "",
                        Group = group.Key.Type == ItemType.Wall ? "wall" : "floor",
                        Count = group.Count(),
                        Trailing = group.Count() > 1 ? $"×{group.Count()}" : "",
                        Fallback = PackIconKind.SofaSingleOutline,
                        Key = first.ItemId,
                        Identifier = info?.Identifier ?? "",
                        ItemKind = group.Key.Type,
                        // The item says whether it may be sold; the furni data only says what the
                        // kind is normally like. A copy that is rented, expiring or otherwise
                        // pinned to this account carries that on itself, and the kind knows
                        // nothing about it.
                        Tradeable = group.Any(item => item.IsTradeable && item.IsSellable),
                        ItemIds =
                        [
                            .. group
                                .Where(item => item.IsTradeable && item.IsSellable)
                                .Select(item => item.ItemId)
                        ]
                    };
                })
                .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
        ];

        List<GameRow> pets =
        [
            .. pet_inventory.Pets
                .OrderBy(pet => pet.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(pet => new GameRow
                {
                    Name = pet.Name,
                    Detail = $"breed {pet.BreedId}, level {pet.Level}",
                    Group = "pet",
                    Fallback = PackIconKind.Paw,
                    Key = pet.Id
                })
        ];

        _items = [.. furni, .. pets];
        StartPriceScan();

        int total = inventory.Total;
        Subheading.Text = _items.Count == 0
            ? ""
            : $"{total:N0} {(total == 1 ? "item" : "items")} in {furni.Count:N0} " +
              $"{(furni.Count == 1 ? "kind" : "kinds")}" +
              (pets.Count > 0 ? $" · {pets.Count:N0} {(pets.Count == 1 ? "pet" : "pets")}" : "");

        EmptyNotice.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_items.Count == 0)
        {
            EmptyText.Text = inventory.Loaded
                ? "Your inventory is empty."
                : "Nothing yet. Reload to ask the hotel for it.";
        }

        Apply();
    }

    private void ShowUnavailable(string message)
    {
        _items = [];
        StartPriceScan();
        EmptyNotice.Visibility = Visibility.Visible;
        EmptyText.Text = message;
        Subheading.Text = "";
        Apply();
    }

    private static ItemType ParseItemType(string value) =>
        Enum.TryParse(value, false, out ItemType item_type) &&
        item_type is Qx.Model.ItemType.Floor or Qx.Model.ItemType.Wall
            ? item_type
            : throw new System.IO.InvalidDataException(
                $"Unsupported inventory item type '{value}'.");

    private void Apply()
    {
        if (!_ready)
            return;

        string term = Filter.Text.Trim();
        int? least_owned = Number(LeastOwned);
        int? least_price = Number(LeastPrice);
        int? most_price = Number(MostPrice);
        bool by_price = least_price is not null || most_price is not null;

        List<GameRow> rows =
        [
            .. _items
                .Select(row => (Row: row, Rank: FurniSearch.Rank(row.Name, row.Detail, row.Group, term)))
                .Where(entry =>
                    entry.Rank is not null &&
                    MatchesKind(entry.Row) &&
                    MatchesTrade(entry.Row) &&
                    (least_owned is not { } fewest || entry.Row.Count >= fewest) &&
                    (!by_price || Priced(entry.Row, least_price, most_price)))
                .OrderBy(entry => term.Length == 0 ? 0 : entry.Rank)
                .ThenBy(entry => entry.Row.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => entry.Row)
        ];

        Rows.ItemsSource = rows;
        Tiles.ItemsSource = rows;

        _shown = rows.Count;
        ClearFilters.Visibility = Filtering ? Visibility.Visible : Visibility.Collapsed;
        ShowStatus();
        StartPriceScan();
    }

    /// <summary>Whether anything is being held back, which is what the clear button is for.</summary>
    private bool Filtering =>
        Filter.Text.Length > 0 || !KindAny.IsChecked!.Value || !TradeAny.IsChecked!.Value ||
        LeastOwned.Text.Length > 0 || LeastPrice.Text.Length > 0 || MostPrice.Text.Length > 0;

    private bool MatchesKind(GameRow row) =>
        KindAny.IsChecked == true ||
        (KindFloor.IsChecked == true && row.ItemKind == ItemType.Floor) ||
        (KindWall.IsChecked == true && row.ItemKind == ItemType.Wall) ||
        (KindPet.IsChecked == true && row.ItemKind is null);

    /// <summary>
    /// Whether the row is on the side of the trade filter that is being shown.
    /// </summary>
    /// <remarks>
    /// A pet counts as locked. It cannot be offered and cannot be handed over, so asking for what
    /// may not change hands and getting furniture only would be a wrong answer to the question.
    /// </remarks>
    private bool MatchesTrade(GameRow row) =>
        TradeAny.IsChecked == true ||
        (TradeOpen.IsChecked == true ? row.Tradeable : !row.Tradeable);

    /// <summary>
    /// Whether the market puts the row inside the bounds asked for.
    /// </summary>
    /// <remarks>
    /// A kind with no price is out. Filtering by price is a question about the market, and a kind
    /// the market has never carried has no answer to it — passing those through would fill a search
    /// for cheap furniture with everything that cannot be sold at all.
    /// </remarks>
    private static bool Priced(GameRow row, int? least, int? most) =>
        row.Market?.Suggested is { } price &&
        (least is not { } floor || price >= floor) &&
        (most is not { } ceiling || price <= ceiling);

    /// <summary>What a number box says, where it says a usable number.</summary>
    private static int? Number(TextBox box) =>
        int.TryParse(box.Text.Trim(), out int value) && value > 0 ? value : null;

    private void ShowStatus()
    {
        string counted = _shown == _items.Count
            ? $"{_shown:N0} shown"
            : $"{_shown:N0} of {_items.Count:N0} shown";

        // While the scan is running the price column and the price filter are both still filling in.
        // Saying so is the difference between a filter that looks slow and one that looks broken.
        Status.Text = _asked < _toAsk
            ? $"{counted} · reading prices, {_asked:N0} of {_toAsk:N0} kinds"
            : counted;
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => Apply();

    private void FilterPicked(object sender, RoutedEventArgs e) => Apply();

    private void ResetFilters(object sender, RoutedEventArgs e)
    {
        _ready = false;
        Filter.Text = "";
        KindAny.IsChecked = true;
        TradeAny.IsChecked = true;
        LeastOwned.Text = "";
        LeastPrice.Text = "";
        MostPrice.Text = "";
        _ready = true;
        Apply();
    }

    /// <summary>
    /// Puts the row under the pointer into the selection before its menu opens.
    /// </summary>
    /// <remarks>
    /// A right click on its own selects nothing, so the menu would be about whatever was selected
    /// last — which is how "sell this" ends up offering something else entirely. A row already in
    /// the selection is left alone, so right-clicking one of several chosen items still means all
    /// of them.
    /// </remarks>
    private void RightClickPicks(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? node = e.OriginalSource as DependencyObject;
        while (node is not null and not DataGridRow and not ListBoxItem)
        {
            // Text inside a cell is not a visual, so the walk falls back to the logical tree rather
            // than throwing on the first run of characters it meets.
            node = node is Visual
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        switch (node)
        {
            case DataGridRow { IsSelected: false } row:
                Rows.SelectedItem = row.Item;
                break;
            case ListBoxItem { IsSelected: false } tile:
                Tiles.SelectedItem = tile.DataContext;
                break;
        }
    }

    private void ShowList(object sender, RoutedEventArgs e)
    {
        ListToggle.IsChecked = true;
        GridToggle.IsChecked = false;
        Rows.Visibility = Visibility.Visible;
        Tiles.Visibility = Visibility.Collapsed;
        StartPriceScan();
    }

    private void ShowGrid(object sender, RoutedEventArgs e)
    {
        ListToggle.IsChecked = false;
        GridToggle.IsChecked = true;
        Rows.Visibility = Visibility.Collapsed;
        Tiles.Visibility = Visibility.Visible;
        StartPriceScan();
    }

    private void Reload(object sender, RoutedEventArgs e) => Observe(ReloadAsync);

    private async Task ReloadAsync()
    {
        if (Application is null)
            return;

        Status.Text = "Asking the hotel…";
        try
        {
            await FetchAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            Status.Text = $"Could not read the inventory: {error.Message}";
            return;
        }

        Refresh();
    }

    private GameRow[] Selection() =>
        Tiles.Visibility == Visibility.Visible
            ? [.. Tiles.SelectedItems.OfType<GameRow>()]
            : [.. Rows.SelectedItems.OfType<GameRow>()];

    private void CopyIdentifier(object sender, RoutedEventArgs e) =>
        Copy(Selection().Select(row => row.Detail).Where(text => text.Length > 0));

    private void CopyItemId(object sender, RoutedEventArgs e) =>
        Copy(Selection().Select(row => row.KeyText));

    private void Copy(IEnumerable<string> values)
    {
        string text = string.Join(Environment.NewLine, values);
        if (text.Length == 0)
            return;

        try
        {
            Clipboard.SetText(text);
            Status.Text = "Copied.";
        }
        catch
        {
            // Another process can hold the clipboard open. Not worth interrupting anyone over.
        }
    }

    /// <summary>
    /// Reads what the marketplace is asking for every kind in the inventory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only kinds that could actually be sold are asked about: a pet has no class name to ask under
    /// and furniture the hotel has marked untradeable will never carry an offer, so asking would
    /// spend a slot in the batch on a question with no answer.
    /// </para>
    /// <para>
    /// The scan is dropped and started again whenever the inventory changes underneath it. Prices
    /// already read are held for a while, so a restart costs almost nothing — the kinds that were
    /// answered are answered again from what is held and only what is genuinely new is asked for.
    /// </para>
    /// </remarks>
    private void StartPriceScan()
    {
        // Whatever was read earlier is put back at once, without waiting and without asking, so
        // returning to the page shows the prices it was showing rather than blanking and filling in
        // again.
        foreach (GameRow row in _items)
        {
            if (row.CanSell &&
                MarketplacePrices.Known(row.ItemKind!.Value, row.Identifier) is { } held)
            {
                row.Market = held;
            }
        }

        _scan_delay.Stop();
        _scan_delay.Start();
    }

    private void BeginPriceScan(object? sender, EventArgs e)
    {
        _scan_delay.Stop();
        _scan?.Cancel();
        _scan?.Dispose();

        var scan = new CancellationTokenSource();
        _scan = scan;
        IReadOnlyList<GameRow> visible = Rows.IsVisible
            ? VisibleItems.Rows<GameRow>(Rows)
            : VisibleItems.Tiles<GameRow>(Tiles);
        if (visible.Count == 0)
        {
            visible = Rows.IsVisible
                ? [.. Rows.Items.OfType<GameRow>().Take(MarketplacePrices.BatchSize)]
                : [.. Tiles.Items.OfType<GameRow>().Take(MarketplacePrices.BatchSize)];
        }
        Observe(() => ScanPricesAsync(visible, scan.Token));
    }

    private async Task ScanPricesAsync(IReadOnlyList<GameRow> rows, CancellationToken token)
    {
        (ItemType Type, string Identifier)[] kinds =
        [
            .. rows
                .Where(row => row.CanSell)
                .Select(row => (row.ItemKind!.Value, row.Identifier))
                .Distinct()
        ];

        _asked = 0;
        _toAsk = kinds.Length;
        ShowStatus();
        if (kinds.Length == 0)
            return;

        try
        {
            for (int start = 0; start < kinds.Length; start += MarketplacePrices.BatchSize)
            {
                (ItemType Type, string Identifier)[] batch =
                    [.. kinds.Skip(start).Take(MarketplacePrices.BatchSize)];

                IReadOnlyDictionary<(ItemType Type, string Identifier), MarketplacePrice> prices =
                    await MarketplacePrices.FetchAsync(batch, token).ConfigureAwait(true);
                if (token.IsCancellationRequested)
                    return;

                Record(rows, batch, prices);
                _asked += batch.Length;

                if (start + MarketplacePrices.BatchSize < kinds.Length)
                    await Task.Delay(150, token).ConfigureAwait(true);

                // Rebuilding the list mid-scan moves rows under the pointer, so it is only done
                // where a price bound is set and the answers are what decides who is on the list at
                // all. Otherwise the rows update themselves in place and only the count is redrawn.
                if (Number(LeastPrice) is not null || Number(MostPrice) is not null)
                    Apply();
                else
                    ShowStatus();
            }
        }
        catch (OperationCanceledException)
        {
            // The inventory changed and a fresh scan took over. Nothing to report.
            return;
        }
        catch (Exception error)
        {
            // Said rather than swallowed. A price column that quietly stopped filling in halfway
            // looks like the rest of the inventory has no price rather than like a failure.
            _toAsk = _asked;
            ShowStatus();
            Status.Text += $" · prices stopped at {_asked:N0}: {error.Message}";
            return;
        }

        _toAsk = _asked;
        ShowStatus();
    }

    private void PricesScrolled(object sender, ScrollChangedEventArgs e) => StartPriceScan();

    /// <summary>
    /// Puts a batch of answers onto the rows that asked for them.
    /// </summary>
    /// <remarks>
    /// A kind that came back with nothing is marked as asked rather than left as it was, so its cell
    /// reads "no offers" instead of staying blank. A blank cell is how a kind still being read looks,
    /// and the two should not look the same.
    /// </remarks>
    private static void Record(
        IReadOnlyList<GameRow> rows,
        (ItemType Type, string Identifier)[] batch,
        IReadOnlyDictionary<(ItemType Type, string Identifier), MarketplacePrice> prices)
    {
        var asked = batch.ToHashSet();

        foreach (GameRow row in rows)
        {
            if (!row.CanSell)
                continue;

            (ItemType, string) kind = (row.ItemKind!.Value, row.Identifier);
            if (!asked.Contains(kind))
                continue;

            if (prices.TryGetValue(kind, out MarketplacePrice? price))
                row.Market = price;
            else if (MarketplacePrices.WasRead(kind.Item1, kind.Item2))
                row.Market = new MarketplacePrice(row.ItemKind!.Value, row.Identifier, null, null, 0, 0);
        }
    }


    /// <summary>
    /// Offers the selected kinds on the marketplace.
    /// </summary>
    /// <remarks>
    /// Only what can actually be offered is carried in. A pet, a bot and anything the hotel marked
    /// untradeable are dropped here rather than shown in the dialog greyed out, because a list of
    /// things that cannot be done is not a choice. Selecting nothing but those says so instead of
    /// opening an empty dialog.
    /// </remarks>
    private void SellSelected(object sender, RoutedEventArgs e)
    {
        if (Game is null || Application is not { } application)
        {
            Status.Text = "Connect before listing marketplace offers.";
            return;
        }

        GameRow[] chosen = [.. Selection().Where(row => row.CanSell && row.ItemIds.Count > 0)];
        if (chosen.Length == 0)
        {
            Status.Text = "Nothing there can be sold on the marketplace.";
            return;
        }

        var dialog = new SellDialog(
            application,
            chosen.Select(row => new SellRow
            {
                Name = row.Name,
                Type = row.ItemKind!.Value,
                ItemIds = row.ItemIds,
                Market = row.Market ?? MarketplacePrices.Known(row.ItemKind!.Value, row.Identifier)
            }))
        {
            Owner = Window.GetWindow(this)
        };

        bool? sold = dialog.ShowDialog();
        if (dialog.Failure is { Length: > 0 } failure)
        {
            Status.Text = dialog.OffersMade > 0
                ? $"{dialog.OffersMade} offered, then it stopped: {failure}"
                : $"Nothing was offered: {failure}";
            return;
        }
        if (sold == true)
            Status.Text = $"{dialog.OffersMade} offer{(dialog.OffersMade == 1 ? "" : "s")} made.";
    }

    /// <summary>
    /// Hides the marketplace entry when nothing chosen could be offered.
    /// </summary>
    /// <remarks>
    /// A pet, a bot and anything the hotel will not let change hands can never be listed. Showing
    /// the entry anyway and answering the click with an apology is worse than not offering it: the
    /// menu should say what can be done, not what cannot.
    /// </remarks>
    private void MenuOpened(object sender, RoutedEventArgs e)
    {
        bool any = Selection().Any(row => row.CanSell && row.ItemIds.Count > 0);
        if (sender is not ContextMenu menu)
            return;

        foreach (object entry in menu.Items)
        {
            if (entry is MenuItem { Name: "SellItem" or "SellTile" } item)
                item.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            if (entry is Separator separator)
                separator.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
