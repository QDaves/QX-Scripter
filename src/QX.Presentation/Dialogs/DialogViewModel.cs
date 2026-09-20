using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public abstract class DialogViewModel : ObservableObject
{
    protected DialogViewModel() => CancelCommand = new RelayCommand(Dismiss);

    public abstract string Title { get; }

    public virtual IconKind Icon => IconKind.Info;

    public virtual DialogTone Tone => DialogTone.Neutral;

    public virtual string? Caption => null;

    public IRelayCommand CancelCommand { get; }

    internal abstract bool IsClosed { get; }

    internal abstract void Dismiss();
}

public abstract class DialogViewModel<TResult> : DialogViewModel
{
    readonly TaskCompletionSource<TResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult> Result => _result.Task;

    protected abstract TResult DismissResult { get; }

    protected void Close(TResult result) => _result.TrySetResult(result);

    internal override bool IsClosed => _result.Task.IsCompleted;

    internal override void Dismiss() => Close(DismissResult);
}
