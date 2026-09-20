namespace Qx.Presentation.Services.Settings;

public interface ISettingsStore
{
    SettingsDocument Current { get; }

    event Action<SettingsDocument>? Changed;

    void Update(Func<SettingsDocument, SettingsDocument> change);

    ScriptPanelMemory? PanelFor(string path);

    void RememberPanel(string path, bool panel, IReadOnlyDictionary<string, string>? values);

    void ForgetPanel(string path);

    Task FlushAsync(CancellationToken cancellation_token);

    bool FlushNow(TimeSpan budget);
}
