using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Qx.Diagnostics;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;
using BitmapCache = Qx.Desktop.Services.BitmapCache;

namespace Qx.Desktop.Controls;

[PseudoClasses(":image", ":fallback")]
public sealed class RemoteImage : TemplatedControl
{
    public static readonly StyledProperty<ImageRequest?> RequestProperty =
        AvaloniaProperty.Register<RemoteImage, ImageRequest?>(nameof(Request));

    public static readonly AttachedProperty<BitmapCache?> ImagesProperty =
        AvaloniaProperty.RegisterAttached<RemoteImage, Control, BitmapCache?>("Images", inherits: true);

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<RemoteImage, Stretch>(nameof(Stretch), Stretch.None);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<RemoteImage, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    public static readonly StyledProperty<double> FallbackSizeProperty =
        AvaloniaProperty.Register<RemoteImage, double>(nameof(FallbackSize), 16);

    public static readonly DirectProperty<RemoteImage, Bitmap?> SourceProperty =
        AvaloniaProperty.RegisterDirect<RemoteImage, Bitmap?>(nameof(Source), image => image.Source);

    public static readonly DirectProperty<RemoteImage, bool> HasImageProperty =
        AvaloniaProperty.RegisterDirect<RemoteImage, bool>(nameof(HasImage), image => image.HasImage);

    public static readonly DirectProperty<RemoteImage, IconKind> FallbackProperty =
        AvaloniaProperty.RegisterDirect<RemoteImage, IconKind>(nameof(Fallback), image => image.Fallback);

    Bitmap? _source;
    bool _has_image;
    IconKind _fallback;
    ImageRequest? _shown_for;
    CancellationTokenSource? _load;
    bool _in_view;

    public RemoteImage()
    {
        EffectiveViewportChanged += OnViewportChanged;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public static BitmapCache? GetImages(Control control) => control.GetValue(ImagesProperty);

    public static void SetImages(Control control, BitmapCache? value) => control.SetValue(ImagesProperty, value);

    public ImageRequest? Request
    {
        get => GetValue(RequestProperty);
        set => SetValue(RequestProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public double FallbackSize
    {
        get => GetValue(FallbackSizeProperty);
        set => SetValue(FallbackSizeProperty, value);
    }

    public Bitmap? Source
    {
        get => _source;
        private set
        {
            SetAndRaise(SourceProperty, ref _source, value);
            HasImage = value is not null;
            RefreshPseudoClasses();
        }
    }

    public bool HasImage
    {
        get => _has_image;
        private set => SetAndRaise(HasImageProperty, ref _has_image, value);
    }

    public IconKind Fallback
    {
        get => _fallback;
        private set => SetAndRaise(FallbackProperty, ref _fallback, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RequestProperty)
        {
            CancelLoad();
            Fallback = Request?.Fallback ?? IconKind.None;
            if (Request is null)
                Source = null;
            RefreshPseudoClasses();
            Start();
        }
        else if (change.Property == ImagesProperty)
        {
            Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _in_view = false;
        CancelLoad();
    }

    void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs args)
    {
        bool in_view = args.EffectiveViewport.Intersects(new Rect(Bounds.Size));
        if (in_view == _in_view)
            return;
        _in_view = in_view;
        if (in_view)
            Start();
    }

    void Start()
    {
        ImageRequest? request = Request;
        if (request is null || GetImages(this) is not { } images)
            return;
        if (images.Cached(request.Url) is { } cached)
        {
            _shown_for = request;
            Source = cached;
            return;
        }
        if (!_in_view || _load is not null || ReferenceEquals(_shown_for, request))
            return;
        _load = new CancellationTokenSource();
        LoadAsync(request, images, _load).Observe("images");
    }

    async Task LoadAsync(ImageRequest request, BitmapCache images, CancellationTokenSource load)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await images.LoadAsync(request.Url, request.ExactPixels, load.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            Diag.Warn($"Image {request.Url} failed: {error.Message}", "images");
        }
        finally
        {
            if (ReferenceEquals(_load, load))
                _load = null;
            load.Dispose();
        }
        if (load.IsCancellationRequested || !ReferenceEquals(Request, request))
            return;
        _shown_for = request;
        Source = bitmap;
    }

    void CancelLoad()
    {
        CancellationTokenSource? load = Interlocked.Exchange(ref _load, null);
        if (load is null)
            return;
        try
        {
            load.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    void RefreshPseudoClasses()
    {
        PseudoClasses.Set(":image", _source is not null);
        PseudoClasses.Set(":fallback", _source is null && Fallback != IconKind.None);
    }
}
