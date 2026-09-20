using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Threading;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":ready", ":loading", ":empty", ":unavailable", ":error", ":message", ":action", ":cancel")]
public sealed class StateHost : ContentControl
{
    public static readonly StyledProperty<ViewState?> StateProperty =
        AvaloniaProperty.Register<StateHost, ViewState?>(nameof(State));

    public static readonly StyledProperty<TimeSpan> MessageDelayProperty =
        AvaloniaProperty.Register<StateHost, TimeSpan>(nameof(MessageDelay), TimeSpan.FromMilliseconds(400));

    public static readonly DirectProperty<StateHost, ViewStateKind> KindProperty =
        AvaloniaProperty.RegisterDirect<StateHost, ViewStateKind>(nameof(Kind), host => host.Kind);

    public static readonly DirectProperty<StateHost, IconKind> IconProperty =
        AvaloniaProperty.RegisterDirect<StateHost, IconKind>(nameof(Icon), host => host.Icon);

    public static readonly DirectProperty<StateHost, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<StateHost, string>(nameof(Title), host => host.Title);

    public static readonly DirectProperty<StateHost, string> MessageProperty =
        AvaloniaProperty.RegisterDirect<StateHost, string>(nameof(Message), host => host.Message);

    public static readonly DirectProperty<StateHost, ICommand?> ActionProperty =
        AvaloniaProperty.RegisterDirect<StateHost, ICommand?>(nameof(Action), host => host.Action);

    public static readonly DirectProperty<StateHost, string> ActionTextProperty =
        AvaloniaProperty.RegisterDirect<StateHost, string>(nameof(ActionText), host => host.ActionText);

    public static readonly DirectProperty<StateHost, ICommand?> CancelProperty =
        AvaloniaProperty.RegisterDirect<StateHost, ICommand?>(nameof(Cancel), host => host.Cancel);

    public static readonly DirectProperty<StateHost, StatusTone> ToneProperty =
        AvaloniaProperty.RegisterDirect<StateHost, StatusTone>(nameof(Tone), host => host.Tone);

    ViewState? _observed;
    DispatcherTimer? _message_timer;
    ViewStateKind _kind;
    IconKind _icon;
    string _title = "";
    string _message = "";
    ICommand? _action;
    string _action_text = "";
    ICommand? _cancel;
    StatusTone _tone;

    public StateHost()
    {
        PseudoClasses.Set(":ready", true);
    }

    public ViewState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public TimeSpan MessageDelay
    {
        get => GetValue(MessageDelayProperty);
        set => SetValue(MessageDelayProperty, value);
    }

    public ViewStateKind Kind
    {
        get => _kind;
        private set => SetAndRaise(KindProperty, ref _kind, value);
    }

    public IconKind Icon
    {
        get => _icon;
        private set => SetAndRaise(IconProperty, ref _icon, value);
    }

    public string Title
    {
        get => _title;
        private set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public string Message
    {
        get => _message;
        private set => SetAndRaise(MessageProperty, ref _message, value);
    }

    public ICommand? Action
    {
        get => _action;
        private set => SetAndRaise(ActionProperty, ref _action, value);
    }

    public string ActionText
    {
        get => _action_text;
        private set => SetAndRaise(ActionTextProperty, ref _action_text, value);
    }

    public ICommand? Cancel
    {
        get => _cancel;
        private set => SetAndRaise(CancelProperty, ref _cancel, value);
    }

    public StatusTone Tone
    {
        get => _tone;
        private set => SetAndRaise(ToneProperty, ref _tone, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
            Observe(State);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(State);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        _message_timer?.Stop();
    }

    void Observe(ViewState? state)
    {
        if (ReferenceEquals(_observed, state))
        {
            Refresh();
            return;
        }
        if (_observed is not null)
            _observed.PropertyChanged -= OnStateChanged;
        _observed = state;
        if (_observed is not null)
            _observed.PropertyChanged += OnStateChanged;
        Refresh();
    }

    void OnStateChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    void Refresh()
    {
        ViewState? state = _observed;
        ViewStateKind kind = state?.Kind ?? ViewStateKind.Ready;
        Kind = kind;
        Icon = state?.Icon ?? IconKind.None;
        Title = state?.Title ?? "";
        Message = state?.Message ?? "";
        Action = state?.Action;
        ActionText = state?.ActionText ?? "";
        Cancel = state?.Cancel;
        Tone = kind == ViewStateKind.Error ? StatusTone.Danger : StatusTone.Neutral;
        PseudoClasses.Set(":ready", kind == ViewStateKind.Ready);
        PseudoClasses.Set(":loading", kind == ViewStateKind.Loading);
        PseudoClasses.Set(":empty", kind == ViewStateKind.Empty);
        PseudoClasses.Set(":unavailable", kind == ViewStateKind.Unavailable);
        PseudoClasses.Set(":error", kind == ViewStateKind.Error);
        PseudoClasses.Set(":action", Action is not null && ActionText.Length > 0);
        PseudoClasses.Set(":cancel", Cancel is not null);
        ScheduleMessage(kind == ViewStateKind.Loading && Message.Length > 0);
    }

    void ScheduleMessage(bool loading)
    {
        _message_timer?.Stop();
        if (!loading)
        {
            PseudoClasses.Set(":message", false);
            return;
        }
        if (MessageDelay <= TimeSpan.Zero)
        {
            PseudoClasses.Set(":message", true);
            return;
        }
        PseudoClasses.Set(":message", false);
        if (_message_timer is null)
        {
            _message_timer = new DispatcherTimer(DispatcherPriority.Background);
            _message_timer.Tick += OnMessageDue;
        }
        _message_timer.Interval = MessageDelay;
        _message_timer.Start();
    }

    void OnMessageDue(object? sender, EventArgs e)
    {
        _message_timer?.Stop();
        if (Kind == ViewStateKind.Loading)
            PseudoClasses.Set(":message", true);
    }
}
