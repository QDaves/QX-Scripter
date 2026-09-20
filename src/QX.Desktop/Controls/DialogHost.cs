using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Presentation.Dialogs;

namespace Qx.Desktop.Controls;

[PseudoClasses(":open")]
public sealed class DialogHost : TemplatedControl
{
    public static readonly StyledProperty<object?> DialogProperty =
        AvaloniaProperty.Register<DialogHost, object?>(nameof(Dialog));

    public static readonly StyledProperty<IDataTemplate?> ViewsProperty =
        AvaloniaProperty.Register<DialogHost, IDataTemplate?>(nameof(Views));

    ContentControl? _card;

    public object? Dialog
    {
        get => GetValue(DialogProperty);
        set => SetValue(DialogProperty, value);
    }

    public IDataTemplate? Views
    {
        get => GetValue(ViewsProperty);
        set => SetValue(ViewsProperty, value);
    }

    public bool IsOpen => Dialog is not null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _card = e.NameScope.Find<ContentControl>("PART_Card");
        if (_card is { } card)
            KeyboardNavigation.SetTabNavigation(card, KeyboardNavigationMode.Cycle);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != DialogProperty)
            return;
        bool open = Dialog is not null;
        PseudoClasses.Set(":open", open);
        IsVisible = open;
        if (open)
            Dispatcher.UIThread.Post(FocusInitial, DispatcherPriority.Loaded);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || Dialog is not DialogViewModel dialog)
            return;
        if (dialog.CancelCommand.CanExecute(null))
            dialog.CancelCommand.Execute(null);
        e.Handled = true;
    }

    void FocusInitial()
    {
        if (_card is null || Dialog is null || !_card.IsAttachedToVisualTree())
            return;
        List<Control> candidates = _card.GetVisualDescendants().OfType<Control>().Where(control => control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible).ToList();
        bool destructive = Dialog is DialogViewModel { Tone: DialogTone.Destructive };
        Control? target = destructive
            ? candidates.OfType<Button>().FirstOrDefault(button => button.IsCancel) ?? candidates.OfType<Button>().FirstOrDefault()
            : candidates.OfType<TextBox>().FirstOrDefault() as Control ?? candidates.OfType<Button>().FirstOrDefault(button => button.IsDefault);
        (target ?? candidates.FirstOrDefault())?.Focus();
    }
}
