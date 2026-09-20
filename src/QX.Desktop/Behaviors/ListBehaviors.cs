using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Presentation.Mvvm;

namespace Qx.Desktop.Behaviors;

public static class ListBehaviors
{
    public static readonly AttachedProperty<ISelectionTarget?> SelectionProperty =
        AvaloniaProperty.RegisterAttached<Control, ISelectionTarget?>("Selection", typeof(ListBehaviors));

    public static readonly AttachedProperty<bool> ClearOnDismissProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("ClearOnDismiss", typeof(ListBehaviors));

    public static readonly AttachedProperty<SortRefreshRequest?> SortRefreshProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, SortRefreshRequest?>("SortRefresh", typeof(ListBehaviors));

    public static readonly AttachedProperty<ScrollRequest?> ScrollProperty =
        AvaloniaProperty.RegisterAttached<Control, ScrollRequest?>("Scroll", typeof(ListBehaviors));

    public static readonly AttachedProperty<IVisibleItemsSink?> VisibleSinkProperty =
        AvaloniaProperty.RegisterAttached<Control, IVisibleItemsSink?>("VisibleSink", typeof(ListBehaviors));

    public static readonly AttachedProperty<ICommand?> CopyCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("CopyCommand", typeof(ListBehaviors));

    static readonly AttachedProperty<IDisposable?> SelectionLinkProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("SelectionLink", typeof(ListBehaviors));

    static readonly AttachedProperty<IDisposable?> SortLinkProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, IDisposable?>("SortLink", typeof(ListBehaviors));

    static readonly AttachedProperty<IDisposable?> ScrollLinkProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("ScrollLink", typeof(ListBehaviors));

    static readonly AttachedProperty<IDisposable?> VisibleLinkProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("VisibleLink", typeof(ListBehaviors));

    static readonly AttachedProperty<IDisposable?> CopyLinkProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("CopyLink", typeof(ListBehaviors));

    static ListBehaviors()
    {
        SelectionProperty.Changed.AddClassHandler<Control>(OnSelectionChanged);
        SortRefreshProperty.Changed.AddClassHandler<DataGrid>(OnSortRefreshChanged);
        ScrollProperty.Changed.AddClassHandler<Control>(OnScrollChanged);
        VisibleSinkProperty.Changed.AddClassHandler<Control>(OnVisibleSinkChanged);
        CopyCommandProperty.Changed.AddClassHandler<Control>(OnCopyCommandChanged);
    }

    public static ISelectionTarget? GetSelection(Control control) => control.GetValue(SelectionProperty);

    public static void SetSelection(Control control, ISelectionTarget? value) => control.SetValue(SelectionProperty, value);

    public static bool GetClearOnDismiss(Control control) => control.GetValue(ClearOnDismissProperty);

    public static void SetClearOnDismiss(Control control, bool value) => control.SetValue(ClearOnDismissProperty, value);

    public static SortRefreshRequest? GetSortRefresh(DataGrid grid) => grid.GetValue(SortRefreshProperty);

    public static void SetSortRefresh(DataGrid grid, SortRefreshRequest? value) => grid.SetValue(SortRefreshProperty, value);

    public static ScrollRequest? GetScroll(Control control) => control.GetValue(ScrollProperty);

    public static void SetScroll(Control control, ScrollRequest? value) => control.SetValue(ScrollProperty, value);

    public static IVisibleItemsSink? GetVisibleSink(Control control) => control.GetValue(VisibleSinkProperty);

    public static void SetVisibleSink(Control control, IVisibleItemsSink? value) => control.SetValue(VisibleSinkProperty, value);

    public static ICommand? GetCopyCommand(Control control) => control.GetValue(CopyCommandProperty);

    public static void SetCopyCommand(Control control, ICommand? value) => control.SetValue(CopyCommandProperty, value);

    static void OnSelectionChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.GetValue(SelectionLinkProperty)?.Dispose();
        control.SetValue(SelectionLinkProperty, args.NewValue is ISelectionTarget target ? new SelectionLink(control, target) : null);
    }

    static void OnSortRefreshChanged(DataGrid grid, AvaloniaPropertyChangedEventArgs args)
    {
        grid.GetValue(SortLinkProperty)?.Dispose();
        grid.SetValue(SortLinkProperty, args.NewValue is SortRefreshRequest request ? new SortLink(grid, request) : null);
    }

    static void OnScrollChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.GetValue(ScrollLinkProperty)?.Dispose();
        control.SetValue(ScrollLinkProperty, args.NewValue is ScrollRequest request ? new ScrollLink(control, request) : null);
    }

    static void OnVisibleSinkChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.GetValue(VisibleLinkProperty)?.Dispose();
        control.SetValue(VisibleLinkProperty, args.NewValue is IVisibleItemsSink sink ? new VisibleLink(control, sink) : null);
    }

    static void OnCopyCommandChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.GetValue(CopyLinkProperty)?.Dispose();
        control.SetValue(CopyLinkProperty, args.NewValue is ICommand command ? new CopyLink(control, command) : null);
    }

    sealed class SelectionLink : IDisposable
    {
        readonly Control _control;
        readonly ISelectionTarget _target;
        UserControl? _page;
        bool _applying;

        public SelectionLink(Control control, ISelectionTarget target)
        {
            _control = control;
            _target = target;
            _target.SelectRequested += Select;
            _control.AttachedToVisualTree += OnAttached;
            _control.DetachedFromVisualTree += OnDetached;
            AttachPage();
            if (_control is ListBox list)
                list.SelectionChanged += OnViewSelectionChanged;
            else if (_control is DataGrid grid)
                grid.SelectionChanged += OnViewSelectionChanged;
        }

        public void Dispose()
        {
            _target.SelectRequested -= Select;
            _control.AttachedToVisualTree -= OnAttached;
            _control.DetachedFromVisualTree -= OnDetached;
            DetachPage();
            if (_control is ListBox list)
                list.SelectionChanged -= OnViewSelectionChanged;
            else if (_control is DataGrid grid)
                grid.SelectionChanged -= OnViewSelectionChanged;
        }

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs args) => AttachPage();

        void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            if (GetClearOnDismiss(_control))
                Select([]);
            DetachPage();
        }

        void AttachPage()
        {
            DetachPage();
            _page = _control.FindAncestorOfType<UserControl>();
            _page?.AddHandler(InputElement.PointerPressedEvent, OnPagePressed, RoutingStrategies.Tunnel);
        }

        void DetachPage()
        {
            _page?.RemoveHandler(InputElement.PointerPressedEvent, OnPagePressed);
            _page = null;
        }

        void OnPagePressed(object? sender, PointerPressedEventArgs args)
        {
            if (!GetClearOnDismiss(_control) || !args.GetCurrentPoint(_control).Properties.IsLeftButtonPressed ||
                args.Source is not Visual source)
                return;
            for (Visual? current = source; current is not null && current != _page; current = current.GetVisualParent())
            {
                if (current is DataGridRow or ListBoxItem or Button or TextBox or ScrollBar or
                    DataGridColumnHeader or MenuItem)
                    return;
            }
            Select([]);
        }

        void OnViewSelectionChanged(object? sender, SelectionChangedEventArgs args)
        {
            if (!_applying)
                _target.Replace(Selected().Cast<object>());
        }

        IList Selected() => _control switch
        {
            ListBox list => list.SelectedItems ?? Array.Empty<object>(),
            DataGrid grid => grid.SelectedItems,
            _ => Array.Empty<object>()
        };

        void Select(IReadOnlyList<object> items)
        {
            _applying = true;
            try
            {
                IList selected = Selected();
                selected.Clear();
                foreach (object item in items)
                    selected.Add(item);
            }
            finally
            {
                _applying = false;
            }
            _target.Replace(items);
            if (items.Count == 0)
                return;
            if (_control is ListBox list)
                list.ScrollIntoView(items[0]);
            else if (_control is DataGrid grid)
                grid.ScrollIntoView(items[0], null);
        }
    }

    sealed class SortLink : IDisposable
    {
        readonly DataGrid _grid;
        readonly SortRefreshRequest _request;
        readonly DispatcherTimer _timer;

        public SortLink(DataGrid grid, SortRefreshRequest request)
        {
            _grid = grid;
            _request = request;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, Refresh);
            _timer.Stop();
            _request.Requested += Schedule;
        }

        public void Dispose()
        {
            _request.Requested -= Schedule;
            _timer.Stop();
        }

        void Schedule()
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        void Refresh(object? sender, EventArgs args)
        {
            _timer.Stop();
            if (_grid.CollectionView is DataGridCollectionView { SortDescriptions.Count: > 0 } view)
                view.Refresh();
        }
    }

    sealed class ScrollLink : IDisposable
    {
        readonly Control _control;
        readonly ScrollRequest _request;

        public ScrollLink(Control control, ScrollRequest request)
        {
            _control = control;
            _request = request;
            _request.ResetRequested += Reset;
            _request.IntoViewRequested += IntoView;
            _request.EndRequested += End;
        }

        public void Dispose()
        {
            _request.ResetRequested -= Reset;
            _request.IntoViewRequested -= IntoView;
            _request.EndRequested -= End;
        }

        void Reset()
        {
            if (_control is ListBox { Scroll: ScrollViewer viewer })
                viewer.Offset = default;
            else if (_control is DataGrid grid && grid.CollectionView?.Cast<object>().FirstOrDefault() is { } first)
                grid.ScrollIntoView(first, null);
        }

        void IntoView(object item)
        {
            if (_control is ListBox list)
                list.ScrollIntoView(item);
            else if (_control is DataGrid grid)
                grid.ScrollIntoView(item, null);
        }

        void End()
        {
            if (_control is ListBox { Scroll: ScrollViewer viewer })
                viewer.ScrollToEnd();
            else if (_control is DataGrid grid && grid.CollectionView?.Cast<object>().LastOrDefault() is { } last)
                grid.ScrollIntoView(last, null);
        }
    }

    sealed class VisibleLink : IDisposable
    {
        readonly Control _control;
        readonly IVisibleItemsSink _sink;
        readonly DispatcherTimer _timer;
        ScrollViewer? _scroller;
        object[] _reported = [];

        public VisibleLink(Control control, IVisibleItemsSink sink)
        {
            _control = control;
            _sink = sink;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, Report);
            _timer.Stop();
            _control.LayoutUpdated += OnLayoutUpdated;
            _control.DetachedFromVisualTree += OnDetached;
        }

        public void Dispose()
        {
            _timer.Stop();
            _control.LayoutUpdated -= OnLayoutUpdated;
            _control.DetachedFromVisualTree -= OnDetached;
            Unhook();
        }

        void OnLayoutUpdated(object? sender, EventArgs args)
        {
            if (_scroller is null)
                Hook();
            Schedule();
        }

        void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _timer.Stop();
            Unhook();
        }

        void Hook()
        {
            _scroller = _control.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (_scroller is not null)
                _scroller.ScrollChanged += OnScrollChanged;
        }

        void Unhook()
        {
            if (_scroller is not null)
                _scroller.ScrollChanged -= OnScrollChanged;
            _scroller = null;
        }

        void OnScrollChanged(object? sender, ScrollChangedEventArgs args) => Schedule();

        void Schedule()
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        void Report(object? sender, EventArgs args)
        {
            _timer.Stop();
            object[] visible = Visible();
            if (visible.SequenceEqual(_reported, ReferenceEqualityComparer.Instance))
                return;
            _reported = visible;
            _sink.VisibleChanged(visible);
        }

        object[] Visible()
        {
            var viewport = new Rect(_control.Bounds.Size);
            var entries = new List<(object Item, Point Top)>();
            foreach ((Control container, object item) in Pairs())
            {
                if (container.TranslatePoint(new Point(0, 0), _control) is not { } top || !viewport.Intersects(new Rect(top, container.Bounds.Size)))
                    continue;
                entries.Add((item, top));
            }
            return entries.OrderBy(entry => entry.Top.Y).ThenBy(entry => entry.Top.X).Select(entry => entry.Item).ToArray();
        }

        IEnumerable<(Control Container, object Item)> Pairs()
        {
            switch (_control)
            {
                case DataGrid grid:
                    foreach (DataGridRow row in grid.GetVisualDescendants().OfType<DataGridRow>())
                    {
                        if (row.DataContext is { } item)
                            yield return (row, item);
                    }
                    break;
                case ItemsControl items:
                    foreach (Control container in items.GetRealizedContainers())
                    {
                        if (items.ItemFromContainer(container) is { } item)
                            yield return (container, item);
                    }
                    break;
            }
        }
    }

    sealed class CopyLink : IDisposable
    {
        readonly Control _control;
        readonly ICommand _command;

        public CopyLink(Control control, ICommand command)
        {
            _control = control;
            _command = command;
            _control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        }

        public void Dispose() => _control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);

        void OnKeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Handled)
                return;
            List<KeyGesture>? gestures = _control.GetPlatformSettings()?.HotkeyConfiguration.Copy;
            if (gestures is null || !gestures.Any(gesture => gesture.Matches(args)) || !_command.CanExecute(null))
                return;
            _command.Execute(null);
            args.Handled = true;
        }
    }
}
