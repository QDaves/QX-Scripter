using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Model;
using Qx.Presentation.Services.Marketplace;

namespace Qx.Presentation.ViewModels.Inventory;

public sealed record SellCandidate(string Name, ItemType Type, string Identifier, IReadOnlyList<Id> ItemIds, MarketplacePrice? Market);

public sealed partial class SellRowViewModel : ObservableObject
{
    readonly SellCandidate _candidate;
    bool _writing;
    int _committed;

    public SellRowViewModel(SellCandidate candidate)
    {
        _candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        Amount = Math.Min(1, Owned);
    }

    public string Name => _candidate.Name;

    public ItemType Type => _candidate.Type;

    public IReadOnlyList<Id> ItemIds => _candidate.ItemIds;

    public MarketplacePrice? Market => _candidate.Market;

    public int Owned => _candidate.ItemIds.Count;

    public string OwnedText => Owned.ToString("N0", CultureInfo.CurrentCulture);

    public bool IsPriced => Market?.IsKnown == true;

    public string MarketText => Market?.ShortText ?? "no offers";

    public IReadOnlyList<Id> Chosen => [.. ItemIds.Take(Amount).Select(id => (Id)Math.Abs((long)id))];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand), nameof(DecreaseCommand))]
    public partial int Amount { get; private set; }

    [ObservableProperty]
    public partial int Price { get; private set; }

    [ObservableProperty]
    public partial string PriceText { get; set; } = "";

    public void Apply(int price)
    {
        int wanted = Math.Max(SellDialogViewModel.MinimumPrice, price);
        _committed = wanted;
        Price = wanted;
        _writing = true;
        PriceText = wanted.ToString(CultureInfo.CurrentCulture);
        _writing = false;
    }

    public void Commit() => Apply(Price);

    public void Revert() => Apply(_committed);

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    void Increase() => Amount = Math.Min(Owned, Amount + 1);

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    void Decrease() => Amount = Math.Max(0, Amount - 1);

    bool CanIncrease() => Amount < Owned;

    bool CanDecrease() => Amount > 0;

    partial void OnPriceTextChanged(string value)
    {
        if (_writing)
            return;
        Price = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int typed) ? typed : 0;
    }
}
