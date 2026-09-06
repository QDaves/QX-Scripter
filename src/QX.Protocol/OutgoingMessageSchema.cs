namespace Qx.Protocol;

public enum OutgoingWireType
{
    Unknown,
    Boolean,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float32,
    Float64,
    Decimal,
    Character,
    String
}

public enum OutgoingCollectionKind
{
    None,
    List,
    Array
}

public sealed record OutgoingParameterSchema(
    int Position,
    string SourceType,
    string Name,
    string? DefaultValue,
    OutgoingWireType WireType,
    OutgoingCollectionKind Collection,
    IReadOnlyList<OutgoingWireType>? ElementWireTypes = null);

public sealed record OutgoingMessageSchema(
    string SourceName,
    IReadOnlyList<OutgoingParameterSchema> Parameters);
