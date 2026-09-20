using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Wardrobe;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Wardrobe;

public sealed partial class WardrobeViewModel : PageViewModel
{
    public const string AddCurrentTip = "Keep what you are wearing right now";
    public const string ImportTipText = "Copy the ten slots the hotel keeps into this shelf";
    public const string DeleteTip = "Delete the selected outfits";

    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    readonly IOutfitStore _outfits;
    readonly IImageService _images;
    readonly HotelContext _hotel;
    readonly IGameGateway _gateway;
    readonly IDialogService _dialogs;
    readonly IClipboardService _clipboard;
    readonly AppLifetime _lifetime;
    readonly KeyedRows<string, OutfitTileViewModel> _kept = new(StringComparer.OrdinalIgnoreCase);
    readonly List<OutfitTileViewModel> _ordered = [];
    readonly FilteredRows<OutfitTileViewModel> _rows = new();
    readonly Debouncer _search;

    public WardrobeViewModel(
        IOutfitStore outfits,
        IImageService images,
        HotelContext hotel,
        IGameGateway gateway,
        IDialogService dialogs,
        IClipboardService clipboard,
        AppLifetime lifetime,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Wardrobe)
    {
        _outfits = outfits ?? throw new ArgumentNullException(nameof(outfits));
        _images = images ?? throw new ArgumentNullException(nameof(images));
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        Notices = Own(new NoticeLine(dispatcher, time));
        _search = Own(new Debouncer(dispatcher, time, SearchDelay, ApplyFilter));
        _rows.Applied += OnRowsApplied;
        Selection.Changed += RefreshGates;
        _outfits.Changed += OnShelfChanged;
        gateway.SessionChanged += OnSessionChanged;
        Own(() =>
        {
            _rows.Applied -= OnRowsApplied;
            Selection.Changed -= RefreshGates;
            _outfits.Changed -= OnShelfChanged;
            gateway.SessionChanged -= OnSessionChanged;
        });
        Subtitle = WardrobeText.Shelf;
        CountText = WardrobeText.Counts(0, 0);
        RefreshGates();
        Rebuild();
        ApplyFilter();
    }

    public ResettableCollection<OutfitTileViewModel> Tiles => _rows.Visible;

    public SelectionList<OutfitTileViewModel> Selection { get; } = new();

    public NoticeLine Notices { get; }

    public int KeptCount => _ordered.Count;

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial OperationProgress? Progress { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(CopyFigureCommand))]
    public partial bool HasSelection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WearCommand), nameof(RenameCommand))]
    public partial bool HasOneSelected { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WearCommand))]
    public partial bool IsWearable { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyPropertyChangedFor(nameof(ImportTip))]
    public partial bool IsImportable { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    public partial bool IsImporting { get; private set; }

    [ObservableProperty]
    public partial string WearTip { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportTip))]
    public partial string ImportReason { get; private set; } = "";

    public string ImportTip => IsImportable ? ImportTipText : ImportReason;

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        ClearFilters();
        return true;
    }

    public void RefreshGates()
    {
        if (_lifetime.IsStopping)
            return;
        MemberGate wear = _gateway.Gate(ApplicationMemberIds.ProfileFigureSet);
        MemberGate import = _gateway.Gate(ApplicationMemberIds.ProfileWardrobeGet);
        HasSelection = Selection.HasAny;
        HasOneSelected = Selection.HasOne;
        IsWearable = wear.Available;
        WearTip = wear.Available ? "" : wear.Reason;
        IsImportable = import.Available;
        ImportReason = import.Reason;
    }

    protected override Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        RefreshGates();
        Rebuild();
        ApplyFilter();
        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string value) => _search.Trigger();

    protected override void OnDeactivated() => Selection.Select([]);

    [RelayCommand]
    void ClearFilters()
    {
        SearchText = "";
        _search.Cancel();
        ApplyFilter();
    }

    [RelayCommand]
    async Task AddCurrentAsync(CancellationToken cancellation_token)
    {
        try
        {
            ProfileStateView state = await _gateway.QueryAsync<ProfileStateRequest, ProfileStateView>(
                ApplicationMemberIds.ProfileState,
                new ProfileStateRequest(),
                cancellation_token);
            if (state.Identity is not { } me)
            {
                Notices.Show(NoticeSeverity.Warning, WardrobeText.NothingToCopy);
                return;
            }
            if (string.IsNullOrWhiteSpace(me.Figure))
            {
                Notices.Show(NoticeSeverity.Warning, WardrobeText.FigureUnknown);
                return;
            }
            bool kept = _outfits.Add(new SavedOutfit(me.Figure, WardrobeText.Gender(me.Gender.ToString())));
            Notices.Show(
                kept ? NoticeSeverity.Success : NoticeSeverity.Info,
                kept ? WardrobeText.KeptCurrent : WardrobeText.AlreadyKept);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not keep what you are wearing: {FailureText.Describe(error)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport), IncludeCancelCommand = true)]
    async Task ImportAsync(CancellationToken cancellation_token)
    {
        IsImporting = true;
        Progress = new OperationProgress(WardrobeText.AskingTheHotel, null, ImportCancelCommand);
        try
        {
            IReadOnlyList<WardrobeOutfit> offered = await WardrobeReader.ReadAsync(_gateway, cancellation_token);
            if (IsDisposed)
                return;
            int added = _outfits.AddRange(offered
                .Where(outfit => !string.IsNullOrWhiteSpace(outfit.Figure))
                .Select(outfit => new SavedOutfit(outfit.Figure, WardrobeText.Gender(outfit.Gender), WardrobeText.Slot(outfit.SlotId))));
            Notices.Show(
                added > 0 ? NoticeSeverity.Success : NoticeSeverity.Info,
                WardrobeText.Imported(added, offered.Count));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not read the wardrobe: {FailureText.Describe(error)}");
        }
        finally
        {
            IsImporting = false;
            Progress = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanWear))]
    async Task WearAsync(OutfitTileViewModel? tile, CancellationToken cancellation_token)
    {
        if ((tile ?? Selection.First) is not { } worn)
            return;
        try
        {
            await _gateway.InvokeAsync<ProfileFigureSetRequest, ProfileDispatchResult>(
                ApplicationMemberIds.ProfileFigureSet,
                new ProfileFigureSetRequest(worn.Gender, worn.Figure),
                cancellation_token);
            Notices.Show(NoticeSeverity.Success, WardrobeText.Worn(worn.Title));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not wear it: {FailureText.Describe(error)}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasOneSelected))]
    async Task RenameAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } tile)
            return;
        string? typed = await _dialogs.PromptAsync(
            new PromptRequest("Name this outfit", tile.Name, "Save", "Outfit name", IconKind.Rename, AllowEmpty: true),
            cancellation_token);
        if (typed is null || IsDisposed)
            return;
        _outfits.Rename(tile.Outfit, typed);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    async Task DeleteAsync(CancellationToken cancellation_token)
    {
        OutfitTileViewModel[] picked = [.. Selection.Items];
        if (picked.Length == 0)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            WardrobeText.DeleteTitle(picked.Length),
            WardrobeText.DeleteMessage(picked.Length, picked[0].Title),
            "Delete",
            DialogTone.Destructive,
            cancellation_token: cancellation_token);
        if (!confirmed || IsDisposed)
            return;
        int removed = _outfits.RemoveRange(picked.Select(tile => tile.Outfit));
        if (removed > 0)
            Notices.Show(NoticeSeverity.Success, WardrobeText.Deleted(removed));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    async Task CopyFigureAsync(CancellationToken cancellation_token)
    {
        string text = string.Join(Environment.NewLine, Selection.Items.Select(tile => tile.Figure));
        if (text.Length == 0)
            return;
        try
        {
            if (await _clipboard.TrySetTextAsync(text, cancellation_token))
            {
                Notices.Show(NoticeSeverity.Info, WardrobeText.FigureCopied);
                return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        Notices.Show(NoticeSeverity.Warning, WardrobeText.ClipboardUnreachable);
    }

    bool CanImport() => IsImportable && !IsImporting;

    bool CanWear(OutfitTileViewModel? tile) => IsWearable && (tile is not null || HasOneSelected);

    void Rebuild()
    {
        IReadOnlyList<SavedOutfit> kept = _outfits.Outfits;
        _kept.Sync(
            kept,
            outfit => outfit.Figure,
            outfit => new OutfitTileViewModel(outfit, _images, _hotel),
            (tile, outfit) => tile.Adopt(outfit));
        _ordered.Clear();
        foreach (SavedOutfit outfit in kept)
        {
            if (_kept.Find(outfit.Figure) is { } tile)
                _ordered.Add(tile);
        }
        OnPropertyChanged(nameof(KeptCount));
    }

    void ApplyFilter()
    {
        string term = SearchText.Trim();
        _rows.ApplyAsync(_ordered, tile => tile.Matches(term), null, ActivationToken).Observe("ui");
    }

    void OnRowsApplied()
    {
        CountText = WardrobeText.Counts(_ordered.Count, Tiles.Count);
        Selection.Prune(Tiles);
        ShowState();
    }

    void ShowState()
    {
        if (_lifetime.IsStopping)
            return;
        if (_ordered.Count == 0)
        {
            State.ShowEmpty(Descriptor.Icon, WardrobeText.NothingKept, IsImportable ? WardrobeText.AddOrImport : WardrobeText.ConnectFirst);
            return;
        }
        if (Tiles.Count == 0)
        {
            State.ShowEmpty(IconKind.Search, WardrobeText.NoMatches, "", ClearFiltersCommand, "Clear filters");
            return;
        }
        State.ShowReady();
    }

    void OnShelfChanged()
    {
        if (_lifetime.IsStopping)
            return;
        Rebuild();
        ApplyFilter();
    }

    void OnSessionChanged()
    {
        if (_lifetime.IsStopping)
            return;
        RefreshGates();
        foreach (OutfitTileViewModel tile in _ordered)
            tile.Rehost();
        ShowState();
    }

    static bool Reportable(Exception error) =>
        error is GameUnavailableException or InvalidOperationException or TimeoutException;
}
