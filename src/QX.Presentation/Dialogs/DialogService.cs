using CommunityToolkit.Mvvm.ComponentModel;

namespace Qx.Presentation.Dialogs;

public sealed partial class DialogService : ObservableObject, IDialogService
{
    readonly Queue<DialogViewModel> _waiting = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial DialogViewModel? Current { get; private set; }

    public bool IsOpen => Current is not null;

    public async Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        using CancellationTokenRegistration registration = cancellationToken.Register(dialog.Dismiss);
        if (Current is null)
            Current = dialog;
        else
            _waiting.Enqueue(dialog);
        try
        {
            return await dialog.Result.WaitAsync(CancellationToken.None);
        }
        finally
        {
            if (ReferenceEquals(Current, dialog))
                Advance();
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string acceptText, DialogTone tone = DialogTone.Neutral, string? caption = null, CancellationToken cancellationToken = default) =>
        ShowAsync(new ConfirmDialogViewModel(title, message, acceptText, tone, caption), cancellationToken);

    public Task AlertAsync(string title, string message, CancellationToken cancellationToken = default) =>
        ShowAsync(new AlertDialogViewModel(title, message), cancellationToken);

    public Task<string?> PromptAsync(PromptRequest request, CancellationToken cancellationToken = default) =>
        ShowAsync(new PromptDialogViewModel(request), cancellationToken);

    public void DismissAll()
    {
        foreach (DialogViewModel waiting in _waiting.ToArray())
            waiting.Dismiss();
        Current?.Dismiss();
    }

    void Advance()
    {
        while (_waiting.TryDequeue(out DialogViewModel? next))
        {
            if (next.IsClosed)
                continue;
            Current = next;
            return;
        }
        Current = null;
    }
}
