using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qx.Mcp;

/// <summary>
/// Persisted MCP transport configuration: the shared access token every request must present
/// and the capability flags that gate code execution, script-file writes and editor access.
/// </summary>
public sealed record McpConfig
{
    /// <summary>Entropy of a generated token, in bytes.</summary>
    public const int TokenByteLength = 24;

    private const int MinimumTokenLength = 32;
    private const string ConfigDirectoryName = "QX Scripter";
    private const string ConfigFileName = "mcp.json";

    private static readonly JsonSerializerOptions FileOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The shared secret presented as <c>X-MCP-Token</c>, <c>Authorization: Bearer</c> or <c>?token=</c>.</summary>
    public required string Token { get; init; }

    /// <summary>When false the token check is skipped entirely; any local process regains full access.</summary>
    public bool RequireAuth { get; init; } = true;

    /// <summary>Allows the tools that compile and run arbitrary C# in this process.</summary>
    public bool AllowExecute { get; init; } = true;

    /// <summary>Allows the tools that create, overwrite, rename or delete files in the script library.</summary>
    public bool AllowFileWrite { get; init; } = true;

    /// <summary>Allows the tools that read and mutate the editor tabs.</summary>
    public bool AllowEditor { get; init; } = true;

    /// <summary>The default configuration file, <c>%APPDATA%/QX Scripter/mcp.json</c>.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ConfigDirectoryName,
        ConfigFileName);

    /// <summary>Generates a fresh CSPRNG token as lowercase hex.</summary>
    public static string CreateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenByteLength)).ToLowerInvariant();

    /// <summary>Creates an unsaved configuration with a fresh token and the default capability flags.</summary>
    public static McpConfig CreateDefault() => new() { Token = CreateToken() };

    /// <summary>
    /// Reads the configuration, creating it with a fresh token when it is missing, unreadable or
    /// carries an unusable token. Capability flags of a readable file are always preserved.
    /// </summary>
    public static McpConfig Load(string? path = null)
    {
        string file = Resolve(path);
        McpConfig? stored = Read(file);
        if (stored is not null && IsUsableToken(stored.Token))
            return stored;

        McpConfig created = (stored ?? CreateDefault()) with { Token = CreateToken() };
        TrySave(created, file);
        return created;
    }

    /// <summary>Writes the configuration atomically, creating the containing directory when needed.</summary>
    public void Save(string? path = null)
    {
        string file = Resolve(path);
        string? directory = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporary = file + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(
                new StoredConfig(Token, RequireAuth, AllowExecute, AllowFileWrite, AllowEditor),
                FileOptions));
        RestrictToOwner(temporary);
        File.Move(temporary, file, overwrite: true);
    }

    /// <summary>Compares a presented token with the configured one in constant time.</summary>
    public bool MatchesToken(string? presented)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(Token))
            return false;

        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(Token), expected);
        SHA256.HashData(Encoding.UTF8.GetBytes(presented), actual);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>True when every capability the tool requires is enabled.</summary>
    public bool Allows(McpCapability capability) =>
        MissingCapabilities(capability).Count == 0;

    /// <summary>The configuration flag names a tool needs but does not have.</summary>
    public IReadOnlyList<string> MissingCapabilities(McpCapability capability)
    {
        var missing = new List<string>(3);
        if (capability.HasFlag(McpCapability.Execute) && !AllowExecute)
            missing.Add("allowExecute");
        if (capability.HasFlag(McpCapability.FileWrite) && !AllowFileWrite)
            missing.Add("allowFileWrite");
        if (capability.HasFlag(McpCapability.Editor) && !AllowEditor)
            missing.Add("allowEditor");
        return missing;
    }

    private static string Resolve(string? path) =>
        string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);

    private static McpConfig? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            StoredConfig? stored = JsonSerializer.Deserialize<StoredConfig>(
                File.ReadAllText(path),
                FileOptions);
            if (stored is null)
                return null;

            return new McpConfig
            {
                Token = stored.Token ?? "",
                RequireAuth = stored.RequireAuth ?? true,
                AllowExecute = stored.AllowExecute ?? true,
                AllowFileWrite = stored.AllowFileWrite ?? true,
                AllowEditor = stored.AllowEditor ?? true
            };
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static void TrySave(McpConfig config, string path)
    {
        try
        {
            config.Save(path);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static bool IsUsableToken(string token)
    {
        if (token.Length < MinimumTokenLength)
            return false;

        foreach (char character in token)
        {
            if (!char.IsAsciiLetterOrDigit(character))
                return false;
        }
        return true;
    }

    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    private sealed record StoredConfig(
        string? Token,
        bool? RequireAuth,
        bool? AllowExecute,
        bool? AllowFileWrite,
        bool? AllowEditor);
}
