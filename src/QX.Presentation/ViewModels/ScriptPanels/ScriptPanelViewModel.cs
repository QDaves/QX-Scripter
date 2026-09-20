using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Workspace;

namespace Qx.Presentation.ViewModels.ScriptPanels;

public sealed partial class ScriptPanelViewModel : ViewModelBase
{
    readonly IFilePickerService _pickers;

    public ScriptPanelViewModel(ScriptDocument document, IFilePickerService pickers)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _pickers = pickers ?? throw new ArgumentNullException(nameof(pickers));
        Panel = document.Panel;
        Run = document.Run;
        Document.PropertyChanged += OnDocumentChanged;
        Run.PropertyChanged += OnRunChanged;
        ((INotifyCollectionChanged)Run.Errors).CollectionChanged += OnErrorsChanged;
        Own(() =>
        {
            Document.PropertyChanged -= OnDocumentChanged;
            Run.PropertyChanged -= OnRunChanged;
            ((INotifyCollectionChanged)Run.Errors).CollectionChanged -= OnErrorsChanged;
        });
    }

    public ScriptDocument Document { get; }

    public PanelDocument Panel { get; }

    public ScriptRunController Run { get; }

    public bool IsPanelMode => Document.PanelMode;

    public string StatusText => Document.StatusText;

    public bool HasStatus => Document.StatusText.Length > 0;

    public bool IsWorking => Run.IsWorking;

    public RunPhase RunPhase => Run.Phase;

    public bool ShowStandInRun => Panel.DeclaredButtons.Count == 0;

    public bool ShowRunControl => ShowStandInRun || Run.IsAlive;

    public bool HasErrors => Run.Errors.Count > 0;

    public string ErrorSummary => Run.Errors.Count == 0 ? "" : Run.Errors[0].Format().Split('\n')[0].TrimEnd('\r');

    [RelayCommand]
    void StandInRun() => PanelPress.Route(Run, null);

    [RelayCommand]
    void Stop() => Run.RequestStop();

    [RelayCommand]
    void ShowCode() => Document.SetPanelMode(false);

    [RelayCommand]
    async Task BrowseAsync(PanelFileFieldNode? field, CancellationToken cancellation_token)
    {
        if (field is null)
            return;
        FilePickResult picked = await _pickers.PickFileAsync(field.Label, cancellation_token);
        if (picked.LocalPath is { Length: > 0 } path)
            field.Path = path;
    }

    public void PressPrimary()
    {
        if (Primary() is { } button)
        {
            if (button.PressCommand.CanExecute(null))
                button.PressCommand.Execute(null);
            return;
        }
        if (ShowStandInRun && StandInRunCommand.CanExecute(null))
            StandInRunCommand.Execute(null);
    }

    PanelButtonNode? Primary()
    {
        PanelButtonNode? first = null;
        foreach (PanelButtonNode button in Buttons(Panel.Nodes))
        {
            first ??= button;
            if (button.Look == PanelButtonLook.Primary)
                return button;
        }
        return first;
    }

    static IEnumerable<PanelButtonNode> Buttons(IEnumerable<PanelNode> nodes)
    {
        foreach (PanelNode node in nodes)
        {
            switch (node)
            {
                case PanelButtonNode button:
                    yield return button;
                    break;
                case PanelButtonStripNode strip:
                    foreach (PanelButtonNode button in strip.Buttons)
                        yield return button;
                    break;
                case PanelRowNode row:
                    foreach (PanelButtonNode button in Buttons(row.Children))
                        yield return button;
                    break;
                case PanelGroupNode group:
                    foreach (PanelButtonNode button in Buttons(group.Children))
                        yield return button;
                    break;
            }
        }
    }

    void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ScriptDocument.PanelMode))
        {
            OnPropertyChanged(nameof(IsPanelMode));
            return;
        }
        if (args.PropertyName != nameof(ScriptDocument.StatusText))
            return;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

    void OnRunChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(ScriptRunController.State) or nameof(ScriptRunController.PanelArmed) or nameof(ScriptRunController.BusyHandlers)))
            return;
        OnPropertyChanged(nameof(RunPhase));
        OnPropertyChanged(nameof(ShowStandInRun));
        OnPropertyChanged(nameof(ShowRunControl));
        OnPropertyChanged(nameof(IsWorking));
        RefreshErrors();
    }

    void OnErrorsChanged(object? sender, NotifyCollectionChangedEventArgs args) => RefreshErrors();

    void RefreshErrors()
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorSummary));
    }
}
