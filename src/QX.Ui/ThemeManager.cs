using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Qx.Ui;

public sealed class ThemeManager(UiSettings settings)
{
    private readonly UiSettings _settings = settings;

    private static readonly Color DarkAccent = Color.FromRgb(0x9B, 0x8C, 0xFF);
    private static readonly Color LightAccent = Color.FromRgb(0x4D, 0x5B, 0xA6);
    private static readonly Color DarkSuccess = Color.FromRgb(0x59, 0xD7, 0x7A);
    private static readonly Color LightSuccess = Color.FromRgb(0x23, 0x7A, 0x3B);
    private static readonly Color DarkDanger = Color.FromRgb(0xFF, 0x76, 0x65);
    private static readonly Color LightDanger = Color.FromRgb(0xB9, 0x36, 0x2B);
    private static readonly Color DarkWarning = Color.FromRgb(0xFF, 0xB4, 0x54);
    private static readonly Color LightWarning = Color.FromRgb(0x8A, 0x56, 0x00);

    public static readonly Color Primary = DarkAccent;
    public static readonly Color Secondary = DarkWarning;

    private readonly PaletteHelper _palette = new();

    public bool IsDark { get; private set; } = true;

    public void Load() => Apply(_settings.Dark);

    public void Toggle() => Apply(!IsDark);

    public void Apply(bool dark)
    {
        IsDark = dark;

        Color accent = dark ? DarkAccent : LightAccent;
        Color warning = dark ? DarkWarning : LightWarning;
        Theme theme = _palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(accent);
        theme.SetSecondaryColor(warning);
        _palette.SetTheme(theme);
        ApplyResources(dark);

        Save();
    }

    private static void ApplyResources(bool dark)
    {
        Color accent = dark ? DarkAccent : LightAccent;
        Color success = dark ? DarkSuccess : LightSuccess;
        Color danger = dark ? DarkDanger : LightDanger;
        Color warning = dark ? DarkWarning : LightWarning;
        byte softOpacity = dark ? (byte)0x32 : (byte)0x1D;

        SetGradientBrush(
            "MaterialDesign.Brush.Background",
            dark ? Color.FromRgb(0x1B, 0x1A, 0x21) : Color.FromRgb(0xFA, 0xFB, 0xFD),
            dark ? Color.FromRgb(0x24, 0x21, 0x32) : Color.FromRgb(0xE7, 0xED, 0xF5));
        SetBrush("MaterialDesign.Brush.Card.Background", dark
            ? Color.FromRgb(0x27, 0x24, 0x31)
            : Color.FromRgb(0xFF, 0xFF, 0xFF));
        SetBrush("MaterialDesign.Brush.Paper", dark
            ? Color.FromRgb(0x25, 0x22, 0x2E)
            : Color.FromRgb(0xF4, 0xF6, 0xFA));
        SetBrush("MaterialDesign.Brush.Foreground", dark
            ? Color.FromRgb(0xF4, 0xF2, 0xF8)
            : Color.FromRgb(0x24, 0x28, 0x33));
        SetBrush("MaterialDesign.Brush.Divider", dark
            ? Color.FromRgb(0x40, 0x3B, 0x4B)
            : Color.FromRgb(0xCB, 0xD3, 0xDF));

        SetBrush("QxAccentBrush", accent);
        SetBrush("QxAccentSoftBrush", WithOpacity(accent, softOpacity));
        SetBrush("QxSelectionBrush", dark
            ? Color.FromRgb(0x39, 0x34, 0x49)
            : Color.FromRgb(0xDC, 0xE4, 0xEF));
        SetBrush("QxSuccessBrush", success);
        SetBrush("QxSuccessSoftBrush", WithOpacity(success, dark ? (byte)0x24 : (byte)0x18));
        SetBrush("QxDangerBrush", danger);
        SetBrush("QxDangerSoftBrush", WithOpacity(danger, dark ? (byte)0x24 : (byte)0x18));
        SetBrush("QxWarningBrush", warning);
        SetBrush("QxSurfaceSubtleBrush", dark
            ? Color.FromArgb(0x18, 0xCE, 0xC4, 0xE8)
            : Color.FromArgb(0x12, 0x4D, 0x5B, 0x70));
        SetBrush("QxTextSecondaryBrush", dark
            ? Color.FromRgb(0xD4, 0xD0, 0xDF)
            : Color.FromRgb(0x4D, 0x55, 0x64));
        SetBrush("QxTextMutedBrush", dark
            ? Color.FromRgb(0x9D, 0x97, 0xAA)
            : Color.FromRgb(0x70, 0x79, 0x88));
        SetBrush("ConnectedBrush", success);
        SetBrush("DangerBrush", danger);
    }

    private static Color WithOpacity(Color color, byte opacity) =>
        Color.FromArgb(opacity, color.R, color.G, color.B);

    private static void SetBrush(string key, Color color)
    {
        if (Application.Current is null)
            return;

        if (Application.Current.Resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetGradientBrush(string key, Color start, Color end)
    {
        if (Application.Current is null)
            return;

        Application.Current.Resources[key] = new LinearGradientBrush(
            start,
            end,
            new Point(0, 0),
            new Point(1, 1));
    }

    private void Save() => _settings.Dark = IsDark;
}
