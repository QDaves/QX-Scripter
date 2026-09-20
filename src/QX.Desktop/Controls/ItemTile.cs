using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":count", ":badge", ":caption")]
public sealed class ItemTile : TemplatedControl
{
    public static readonly StyledProperty<ImageRequest?> ImageProperty =
        AvaloniaProperty.Register<ItemTile, ImageRequest?>(nameof(Image));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<ItemTile, int>(nameof(Count), 1);

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<ItemTile, string?>(nameof(Badge));

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<ItemTile, string?>(nameof(Caption));

    public static readonly StyledProperty<double> ImageMaxWidthProperty =
        AvaloniaProperty.Register<ItemTile, double>(nameof(ImageMaxWidth), 48);

    public static readonly StyledProperty<double> ImageMaxHeightProperty =
        AvaloniaProperty.Register<ItemTile, double>(nameof(ImageMaxHeight), 48);

    public static readonly DirectProperty<ItemTile, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<ItemTile, string>(nameof(CountText), tile => tile.CountText);

    string _count_text = "";

    public ImageRequest? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public double ImageMaxWidth
    {
        get => GetValue(ImageMaxWidthProperty);
        set => SetValue(ImageMaxWidthProperty, value);
    }

    public double ImageMaxHeight
    {
        get => GetValue(ImageMaxHeightProperty);
        set => SetValue(ImageMaxHeightProperty, value);
    }

    public string CountText
    {
        get => _count_text;
        private set => SetAndRaise(CountTextProperty, ref _count_text, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty)
        {
            CountText = Count > 1 ? $"×{Count:N0}" : "";
            PseudoClasses.Set(":count", Count > 1);
        }
        else if (change.Property == BadgeProperty)
        {
            PseudoClasses.Set(":badge", !string.IsNullOrEmpty(Badge));
        }
        else if (change.Property == CaptionProperty)
        {
            PseudoClasses.Set(":caption", !string.IsNullOrEmpty(Caption));
        }
    }
}
