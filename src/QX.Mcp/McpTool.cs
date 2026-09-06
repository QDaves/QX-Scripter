using System.Text.Json;
using System.Collections.ObjectModel;

namespace Qx.Mcp;

public sealed class McpTool
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public required string Description { get; init; }
    public required object InputSchema { get; init; }
    public object? OutputSchema { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
    public required McpToolAnnotations Annotations { get; init; }
    public required Func<JsonElement, CancellationToken, Task<string>> Handler { get; init; }

    /// <summary>Capabilities the configuration must grant before this tool may run.</summary>
    public McpCapability Capability { get; init; } = McpCapability.None;
    public McpRuntimeCapability RuntimeCapability { get; init; } = McpRuntimeCapability.None;

    /// <summary>
    /// Resolves the per-request deadline in milliseconds from the call arguments.
    /// A tool without a selector, or one returning zero, runs without a deadline.
    /// </summary>
    public Func<JsonElement, int>? Timeout { get; init; }
}

/// <summary>Privileges a tool needs, gated by the persisted <see cref="McpConfig"/> flags.</summary>
[Flags]
public enum McpCapability
{
    None = 0,

    /// <summary>Compiles and runs arbitrary C# inside the QX process.</summary>
    Execute = 1,

    /// <summary>Creates, overwrites, renames or deletes files in the script library.</summary>
    FileWrite = 2,

    /// <summary>Reads or mutates the editor tabs.</summary>
    Editor = 4
}

[Flags]
public enum McpRuntimeCapability
{
    None = 0,
    Editor = 1
}

public sealed record McpToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);

public sealed class McpToolException : Exception
{
    public McpToolException(
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null) : base(message)
    {
        Metadata = metadata is null
            ? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>())
            : new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(metadata, StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, object?> Metadata { get; }
}
