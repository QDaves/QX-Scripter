namespace Flazzy.ABC;

public enum ConstantKind : byte
{
    Null = 0x0C,
    Undefined = 0x00,

    String = 0x01,
    Float = 0x02,
    Double = 0x06,
    Integer = 0x03,
    UInteger = 0x04,
    Float4 = 0x1E,

    True = 0x0B,
    False = 0x0A,

    PrivateNs = 0x05,
    Namespace = 0x08,
    PackageNamespace = 0x16,
    PackageInternalNs = 0x17,
    ProtectedNs = 0x18,
    ExplicitNamespace = 0x19,
    StaticProtectedNs = 0x1A
}
