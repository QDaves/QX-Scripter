using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":idle", ":compiling", ":running", ":ready", ":stopping", ":busy", ":active", ":keycap")]
public sealed class RunButton : Button
{
    public static readonly TimeSpan LapDuration = TimeSpan.FromSeconds(1.6);
    public const double CometLength = 22;

    public static readonly StyledProperty<RunButtonState> StateProperty =
        AvaloniaProperty.Register<RunButton, RunButtonState>(nameof(State));

    public static readonly StyledProperty<ICommand?> RunCommandProperty =
        AvaloniaProperty.Register<RunButton, ICommand?>(nameof(RunCommand));

    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<RunButton, ICommand?>(nameof(StopCommand));

    public static readonly DirectProperty<RunButton, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<RunButton, string>(nameof(Label), button => button.Label);

    public static readonly DirectProperty<RunButton, IconKind> GlyphProperty =
        AvaloniaProperty.RegisterDirect<RunButton, IconKind>(nameof(Glyph), button => button.Glyph);

    Rectangle? _comet;
    CancellationTokenSource? _lap;
    string _label = "Run";
    IconKind _glyph = IconKind.Play;

    public RunButton()
    {
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(RunButton);

    public RunButtonState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public ICommand? RunCommand
    {
        get => GetValue(RunCommandProperty);
        set => SetValue(RunCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public string Label
    {
        get => _label;
        private set => SetAndRaise(LabelProperty, ref _label, value);
    }

    public IconKind Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    public bool IsCometRunning => _lap is { IsCancellationRequested: false };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_comet is not null)
            _comet.PropertyChanged -= OnCometChanged;
        _comet = e.NameScope.Find<Rectangle>("PART_Comet");
        if (_comet is not null)
            _comet.PropertyChanged += OnCometChanged;
        RestartLap();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty || change.Property == RunCommandProperty || change.Property == StopCommandProperty)
            Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopLap();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RestartLap();
    }

    void Apply()
    {
        RunButtonState state = State;
        PseudoClasses.Set(":idle", state == RunButtonState.Idle);
        PseudoClasses.Set(":compiling", state == RunButtonState.Compiling);
        PseudoClasses.Set(":running", state == RunButtonState.Running);
        PseudoClasses.Set(":ready", state == RunButtonState.Ready);
        PseudoClasses.Set(":stopping", state == RunButtonState.Stopping);
        PseudoClasses.Set(":busy", state is RunButtonState.Compiling or RunButtonState.Stopping);
        PseudoClasses.Set(":active", state != RunButtonState.Idle);
        PseudoClasses.Set(":keycap", state is RunButtonState.Idle or RunButtonState.Running);
        Label = state switch
        {
            RunButtonState.Compiling => "Compiling",
            RunButtonState.Running => "Stop",
            RunButtonState.Ready => "Ready",
            RunButtonState.Stopping => "Stopping",
            _ => "Run"
        };
        Glyph = state == RunButtonState.Idle ? IconKind.Play : IconKind.Stop;
        Command = state switch
        {
            RunButtonState.Idle => RunCommand,
            RunButtonState.Running or RunButtonState.Ready => StopCommand,
            _ => null
        };
        RestartLap();
    }

    void OnCometChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && State != RunButtonState.Idle)
            RestartLap();
    }

    void RestartLap()
    {
        StopLap();
        if (_comet is not { } comet || State == RunButtonState.Idle || !this.IsAttachedToVisualTree())
            return;
        double thickness = Math.Max(0.5, comet.StrokeThickness);
        double radius = comet.RadiusX;
        double width = comet.Bounds.Width - thickness;
        double height = comet.Bounds.Height - thickness;
        double perimeter = 2 * (width + height) - 8 * radius + 2 * Math.PI * radius;
        if (perimeter <= CometLength)
            return;
        double period = perimeter / thickness;
        double dash = CometLength / thickness;
        comet.StrokeDashArray = new AvaloniaList<double> { dash, period - dash };
        _lap = new CancellationTokenSource();
        var lap = new Animation
        {
            Duration = LapDuration,
            IterationCount = new IterationCount(1),
            Easing = new LinearEasing(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Shape.StrokeDashOffsetProperty, period) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Shape.StrokeDashOffsetProperty, 0d) } }
            }
        };
        LapAsync(lap, comet, _lap.Token).Observe("ui");
    }

    static async Task LapAsync(Animation lap, Rectangle comet, CancellationToken cancellation_token)
    {
        try
        {
            while (!cancellation_token.IsCancellationRequested)
                await lap.RunAsync(comet, cancellation_token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    void StopLap()
    {
        CancellationTokenSource? lap = Interlocked.Exchange(ref _lap, null);
        if (lap is null)
            return;
        lap.Cancel();
        lap.Dispose();
    }
}
