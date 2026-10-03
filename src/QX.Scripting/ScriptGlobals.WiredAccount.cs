using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>Gets cached account preferences, or null before receipt or after disconnection. Sends nothing.</summary>
    /// <remarks>Account preferences survive room changes. Optional null fields were omitted by the hotel.</remarks>
    public AccountPreferences? WiredAccountPreferences =>
        _application.Invoke<WiredCommandRequest, WiredAccountPreferencesView>(
            ApplicationMemberIds.WiredPreferencesGet, new WiredCommandRequest(), Ct).Preferences;

    /// <summary>Generates a Web API key and waits for the matching Wired ID and access type.</summary>
    /// <param name="wiredId">The Wired furniture ID.</param>
    /// <param name="readKey">True for a read key; false for a write key.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The generated key and its identity. The key is not retained in room state.</returns>
    public Task<WiredWebApiKeyResult> GenerateWiredWebApiKey(Id wiredId, bool readKey, int timeoutMs = 10000) =>
        wired_call<WiredWebApiKeyRequest, WiredWebApiKeyResult>(
            ApplicationMemberIds.WiredWebApiKeyGenerate, new WiredWebApiKeyRequest(wiredId, readKey, timeoutMs));
}
