using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Input;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.CommandPalette;

public sealed partial class CommandPaletteViewModel : ObservableObject, ICommandPalette
{
    readonly ICommandRegistry _registry;
    readonly IGestureFormatter _gestures;
    readonly IUiDispatcher _dispatcher;

    public CommandPaletteViewModel(ICommandRegistry registry, IGestureFormatter gestures, IUiDispatcher dispatcher)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<PaletteEntry> Results { get; private set; } = [];

    [ObservableProperty]
    public partial PaletteEntry? Selected { get; set; }

    public bool IsEmpty => Results.Count == 0;

    public string EmptyText => "No matching command";

    public string Placeholder => "Search commands";

    public void Open()
    {
        Query = "";
        Refilter();
        IsOpen = true;
    }

    public void Dismiss() => IsOpen = false;

    [RelayCommand]
    void MoveSelection(int offset)
    {
        if (Results.Count == 0)
            return;
        int at = IndexOfSelected();
        int next = at < 0
            ? offset < 0 ? Results.Count - 1 : 0
            : ((at + offset) % Results.Count + Results.Count) % Results.Count;
        Selected = Results[next];
    }

    [RelayCommand]
    void InvokeSelected()
    {
        if (Selected is not { } entry)
            return;
        Dismiss();
        _dispatcher.Post(() =>
        {
            if (entry.Command.Command.CanExecute(null))
                entry.Command.Command.Execute(null);
        });
    }

    partial void OnQueryChanged(string value) => Refilter();

    void Refilter()
    {
        Results = [.. CommandMatcher.Filter(_registry.Commands, Query).Select(Entry)];
        Selected = Results.Count > 0 ? Results[0] : null;
    }

    int IndexOfSelected()
    {
        if (Selected is not { } current)
            return -1;
        for (int index = 0; index < Results.Count; index++)
        {
            if (Results[index] == current)
                return index;
        }
        return -1;
    }

    PaletteEntry Entry(AppCommand command) =>
        new(command, command.Group.ToString(), command.Title, Gesture(command));

    string Gesture(AppCommand command) =>
        command.GestureText ?? (command.PrimaryChord is { } chord ? _gestures.Describe(chord) : "");
}
