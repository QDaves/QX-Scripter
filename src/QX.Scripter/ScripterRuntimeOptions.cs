using Qx.Hosting;
using Qx.Mcp;
using Qx.Platform;

namespace Qx.Scripter;

public sealed record ScripterRuntimeOptions : GameHostOptions
{
    public string ScriptsDirectory { get; init; } = ScripterPaths.Scripts;

    public string SessionRulesPath { get; init; } = Path.Combine(ScripterPaths.Configuration, "rules.json");

    public bool EnableMcp { get; init; } = true;

    public int McpPort { get; init; } = 9390;

    public McpConfig? McpConfiguration { get; init; }

    public KeyboardReader? Keyboard { get; init; }

    internal void ValidateScripter()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ScriptsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(SessionRulesPath);
        if (McpPort is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(McpPort));
        _ = Path.GetFullPath(ScriptsDirectory);
        _ = Path.GetFullPath(SessionRulesPath);
    }
}
