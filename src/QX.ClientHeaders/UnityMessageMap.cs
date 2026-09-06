using System.Text.Json.Serialization;

namespace Qx.Unity;

public enum UnityHeaderDirection
{
    Incoming,
    Outgoing
}

[method: JsonConstructor]
public sealed record UnityHeaderDefinition(
    short Id,
    string? Name,
    string? FlashName,
    string SourceName,
    int Ordinal)
{
    public UnityHeaderDefinition(short id, string? name, string source_name, int ordinal)
        : this(id, name, null, source_name, ordinal)
    {
    }

    public bool IsNamed => Name is not null || FlashName is not null;
    public bool IsUnityNamed => Name is not null;
    public bool IsFlashNamed => FlashName is not null;
}

public sealed class UnityMessageMap
{
    public required int MetadataVersion { get; init; }
    public required string MetadataSha256 { get; init; }
    public required long MetadataLength { get; init; }
    public required string IncomingEnum { get; init; }
    public required string OutgoingEnum { get; init; }
    public required string DirectionEvidence { get; init; }
    public required bool DirectionsVerified { get; init; }
    public required double IncomingInventorySimilarity { get; init; }
    public required double OutgoingInventorySimilarity { get; init; }
    public required int IncomingReferenceCount { get; init; }
    public required int OutgoingReferenceCount { get; init; }
    public required int IncomingReferenceMatchCount { get; init; }
    public required int OutgoingReferenceMatchCount { get; init; }
    public required IReadOnlyList<UnityHeaderDefinition> Incoming { get; init; }
    public required IReadOnlyList<UnityHeaderDefinition> Outgoing { get; init; }

    public int NamedIncomingCount => Incoming.Count(header => header.IsNamed);
    public int NamedOutgoingCount => Outgoing.Count(header => header.IsNamed);
    public int UnityNamedIncomingCount => Incoming.Count(header => header.IsUnityNamed);
    public int UnityNamedOutgoingCount => Outgoing.Count(header => header.IsUnityNamed);
    public int FlashNamedIncomingCount => Incoming.Count(header => header.IsFlashNamed);
    public int FlashNamedOutgoingCount => Outgoing.Count(header => header.IsFlashNamed);
    public double IncomingReferenceCoverage => Ratio(IncomingReferenceMatchCount, IncomingReferenceCount);
    public double OutgoingReferenceCoverage => Ratio(OutgoingReferenceMatchCount, OutgoingReferenceCount);

    [JsonIgnore]
    public double IncomingConfidence => IncomingInventorySimilarity;

    [JsonIgnore]
    public double OutgoingConfidence => OutgoingInventorySimilarity;

    static double Ratio(int value, int total) => total == 0 ? 0 : (double)value / total;
}
