using Qx.Presentation.Dialogs;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Output;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Runs;

public interface IScriptPrompts
{
    Task<bool> ConfirmAsync(ScriptDocument document, long run_epoch, string title, string message, CancellationToken run_token);

    Task<string?> PromptAsync(ScriptDocument document, long run_epoch, string title, string initial, CancellationToken run_token);

    Task DownloadAsync(ScriptDocument document, long run_epoch, string file_name, string content, CancellationToken run_token);

    void CloseAll();
}

public sealed class ScriptPrompts : IScriptPrompts, IDisposable
{
    readonly IDialogService _dialogs;
    readonly IFilePickerService _files;
    readonly IUiDispatcher _dispatcher;
    readonly CancellationTokenSource _closing = new();

    public ScriptPrompts(IDialogService dialogs, IFilePickerService files, IUiDispatcher dispatcher)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<bool> ConfirmAsync(ScriptDocument document, long run_epoch, string title, string message, CancellationToken run_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        return AskAsync(
            document,
            run_epoch,
            nothing: false,
            "Confirmation failed: ",
            token => _dialogs.ConfirmAsync(title, message, "Yes", DialogTone.Neutral, document.Name, token),
            run_token);
    }

    public Task<string?> PromptAsync(ScriptDocument document, long run_epoch, string title, string initial, CancellationToken run_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        return AskAsync<string?>(
            document,
            run_epoch,
            nothing: null,
            "Prompt failed: ",
            token => _dialogs.PromptAsync(new PromptRequest(title, initial, "OK", "Your answer", Caption: document.Name), token),
            run_token);
    }

    public async Task DownloadAsync(ScriptDocument document, long run_epoch, string file_name, string content, CancellationToken run_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanAsk(document, run_epoch, run_token))
            return;
        using var asked = CancellationTokenSource.CreateLinkedTokenSource(run_token, _closing.Token);
        FilePickResult result = await _files.SaveTextAsync(file_name, content, asked.Token);
        if (result.Failure is { } failure)
            Report(document, "Download failed: " + failure);
    }

    public void CloseAll() => _closing.Cancel();

    public void Dispose() => _closing.Dispose();

    async Task<T> AskAsync<T>(ScriptDocument document, long run_epoch, T nothing, string failure, Func<CancellationToken, Task<T>> ask, CancellationToken run_token)
    {
        try
        {
            return await _dispatcher.InvokeAsync(async () =>
            {
                if (!CanAsk(document, run_epoch, run_token))
                    return nothing;
                using var asked = CancellationTokenSource.CreateLinkedTokenSource(run_token, _closing.Token);
                return await ask(asked.Token);
            }, run_token);
        }
        catch (OperationCanceledException)
        {
            return nothing;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Report(document, failure + error.Message);
            return nothing;
        }
    }

    bool CanAsk(ScriptDocument document, long run_epoch, CancellationToken run_token) =>
        !_closing.IsCancellationRequested &&
        !run_token.IsCancellationRequested &&
        !document.IsClosed &&
        document.Run.IsCurrent(run_epoch);

    static void Report(ScriptDocument document, string text) => document.Run.Output.Write(text, OutputLevel.Error);
}
