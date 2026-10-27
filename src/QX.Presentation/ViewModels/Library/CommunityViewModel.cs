using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.Library;

public sealed partial class CommunityViewModel : PageViewModel
{
    readonly CommunityClient _client;
    readonly CommunityCopies _copies;
    readonly IScriptFileCommands _files;
    readonly IScriptWorkspace _workspace;
    readonly IClipboardService _clipboard;
    readonly IDialogService _dialogs;
    readonly SerialOperation _listing = new();
    readonly SerialOperation _details = new();
    readonly Debouncer _search;
    int _page = 1;
    int _pages = 1;

    public CommunityViewModel(CommunityClient client, CommunityCopies copies, IScriptFileCommands files, IScriptWorkspace workspace,
        IClipboardService clipboard, IDialogService dialogs, IUiDispatcher dispatcher, TimeProvider time)
        : base(PageKey.Community)
    {
        _client = client;
        _copies = copies;
        _files = files;
        _workspace = workspace;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _search = Own(new Debouncer(dispatcher, time, TimeSpan.FromMilliseconds(350), () =>
        {
            _page = 1;
            BrowseAsync(ActivationToken).Observe("community");
        }));
    }

    public ObservableCollection<CommunityScript> Scripts { get; } = [];
    public IReadOnlyList<string> Categories { get; } = ["All", "Movement", "Chat", "Rooms", "Building", "Trading", "Wired", "Fun", "Utility"];

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string Category { get; set; } = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortIndex))]
    public partial bool NewestFirst { get; set; }

    public int SortIndex
    {
        get => NewestFirst ? 1 : 0;
        set => NewestFirst = value == 1;
    }

    [ObservableProperty]
    public partial bool IsInspecting { get; private set; }

    [ObservableProperty]
    public partial CommunityScript? SelectedScript { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SaveCopyCommand), nameof(CopyCodeCommand))]
    public partial CommunityScript? Detail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; } = "";

    [ObservableProperty]
    public partial string PageText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SaveCopyCommand), nameof(CopyCodeCommand))]
    public partial bool IsBusy { get; private set; }

    public bool HasDetail => Detail is not null;
    public bool HasMessage => Message.Length > 0;
    bool CanUse() => HasDetail && !IsBusy;

    partial void OnSearchTextChanged(string value) => _search.Trigger();
    partial void OnCategoryChanged(string value) => _search.Trigger();
    partial void OnNewestFirstChanged(bool value) => _search.Trigger();
    partial void OnSelectedScriptChanged(CommunityScript? value)
    {
        if (value is not null && IsInspecting)
            ReadAsync(value.Id, ActivationToken).Observe("community");
        else
        {
            _details.StopAsync().Observe("community");
            Detail = null;
        }
    }

    protected override Task OnActivatedAsync(CancellationToken cancellationToken) => BrowseAsync(cancellationToken);

    protected override void OnDeactivated() => _search.Cancel();

    [RelayCommand]
    async Task OpenAsync(CommunityScript? script, CancellationToken cancellation_token)
    {
        script ??= SelectedScript;
        if (script is null)
            return;
        SelectedScript = script;
        IsInspecting = true;
        await ReadAsync(script.Id, cancellation_token);
    }

    [RelayCommand]
    async Task BackAsync()
    {
        IsInspecting = false;
        Message = "";
        await _details.StopAsync();
    }

    public override bool TryReselect()
    {
        if (IsInspecting)
            BackCommand.Execute(null);
        return true;
    }

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        SearchText = "";
        return true;
    }

    [RelayCommand]
    Task RefreshAsync(CancellationToken cancellation_token) => BrowseAsync(cancellation_token);

    bool CanPrevious() => _page > 1;
    bool CanNext() => _page < _pages;

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    async Task PreviousAsync(CancellationToken cancellation_token)
    {
        if (_page <= 1)
            return;
        _page--;
        await BrowseAsync(cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    async Task NextAsync(CancellationToken cancellation_token)
    {
        if (_page >= _pages)
            return;
        _page++;
        await BrowseAsync(cancellation_token);
    }

    async Task BrowseAsync(CancellationToken cancellation_token)
    {
        OperationLease lease = await _listing.StartAsync(cancellation_token);
        try
        {
            State.ShowLoading("Loading community scripts…");
            CommunityPage page = await _client.BrowseAsync(SearchText, Category, NewestFirst, _page, lease.Token);
            if (!_listing.IsCurrent(lease))
                return;
            _page = page.Page;
            _pages = page.Pages;
            PreviousCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
            IsInspecting = false;
            Message = "";
            Scripts.Clear();
            foreach (CommunityScript script in page.Items)
                Scripts.Add(script);
            PageText = $"{page.Total} scripts · page {_page} of {_pages}";
            State.ShowReady();
            Detail = null;
            SelectedScript = Scripts.FirstOrDefault();
            if (Scripts.Count == 0)
                Message = "No scripts match these filters. New scripts appear after 73 hours; updates after 24 hours.";
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_listing.IsCurrent(lease))
                State.ShowError("Community unavailable", error.Message, RefreshCommand);
        }
        finally { _listing.Complete(lease); }
    }

    async Task ReadAsync(string id, CancellationToken cancellation_token)
    {
        OperationLease lease = await _details.StartAsync(cancellation_token);
        try
        {
            Detail = null;
            Message = "Loading script…";
            CommunityScript script = await _client.GetAsync(id, null, lease.Token);
            if (!_details.IsCurrent(lease))
                return;
            Detail = script;
            Message = "";
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_details.IsCurrent(lease))
                Message = error.Message;
        }
        finally { _details.Complete(lease); }
    }

    [RelayCommand(CanExecute = nameof(CanUse))]
    Task RunAsync(CancellationToken cancellation_token) => ImportAsync(true, cancellation_token);

    [RelayCommand(CanExecute = nameof(CanUse))]
    Task SaveCopyAsync(CancellationToken cancellation_token) => ImportAsync(false, cancellation_token);

    async Task ImportAsync(bool run, CancellationToken cancellation_token)
    {
        if (Detail is not { } selected)
            return;
        IsBusy = true;
        try
        {
            CommunityScript current = await _client.GetAsync(selected.Id, null, cancellation_token);
            if (current.Sha256 != selected.Sha256)
            {
                Detail = current;
                Message = "This script was updated. Review the latest code, then try again.";
                return;
            }
            if (run)
            {
                var document = _files.OpenCommunity(current);
                if (!document.HasUi)
                    document.Run.Start(null, false);
            }
            else
            {
                string path = await _copies.SaveAsync(current, cancellation_token, candidate => _workspace.FindByPath(candidate) is not { } open || open.Text == current.Code);
                Message = $"Saved to your library: {Path.GetFileName(path)}. Your copy will not be overwritten by updates.";
            }
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested) { }
        catch (Exception error) { await _dialogs.AlertAsync("Could not import script", error.Message, cancellation_token); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUse))]
    async Task CopyCodeAsync(CancellationToken cancellation_token)
    {
        if (Detail is { } script && await _clipboard.TrySetTextAsync(script.Code, cancellation_token))
            Message = "Code copied.";
    }

}
