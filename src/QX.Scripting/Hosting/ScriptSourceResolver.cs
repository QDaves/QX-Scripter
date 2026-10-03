using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Resolves <c>#load</c> paths against the script library and can serve loaded files from a
/// snapshot, so a script compiles against the exact text its loaded files were rewritten from.
/// </summary>
internal sealed class ScriptSourceResolver : SourceReferenceResolver
{
    private readonly SourceFileResolver _files;
    private readonly IReadOnlyDictionary<string, string> _snapshot;

    public ScriptSourceResolver(string? directory, IReadOnlyDictionary<string, string>? snapshot = null)
    {
        _files = new SourceFileResolver([], directory);
        _snapshot = snapshot ?? new Dictionary<string, string>();
    }

    public override string? NormalizePath(string path, string? baseFilePath) =>
        _files.NormalizePath(path, baseFilePath);

    public override string? ResolveReference(string path, string? baseFilePath) =>
        _files.ResolveReference(path, baseFilePath);

    public override Stream OpenRead(string resolvedPath) =>
        _snapshot.TryGetValue(resolvedPath, out string? text)
            ? new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false)
            : _files.OpenRead(resolvedPath);

    public override SourceText ReadText(string resolvedPath) =>
        _snapshot.TryGetValue(resolvedPath, out string? text)
            ? SourceText.From(text, Encoding.UTF8)
            : _files.ReadText(resolvedPath);

    public override bool Equals(object? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
