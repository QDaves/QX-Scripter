using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Ui;

/// <summary>
/// One kind about to be offered: how many go out and at what price.
/// </summary>
public sealed class SellRow : INotifyPropertyChanged
{
    private int _amount;
    private int _price;

    public required string Name { get; init; }
    public required ItemType Type { get; init; }
    public required IReadOnlyList<Id> ItemIds { get; init; }
    public MarketplacePrice? Market { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Owned => ItemIds.Count;

    /// <summary>Whether the market gave a figure to work from.</summary>
    public bool IsPriced => Market?.IsKnown == true;

    public string MarketText => Market is null || !Market.IsKnown
        ? "no offers"
        : Market.IsCurrent
            ? $"{Market.CurrentPrice}c"
            : $"~{Market.AveragePrice}c";

    /// <summary>How many of the copies owned go out.</summary>
    public int Amount
    {
        get => _amount;
        set
        {
            _amount = Math.Clamp(value, 0, Owned);
            Raise(nameof(Amount));
        }
    }

    /// <summary>
    /// What each copy is offered at.
    /// </summary>
    /// <remarks>
    /// Never below the hotel's own floor. An offer of one is refused after it has been sent, which
    /// costs a round trip and leaves the seller wondering what happened; clamping here means what
    /// the dialog shows is what the hotel will take.
    /// </remarks>
    public int Price
    {
        get => _price;
        set
        {
            _price = Math.Max(SellDialog.MinimumPrice, value);
            Raise(nameof(Price));
        }
    }

    /// <summary>
    /// The copies going out, named the way the marketplace wants them.
    /// </summary>
    /// <remarks>
    /// A furni's number is negated while it sits in the inventory, and that is the number every
    /// inventory message uses — including the one that puts it into a trade. The marketplace is the
    /// exception: it is told the number without the sign, which is what the client itself sends.
    /// Converting here rather than where the rows are built keeps the row honest for everything else
    /// that reads it.
    /// </remarks>
    public IReadOnlyList<Id> Chosen =>
        [.. ItemIds.Take(Amount).Select(id => (Id)Math.Abs((long)id))];

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public partial class SellDialog : Window
{
    /// <summary>The least the hotel will take for an offer.</summary>
    public const int MinimumPrice = 2;

    /// <summary>
    /// How long one offer is waited on.
    /// </summary>
    /// <remarks>
    /// Shorter than the manager's own default because these are sent one after another: a dozen
    /// items against a ten second wait is two minutes of a window that appears to have hung.
    /// </remarks>
    private const int OfferTimeoutMs = 4000;

    /// <summary>The result code the hotel sends back when an offer was actually posted.</summary>
    private const int Accepted = 1;

    private readonly IApplicationRuntime _application;
    private readonly ObservableCollection<SellRow> _rows = [];
    private readonly UiTaskScope _ui_tasks;
    private int _refused;
    private bool _split_offers;
    private bool _stop;
    private bool _gone;

    public SellDialog(IApplicationRuntime application, IEnumerable<SellRow> rows)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(rows);
        _ui_tasks = new UiTaskScope(Dispatcher, "marketplace");
        InitializeComponent();
        _application = application;
        foreach (SellRow row in rows)
        {
            row.Amount = Math.Min(1, row.Owned);
            _rows.Add(row);
        }
        Rows.ItemsSource = _rows;
        ApplyPricing();
    }

    /// <summary>How many offers went out, once the dialog has closed with a sale.</summary>
    public int OffersMade { get; private set; }

    /// <summary>What went wrong, where anything did.</summary>
    public string? Failure { get; private set; }

    private void OnModeChanged(object sender, RoutedEventArgs e) => ApplyPricing();

    private void OnPricingChanged(object sender, TextChangedEventArgs e) => ApplyPricing();

    private void OnCellEdited(object? sender, DataGridCellEditEndingEventArgs e) =>
        _ui_tasks.Post(UpdateSummary, DispatcherPriority.Normal);

    private void DecreaseAmount(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: SellRow row })
            row.Amount--;
        UpdateSummary();
    }

    private void IncreaseAmount(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: SellRow row })
            row.Amount++;
        UpdateSummary();
    }

    /// <summary>
    /// Works out what each kind goes out at from the one choice made above the list.
    /// </summary>
    /// <remarks>
    /// A fixed price applies to everything, including kinds the market has never priced. The other
    /// two modes need a figure to work from, so kinds without one are left to the separate box that
    /// appears for them — otherwise selecting one untraded item alongside eleven traded ones would
    /// either block the sale or quietly send that one out at the floor price.
    /// </remarks>
    private void ApplyPricing()
    {
        if (!IsInitialized)
            return;

        int undercut = Read(UndercutBy.Text) ?? 0;
        int? fixed_price = Read(FixedPrice.Text);
        int? unpriced = Read(UnpricedPrice.Text);

        foreach (SellRow row in _rows)
        {
            if (ModeFixed.IsChecked == true)
            {
                if (fixed_price is int flat)
                    row.Price = flat;
                continue;
            }
            if (row.Market?.Suggested is not int market)
            {
                if (unpriced is int own)
                    row.Price = own;
                continue;
            }
            row.Price = ModeUndercut.IsChecked == true ? market - undercut : market;
        }

        int without = _rows.Count(row => !row.IsPriced);
        UnpricedRow.Visibility = without > 0 && ModeFixed.IsChecked != true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UnpricedNote.Text = without == 1
            ? "One kind has never been offered, so there is no price to work from. Set one here:"
            : $"{without} kinds have never been offered, so there is no price to work from. Set one here:";
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        SellRow[] going = [.. _rows.Where(row => row.Amount > 0 && row.Price >= MinimumPrice)];
        int items = going.Sum(row => row.Amount);
        long credits = going.Sum(row => (long)row.Amount * row.Price);
        int blocked = _rows.Count(row => row.Amount > 0 && row.Price < MinimumPrice);

        Summary.Text = items == 0
            ? "Nothing to sell."
            : blocked == 0
                ? $"{items} item{(items == 1 ? "" : "s")} across {going.Length} offer{(going.Length == 1 ? "" : "s")}, {credits:N0} credits if they all sell."
                : $"{items} item{(items == 1 ? "" : "s")}, {credits:N0} credits. {blocked} without a price and left out.";
        SellButton.IsEnabled = items > 0;
    }

    private static int? Read(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
            ? value
            : null;

    /// <summary>
    /// Sends the offers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One that fails does not stop the rest. A hotel refuses individual offers for reasons that
    /// belong to that item — already listed, no longer owned, over the price ceiling — and abandoning
    /// the other eleven because the third was refused would be worse than useless. What went wrong is
    /// kept and reported once at the end, with the count that did go out.
    /// </para>
    /// <para>
    /// Everything is caught. This runs from an event handler, so an exception nobody catches here
    /// does not surface as a failed sale but as the application falling over, and the one thing worth
    /// knowing — which offer failed and why — would be lost with it.
    /// </para>
    /// </remarks>
    private void OnSell(object sender, RoutedEventArgs e) => _ui_tasks.Observe(SellSafelyAsync);

    private async Task SellSafelyAsync()
    {
        try
        {
            await SellAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            Failure ??= error.Message;
            Finish(OffersMade > 0);
        }
    }

    private async Task SellAsync()
    {
        SellButton.IsEnabled = false;
        SellRow[] going = [.. _rows.Where(row => row.Amount > 0 && row.Price >= MinimumPrice)];
        int refused = 0;

        foreach (SellRow row in going)
        {
            MarketplaceSellCategory category = row.Type == ItemType.Wall
                ? MarketplaceSellCategory.Wall
                : MarketplaceSellCategory.Floor;

            // Every copy goes out in one message, which is what the client itself sends: a price, a
            // category, then the ids with their count in front. The hotel makes one offer per id at
            // that price, so five copies at ten is one message and five offers, not five messages.
            IReadOnlyList<Id> items = row.Chosen;
            if (!await OfferAsync(row, category, items).ConfigureAwait(true))
            {
                // A build on the older layout takes exactly one id per message. Splitting is the
                // same sale, just spelled the way that build understands.
                if (_split_offers && items.Count > 1)
                {
                    foreach (Id item in items)
                        await OfferAsync(row, category, [item]).ConfigureAwait(true);
                }
                if (_stop)
                {
                    Summarise(refused);
                    Finish(OffersMade > 0);
                    return;
                }
            }
            refused = _refused;
        }

        Summarise(refused);
        Finish(OffersMade > 0 || refused == 0);
    }

    /// <summary>
    /// Closes with an answer, unless there is no longer a dialog to answer for.
    /// </summary>
    /// <remarks>
    /// Selling runs across awaits, and the window can be gone before the last one returns — closed
    /// from the header, dismissed with escape, or closed by an earlier failure in this same run.
    /// Setting the result on a window that is no longer showing throws, and from an event handler
    /// that throw is not a failed sale but the application falling over. It has already done so
    /// once.
    /// </remarks>
    private void Finish(bool sold)
    {
        if (_gone)
            return;
        _gone = true;
        try
        {
            DialogResult = sold;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _gone = true;
        base.OnClosed(e);
    }

    /// <summary>
    /// Sends one offer and says whether it went out.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the message itself was refused before reaching the hotel, which
    /// is the only case worth retrying in another shape.
    /// </returns>
    private async Task<bool> OfferAsync(
        SellRow row,
        MarketplaceSellCategory category,
        IReadOnlyList<Id> items)
    {
        try
        {
            MarketplaceMakeOfferResult result = await _application
                .InvokeAsync<MarketplaceMakeOfferRequest, MarketplaceMakeOfferResult>(
                    ApplicationMemberIds.MarketplaceOfferMake,
                    new MarketplaceMakeOfferRequest(
                        row.Price,
                        category,
                        items,
                        OfferTimeoutMs))
                .ConfigureAwait(true);

            // The hotel answers every offer, refusals included. Counting a reply as a sale because
            // one arrived would report twelve listings for twelve refusals.
            if (result.Result == Accepted)
            {
                OffersMade += items.Count;
                return true;
            }
            _refused += items.Count;
            Failure ??= $"{row.Name}: the hotel refused it (code {result.Result}).";
            return true;
        }
        catch (NotSupportedException error)
        {
            // Either this build takes one id at a time, or its marketplace layout was never
            // identified. The first is worth retrying differently; the second never is.
            _split_offers = items.Count > 1;
            _stop = !_split_offers;
            _refused += items.Count;
            Failure ??= $"{row.Name}: {error.Message}";
            return false;
        }
        catch (Exception error)
        {
            _refused += items.Count;
            Failure ??= $"{row.Name}: {error.Message}";
            _stop = error is InvalidOperationException;
            return true;
        }
    }

    private void Summarise(int refused)
    {
        if (refused == 0 || Failure is null)
            return;
        Failure = OffersMade > 0
            ? $"{OffersMade} listed, {refused} refused. First: {Failure}"
            : $"None listed, {refused} refused. First: {Failure}";
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(false);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Finish(false);
            e.Handled = true;
        }
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
