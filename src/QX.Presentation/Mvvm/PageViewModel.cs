using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Diagnostics;
using Qx.Presentation.Navigation;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Mvvm;

public abstract partial class PageViewModel : ViewModelBase
{
    CancellationTokenSource? _activation;

    protected PageViewModel(PageKey key)
    {
        Descriptor = PageCatalog.For(key);
        Title = Descriptor.Title;
    }

    public PageDescriptor Descriptor { get; }

    public PageKey Key => Descriptor.Key;

    public IconKind Icon => Descriptor.Icon;

    public ViewState State { get; } = new();

    [ObservableProperty]
    public partial string Title { get; protected set; }

    [ObservableProperty]
    public partial string Subtitle { get; protected set; } = "";

    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    protected CancellationToken ActivationToken => _activation?.Token ?? CancellationToken.None;

    public virtual bool TryClearSearch() => false;

    internal void Activate()
    {
        if (IsActive)
            return;
        _activation = new CancellationTokenSource();
        IsActive = true;
        ActivateCoreAsync(_activation.Token).Observe("ui");
    }

    internal void Deactivate()
    {
        if (!IsActive)
            return;
        IsActive = false;
        CancellationTokenSource? activation = Interlocked.Exchange(ref _activation, null);
        activation?.Cancel();
        activation?.Dispose();
        OnDeactivated();
    }

    protected virtual Task OnActivatedAsync(CancellationToken cancellation_token) => Task.CompletedTask;

    protected virtual void OnDeactivated()
    {
    }

    protected override void OnDisposed() => Deactivate();

    async Task ActivateCoreAsync(CancellationToken cancellation_token)
    {
        try
        {
            await OnActivatedAsync(cancellation_token);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Diag.Warn($"{Title} could not load: {error.Message}", "ui");
            State.ShowError("Could not load this page", FailureText.Describe(error));
        }
    }
}
