using CommunityToolkit.Mvvm.ComponentModel;

namespace Qx.Presentation.Dialogs;

public sealed partial class DialogService : ObservableObject, IDialogService
{
    readonly Queue<DialogViewModel> _waiting = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial DialogViewModel? Current { get; private set; }

    public bool IsOpen => Current is not null;

    public async Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        using CancellationTokenRegistration registration = cancellation_token.Register(dialog.Dismiss);
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

    public Task<bool> ConfirmAsync(string title, string message, string accept_text, DialogTone tone = DialogTone.Neutral, string? caption = null, CancellationToken cancellation_token = default) =>
        ShowAsync(new ConfirmDialogViewModel(title, message, accept_text, tone, caption), cancellation_token);

    public Task AlertAsync(string title, string message, CancellationToken cancellation_token = default) =>
        ShowAsync(new AlertDialogViewModel(title, message), cancellation_token);

    public Task<string?> PromptAsync(PromptRequest request, CancellationToken cancellation_token = default) =>
        ShowAsync(new PromptDialogViewModel(request), cancellation_token);

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
