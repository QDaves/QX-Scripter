using Qx.Messages;

namespace Qx.Protocol;

public static class OutgoingSchemaMatcher
{
    public static bool TryMatch(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas,
        out bool has_supported_schema)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(schemas);
        has_supported_schema = false;
        foreach (OutgoingMessageSchema schema in schemas)
        {
            if (schema.Parameters.Any(parameter => !IsSupported(parameter)))
                continue;
            has_supported_schema = true;
            try
            {
                int position = 0;
                var reader = new PacketReader(packet, ref position);
                foreach (OutgoingParameterSchema parameter in schema.Parameters)
                {
                    if (parameter.Collection is OutgoingCollectionKind.None)
                    {
                        ReadScalar(in reader, parameter.WireType);
                        continue;
                    }

                    int count = reader.ReadLength();
                    for (int index = 0; index < count; index++)
                    {
                        if (parameter.ElementWireTypes is { Count: > 0 })
                        {
                            foreach (OutgoingWireType element_type in parameter.ElementWireTypes)
                                ReadScalar(in reader, element_type);
                        }
                        else
                        {
                            ReadScalar(in reader, parameter.WireType);
                        }
                    }
                }
                if (reader.Available == 0)
                    return true;
            }
            catch (Exception error) when (error is IndexOutOfRangeException or InvalidDataException)
            {
            }
        }
        return false;
    }

    private static bool IsSupported(OutgoingParameterSchema parameter)
    {
        if (parameter.WireType is OutgoingWireType.Decimal)
            return false;
        if (parameter.WireType is not OutgoingWireType.Unknown)
            return true;
        return parameter.Collection is not OutgoingCollectionKind.None &&
            parameter.ElementWireTypes is { Count: > 0 } element_types &&
            element_types.All(type =>
                type is not OutgoingWireType.Unknown and not OutgoingWireType.Decimal);
    }

    private static void ReadScalar(in PacketReader reader, OutgoingWireType wire_type)
    {
        switch (wire_type)
        {
            case OutgoingWireType.Boolean:
                if (reader.ReadByte() > 1)
                    throw new InvalidDataException("Boolean wire value is outside its native range.");
                break;
            case OutgoingWireType.Int8:
            case OutgoingWireType.UInt8:
                reader.ReadByte();
                break;
            case OutgoingWireType.Int16:
            case OutgoingWireType.UInt16:
                reader.ReadShort();
                break;
            case OutgoingWireType.Int32:
            case OutgoingWireType.UInt32:
                reader.ReadInt();
                break;
            case OutgoingWireType.Int64:
            case OutgoingWireType.UInt64:
                reader.ReadLong();
                break;
            case OutgoingWireType.Float32:
                reader.ReadFloat();
                break;
            case OutgoingWireType.Float64:
                reader.ReadDouble();
                break;
            case OutgoingWireType.Character:
                if (reader.ReadString().Length != 1)
                    throw new InvalidDataException("Character wire value must contain one character.");
                break;
            case OutgoingWireType.String:
                reader.ReadString();
                break;
            default:
                throw new InvalidDataException($"Unsupported outgoing wire type '{wire_type}'.");
        }
    }
}
