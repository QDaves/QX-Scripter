using Flazzy.ABC;

namespace Qx.Headers.Flash;

public enum MessageDirection
{
    Incoming,
    Outgoing
}

public enum NameSource
{
    None,
    ClassName,
    PrivateNamespace,
    ParserNamespace,
    ClassSignature,
    StructureHash,
    ReferenceClass,
    ReferenceId,
    ConstructorName
}

public sealed class FlashHeaderDefinition
{
    public required int Id { get; init; }
    public required MessageDirection Direction { get; init; }
    public required string Class { get; init; }
    public required string Namespace { get; init; }
    public string? Name { get; set; }
    public NameSource NameSource { get; set; } = NameSource.None;
    public string? Signature { get; set; }
    public IReadOnlyList<string> SemanticAliases { get; internal set; } = [];
    public string? ParserClass { get; set; }
    public string? ParserNamespace { get; set; }
    public bool ConstructorSignatureResolved { get; internal set; }
    public IReadOnlyList<string> ConstructorParameterTypes { get; internal set; } = [];
    internal ASMultiname? RegistrationType { get; init; }
    internal ABCFile? RegistrationAbc { get; init; }
    internal int RegistrationAbcIndex { get; init; } = -1;
    internal ASClass? RegistrationConfiguration { get; init; }
    internal ASMultiname? RegistrationField { get; init; }
    internal ASMultiname? ParserType { get; set; }
    internal int ParserAbcIndex { get; set; } = -1;
    internal IReadOnlyList<Avm2TypeDefinition> TypeDefinitions { get; init; } = [];

    public string Qualified => string.IsNullOrEmpty(Namespace) ? Class : $"{Namespace}.{Class}";
    public string? ParserQualified => ParserClass is null
        ? null
        : string.IsNullOrEmpty(ParserNamespace) ? ParserClass : $"{ParserNamespace}.{ParserClass}";
}

public sealed class FlashHeaderMap : IDisposable
{
    readonly object ownership_sync = new();
    SwfInfo? owned_swf;
    bool disposed;

    public string? ConfigClass { get; set; }
    public string? IncomingField { get; set; }
    public string? OutgoingField { get; set; }
    public int CandidateClassCount { get; set; }
    public int DuplicateRegistrationCount { get; set; }
    public int UnclassifiedRegistrationCount { get; set; }
    public string SourceSha256 { get; internal set; } = "";
    public IReadOnlyList<string> BuildIds { get; internal set; } = [];
    public List<FlashHeaderDefinition> Incoming { get; } = [];
    public List<FlashHeaderDefinition> Outgoing { get; } = [];
    public int Count => Incoming.Count + Outgoing.Count;
    public int NamedCount => Incoming.Count(message => message.Name is not null) +
        Outgoing.Count(message => message.Name is not null);
    internal Avm2CallTargetResolver? Types { get; set; }

    internal void Own(SwfInfo swf)
    {
        ArgumentNullException.ThrowIfNull(swf);
        lock (ownership_sync)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(FlashHeaderMap));
            if (owned_swf is not null)
                throw new InvalidOperationException("The flash header map already owns a SWF.");
            owned_swf = swf;
        }
    }

    public void Dispose()
    {
        SwfInfo? swf;
        lock (ownership_sync)
        {
            if (disposed)
                return;
            disposed = true;
            swf = owned_swf;
            owned_swf = null;
            Types = null;
        }
        swf?.Dispose();
    }
}
