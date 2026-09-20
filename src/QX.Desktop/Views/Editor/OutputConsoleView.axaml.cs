using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Desktop.Controls;
using Qx.Desktop.Input;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Output;
using Qx.Presentation.ViewModels.Editor;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Views.Editor;

public sealed partial class OutputConsoleView : UserControl
{
    public const double FollowSlack = 24;

    OutputConsoleViewModel? _console;
    OutputPreferences? _output;
    ScrollViewer? _scroller;
    bool _scheduled;

    public OutputConsoleView()
    {
        InitializeComponent();
        Wrap.Click += OnWrapClicked;
        Lines.AddHandler(KeyDownEvent, OnLinesKeyDown, RoutingStrategies.Bubble);
    }

    public void Use(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (_output is { } previous)
            previous.PropertyChanged -= OnOutputChanged;
        _output = workspace.Output;
        _output.PropertyChanged += OnOutputChanged;
        NextError.Command = workspace.NextErrorCommand;
        ApplyWrap();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_console is { } previous)
            previous.PropertyChanged -= OnConsoleChanged;
        _console = DataContext as OutputConsoleViewModel;
        if (_console is { } console)
            console.PropertyChanged += OnConsoleChanged;
        ApplyCollapse();
        ApplyProblems();
        ScheduleScroll();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_scroller is { } scroller)
            scroller.ScrollChanged -= OnScrolled;
        _scroller = null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Attach();
    }

    public void Release()
    {
        NextError.Command = null;
        Wrap.Click -= OnWrapClicked;
        Lines.RemoveHandler(KeyDownEvent, OnLinesKeyDown);
        if (_console is { } console)
            console.PropertyChanged -= OnConsoleChanged;
        if (_output is { } output)
            output.PropertyChanged -= OnOutputChanged;
        if (_scroller is { } scroller)
            scroller.ScrollChanged -= OnScrolled;
        _scroller = null;
        _console = null;
        _output = null;
    }

    void Attach()
    {
        if (_scroller is not null)
            return;
        _scroller = Lines.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_scroller is { } scroller)
            scroller.ScrollChanged += OnScrolled;
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs args)
    {
        if (_console is not { } console || _scroller is not { } scroller)
            return;
        double distance = scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y;
        console.IsFollowing = distance <= FollowSlack;
    }

    void OnConsoleChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(OutputConsoleViewModel.ScrollToEndRequest):
                ScheduleScroll();
                break;
            case nameof(OutputConsoleViewModel.IsCollapsed):
                ApplyCollapse();
                break;
            case nameof(OutputConsoleViewModel.ErrorCount):
                ApplyProblems();
                break;
        }
    }

    void OnOutputChanged(object? sender, PropertyChangedEventArgs args) => ApplyWrap();

    void OnWrapClicked(object? sender, RoutedEventArgs args) => _output?.ToggleWrap();

    void OnLinesKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.C || !args.KeyModifiers.HasFlag(PrimaryModifier.Current) || _console is not { } console)
            return;
        OutputLine[] selected = Lines.SelectedItems is { } items ? [.. items.OfType<OutputLine>()] : [];
        if (selected.Length == 0)
            return;
        console.CopySelected(selected);
        args.Handled = true;
    }

    void ApplyWrap()
    {
        bool wrap = _output?.Wrap == true;
        Wrap.IsChecked = wrap;
        WrapGlyph.Kind = wrap ? IconKind.WrapOff : IconKind.Wrap;
        Wrap.SetValue(ToolTip.TipProperty, _output?.WrapTip ?? "Wrap output lines");
        Body.Classes.Set("wrapped", wrap);
        Lines.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
    }

    void ApplyCollapse() =>
        CollapseGlyph.Kind = _console?.IsCollapsed == true ? IconKind.ChevronUp : IconKind.ChevronDown;

    void ApplyProblems() =>
        Problems.Tone = _console is { HasErrors: true } ? StatusTone.Danger : StatusTone.Warning;

    void ScheduleScroll()
    {
        if (_scheduled || _console is not { IsFollowing: true })
            return;
        _scheduled = true;
        Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
    }

    void ScrollToEnd()
    {
        _scheduled = false;
        Attach();
        if (_console is { IsFollowing: true } && _scroller is { } scroller)
            scroller.ScrollToEnd();
    }
}
