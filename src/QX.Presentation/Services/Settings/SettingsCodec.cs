using System.Text.Json;

namespace Qx.Presentation.Services.Settings;

public static class SettingsCodec
{
    public static SettingsDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, SettingsJson.Default.SettingsDocument) ?? new SettingsDocument();
    }

    public static string Write(SettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, SettingsJson.Default.SettingsDocument);
    }

    public static ThemeMode EffectiveTheme(SettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Theme ?? (document.Dark ? ThemeMode.Dark : ThemeMode.Light);
    }
}
