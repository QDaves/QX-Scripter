using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":tone0", ":tone1", ":tone2", ":tone3", ":tone4", ":tone5", ":image", ":bot", ":pet", ":dimmed")]
public sealed class Avatar : TemplatedControl
{
    const int ToneCount = 6;

    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<ImageRequest?> ImageProperty =
        AvaloniaProperty.Register<Avatar, ImageRequest?>(nameof(Image));

    public static readonly StyledProperty<AvatarKind> KindProperty =
        AvaloniaProperty.Register<Avatar, AvatarKind>(nameof(Kind));

    public static readonly StyledProperty<bool> IsDimmedProperty =
        AvaloniaProperty.Register<Avatar, bool>(nameof(IsDimmed));

    public static readonly DirectProperty<Avatar, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<Avatar, string>(nameof(Initials), avatar => avatar.Initials);

    string _initials = "";
    RemoteImage? _image;

    public Avatar()
    {
        PseudoClasses.Set(":tone0", true);
    }

    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public ImageRequest? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public AvatarKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsDimmed
    {
        get => GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }

    public string Initials
    {
        get => _initials;
        private set => SetAndRaise(InitialsProperty, ref _initials, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_image is not null)
            _image.PropertyChanged -= OnImageChanged;
        _image = e.NameScope.Find<RemoteImage>("PART_Image");
        if (_image is not null)
            _image.PropertyChanged += OnImageChanged;
        PseudoClasses.Set(":image", _image?.HasImage == true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            PseudoClasses.Set(":bot", Kind == AvatarKind.Bot);
            PseudoClasses.Set(":pet", Kind == AvatarKind.Pet);
        }
        else if (change.Property == IsDimmedProperty)
        {
            PseudoClasses.Set(":dimmed", IsDimmed);
        }
        else if (change.Property == DisplayNameProperty)
        {
            string name = DisplayName?.Trim() ?? "";
            Initials = new string(name.Where(char.IsLetterOrDigit).Take(1).Select(char.ToUpperInvariant).ToArray());
            int tone = 0;
            foreach (char character in name)
                tone = unchecked(tone * 31 + char.ToLowerInvariant(character));
            tone = Math.Abs(tone % ToneCount);
            for (int index = 0; index < ToneCount; index++)
                PseudoClasses.Set(":tone" + index, index == tone);
        }
    }

    void OnImageChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RemoteImage.HasImageProperty)
            PseudoClasses.Set(":image", _image?.HasImage == true);
    }
}
