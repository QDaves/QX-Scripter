namespace Qx.Presentation.Dialogs;

public interface IDialogService
{
    DialogViewModel? Current { get; }

    bool IsOpen { get; }

    Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(string title, string message, string acceptText, DialogTone tone = DialogTone.Neutral, string? caption = null, CancellationToken cancellationToken = default);

    Task AlertAsync(string title, string message, CancellationToken cancellationToken = default);

    Task<string?> PromptAsync(PromptRequest request, CancellationToken cancellationToken = default);

    void DismissAll();
}
