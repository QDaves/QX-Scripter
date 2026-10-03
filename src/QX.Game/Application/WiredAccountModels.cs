using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>The account preferences cached during the current hotel connection.</summary>
/// <param name="Preferences">The last received preferences, or null before receipt or after disconnection.</param>
public sealed record WiredAccountPreferencesView(AccountPreferences? Preferences);

/// <summary>Requests a generated Web API key for a Wired.</summary>
/// <param name="WiredId">The Wired furniture ID.</param>
/// <param name="ReadKey">True for a read key; false for a write key.</param>
/// <param name="TimeoutMilliseconds">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
public sealed record WiredWebApiKeyRequest(Id WiredId, bool ReadKey, int TimeoutMilliseconds = 10000);
