namespace Qx.Presentation.Dialogs;

public interface IDialogService
{
    DialogViewModel? Current { get; }

    bool IsOpen { get; }

    Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog, CancellationToken cancellation_token = default);

    Task<bool> ConfirmAsync(string title, string message, string accept_text, DialogTone tone = DialogTone.Neutral, string? caption = null, CancellationToken cancellation_token = default);

    Task AlertAsync(string title, string message, CancellationToken cancellation_token = default);

    Task<string?> PromptAsync(PromptRequest request, CancellationToken cancellation_token = default);

    void DismissAll();
}
