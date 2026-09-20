using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Qx.Presentation.Mvvm;

namespace Qx.Desktop.Controls;

[PseudoClasses(":toolbar", ":footer", ":count", ":notice", ":progress", ":progress-fraction", ":progress-cancel", ":readable")]
public sealed class PageLayout : ContentControl
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageLayout, object?>(nameof(Page));

    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<PageLayout, object?>(nameof(Toolbar));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PageLayout, object?>(nameof(Actions));

    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<PageLayout, object?>(nameof(Meta));

    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<PageLayout, string?>(nameof(CountText));

    public static readonly StyledProperty<NoticeLine?> NoticesProperty =
        AvaloniaProperty.Register<PageLayout, NoticeLine?>(nameof(Notices));

    public static readonly StyledProperty<OperationProgress?> ProgressProperty =
        AvaloniaProperty.Register<PageLayout, OperationProgress?>(nameof(Progress));

    public static readonly StyledProperty<PageWidth> WidthPolicyProperty =
        AvaloniaProperty.Register<PageLayout, PageWidth>(nameof(WidthPolicy));

    public static readonly DirectProperty<PageLayout, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, string>(nameof(Title), layout => layout.Title);

    public static readonly DirectProperty<PageLayout, string> SubtitleProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, string>(nameof(Subtitle), layout => layout.Subtitle);

    public static readonly DirectProperty<PageLayout, ViewState?> StateProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, ViewState?>(nameof(State), layout => layout.State);

    public static readonly DirectProperty<PageLayout, Notice?> NoticeProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, Notice?>(nameof(Notice), layout => layout.Notice);

    public static readonly DirectProperty<PageLayout, ICommand?> DismissNoticeProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, ICommand?>(nameof(DismissNotice), layout => layout.DismissNotice);

    public static readonly DirectProperty<PageLayout, string> ProgressTextProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, string>(nameof(ProgressText), layout => layout.ProgressText);

    public static readonly DirectProperty<PageLayout, double> ProgressValueProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, double>(nameof(ProgressValue), layout => layout.ProgressValue);

    public static readonly DirectProperty<PageLayout, ICommand?> ProgressCancelProperty =
        AvaloniaProperty.RegisterDirect<PageLayout, ICommand?>(nameof(ProgressCancel), layout => layout.ProgressCancel);

    PageViewModel? _page;
    NoticeLine? _notices;
    string _title = "";
    string _subtitle = "";
    ViewState? _state;
    Notice? _notice;
    ICommand? _dismiss_notice;
    string _progress_text = "";
    double _progress_value;
    ICommand? _progress_cancel;

    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public object? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public string? CountText
    {
        get => GetValue(CountTextProperty);
        set => SetValue(CountTextProperty, value);
    }

    public NoticeLine? Notices
    {
        get => GetValue(NoticesProperty);
        set => SetValue(NoticesProperty, value);
    }

    public OperationProgress? Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public PageWidth WidthPolicy
    {
        get => GetValue(WidthPolicyProperty);
        set => SetValue(WidthPolicyProperty, value);
    }

    public string Title
    {
        get => _title;
        private set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetAndRaise(SubtitleProperty, ref _subtitle, value);
    }

    public ViewState? State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    public Notice? Notice
    {
        get => _notice;
        private set => SetAndRaise(NoticeProperty, ref _notice, value);
    }

    public ICommand? DismissNotice
    {
        get => _dismiss_notice;
        private set => SetAndRaise(DismissNoticeProperty, ref _dismiss_notice, value);
    }

    public string ProgressText
    {
        get => _progress_text;
        private set => SetAndRaise(ProgressTextProperty, ref _progress_text, value);
    }

    public double ProgressValue
    {
        get => _progress_value;
        private set => SetAndRaise(ProgressValueProperty, ref _progress_value, value);
    }

    public ICommand? ProgressCancel
    {
        get => _progress_cancel;
        private set => SetAndRaise(ProgressCancelProperty, ref _progress_cancel, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty)
            ObservePage(Page as PageViewModel);
        else if (change.Property == NoticesProperty)
            ObserveNotices(Notices);
        else if (change.Property == ProgressProperty)
            RefreshProgress();
        else if (change.Property == ToolbarProperty)
            PseudoClasses.Set(":toolbar", Toolbar is not null);
        else if (change.Property == CountTextProperty)
            RefreshFooter();
        else if (change.Property == WidthPolicyProperty)
            PseudoClasses.Set(":readable", WidthPolicy == PageWidth.Readable);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObservePage(Page as PageViewModel);
        ObserveNotices(Notices);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ObservePage(null);
        ObserveNotices(null);
    }

    void ObservePage(PageViewModel? page)
    {
        if (!ReferenceEquals(_page, page))
        {
            if (_page is not null)
                _page.PropertyChanged -= OnPageChanged;
            _page = page;
            if (_page is not null)
                _page.PropertyChanged += OnPageChanged;
        }
        RefreshPage();
    }

    void ObserveNotices(NoticeLine? notices)
    {
        if (!ReferenceEquals(_notices, notices))
        {
            if (_notices is not null)
                _notices.PropertyChanged -= OnNoticesChanged;
            _notices = notices;
            if (_notices is not null)
                _notices.PropertyChanged += OnNoticesChanged;
        }
        RefreshNotice();
    }

    void OnPageChanged(object? sender, PropertyChangedEventArgs e) => RefreshPage();

    void OnNoticesChanged(object? sender, PropertyChangedEventArgs e) => RefreshNotice();

    void RefreshPage()
    {
        PageViewModel? page = _page ?? Page as PageViewModel;
        Title = page?.Title ?? "";
        Subtitle = page?.Subtitle ?? "";
        State = page?.State;
    }

    void RefreshNotice()
    {
        NoticeLine? notices = _notices ?? Notices;
        Notice = notices?.Current;
        DismissNotice = notices?.ClearCommand;
        RefreshFooter();
    }

    void RefreshProgress()
    {
        OperationProgress? progress = Progress;
        ProgressText = progress?.Text ?? "";
        ProgressValue = progress?.Fraction is { } fraction ? Math.Clamp(fraction, 0, 1) * 100 : 0;
        ProgressCancel = progress?.Cancel;
        PseudoClasses.Set(":progress-fraction", progress?.Fraction is not null);
        PseudoClasses.Set(":progress-cancel", progress?.Cancel is not null);
        RefreshFooter();
    }

    void RefreshFooter()
    {
        bool count = !string.IsNullOrEmpty(CountText);
        bool notice = Notice is not null;
        bool progress = Progress is not null;
        PseudoClasses.Set(":count", count);
        PseudoClasses.Set(":notice", notice && !progress);
        PseudoClasses.Set(":progress", progress);
        PseudoClasses.Set(":footer", count || notice || progress);
    }
}
