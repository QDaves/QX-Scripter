using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Presentation.ViewModels.Chat;

namespace Qx.Desktop.Views.Chat;

public sealed partial class ChatView : UserControl
{
    public const double FollowSlack = 24;

    ChatViewModel? _page;
    ScrollViewer? _scroller;
    bool _scheduled;

    public ChatView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_page is { } previous)
            previous.PropertyChanged -= OnPageChanged;
        _page = DataContext as ChatViewModel;
        if (_page is { } page)
            page.PropertyChanged += OnPageChanged;
        ScheduleScroll();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Attach();
        ScheduleScroll();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Detach();
    }

    void Attach()
    {
        if (_scroller is not null)
            return;
        _scroller = Lines.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_scroller is { } scroller)
            scroller.ScrollChanged += OnScrolled;
    }

    void Detach()
    {
        if (_scroller is { } scroller)
            scroller.ScrollChanged -= OnScrolled;
        _scroller = null;
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs args)
    {
        if (_page is not { } page || _scroller is not { } scroller)
            return;
        double distance = scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y;
        page.IsFollowing = distance <= FollowSlack;
    }

    void OnPageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ChatViewModel.ScrollToEndRequest))
            ScheduleScroll();
    }

    void ScheduleScroll()
    {
        if (_scheduled || _page is not { IsFollowing: true })
            return;
        _scheduled = true;
        Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
    }

    void ScrollToEnd()
    {
        _scheduled = false;
        Attach();
        if (_page is { IsFollowing: true } && _scroller is { } scroller)
            scroller.ScrollToEnd();
    }
}
