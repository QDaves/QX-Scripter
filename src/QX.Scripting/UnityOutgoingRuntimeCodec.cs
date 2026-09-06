using Qx.Messages;
using Qx.Protocol;

namespace Qx.Scripting;

internal sealed class UnityOutgoingRuntimeCodec : UnityOutgoingCodec
{
    private readonly OutgoingMessageSchema _schema;

    private UnityOutgoingRuntimeCodec(OutgoingMessageSchema schema)
    {
        _schema = schema;
    }

    public OutgoingMessageSchema Schema => _schema;

    public static bool TryCreate(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas,
        out UnityOutgoingRuntimeCodec codec)
    {
        codec = null!;
        UnityOutgoingRuntimeCodec[] matches = schemas
            .Where(IsSupported)
            .Select(schema => new UnityOutgoingRuntimeCodec(schema))
            .Where(candidate => candidate.MatchesUnity(packet))
            .ToArray();
        if (matches.Length == 0)
            return false;

        UnityOutgoingRuntimeCodec[] distinct = matches
            .GroupBy(candidate => Signature(candidate._schema), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (distinct.Length != 1)
            return false;

        codec = distinct[0];
        return true;
    }

    public bool MatchesFlashSchemas(IReadOnlyList<OutgoingMessageSchema> flash_schemas) =>
        IsFlashCompatible(_schema, flash_schemas);

    public static bool TryCreateFlash(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas,
        IReadOnlyList<OutgoingMessageSchema> flash_schemas,
        out UnityOutgoingRuntimeCodec codec)
    {
        codec = null!;
        UnityOutgoingRuntimeCodec[] matches = schemas
            .Where(schema => IsSupported(schema) && IsFlashCompatible(schema, flash_schemas))
            .Select(schema => new UnityOutgoingRuntimeCodec(schema))
            .Where(candidate => candidate.MatchesFlash(packet))
            .ToArray();
        if (matches.Length == 0)
            return false;

        UnityOutgoingRuntimeCodec[] distinct = matches
            .GroupBy(candidate => Signature(candidate._schema), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (distinct.Length != 1)
            return false;

        codec = distinct[0];
        return true;
    }

    protected override object ReadUnity(in PacketReader reader) => Read(in reader, false);

    protected override object ReadFlash(in PacketReader reader) => Read(in reader, true);

    protected override void WriteUnity(in PacketWriter writer, object value) =>
        Write(in writer, (object?[])value, false);

    protected override void WriteFlash(in PacketWriter writer, object value) =>
        Write(in writer, (object?[])value, true);

    protected override object ToFlash(object native_value) => native_value;

    protected override object ToUnity(object native_value, object flash_value)
    {
        var original = (object?[])native_value;
        var changed = (object?[])flash_value;
        var merged = new object?[changed.Length];
        for (int index = 0; index < changed.Length; index++)
        {
            OutgoingParameterSchema parameter = _schema.Parameters[index];
            merged[index] = parameter.Collection is OutgoingCollectionKind.None
                ? Restore(parameter.WireType, original[index]!, changed[index]!)
                : RestoreCollection(
                    parameter.WireType,
                    (object?[])original[index]!,
                    (object?[])changed[index]!);
        }
        return merged;
    }

    protected override object[] ExportFlashValues(object flash_value)
    {
        var values = (object?[])flash_value;
        var exported = new object[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            OutgoingParameterSchema parameter = _schema.Parameters[index];
            if (parameter.Collection is OutgoingCollectionKind.None)
            {
                exported[index] = ExportScalar(parameter.WireType, values[index]!);
                continue;
            }

            var items = (object?[])values[index]!;
            exported[index] = items
                .Select(item => ExportScalar(parameter.WireType, item!))
                .ToArray();
        }
        return exported;
    }

    private object?[] Read(in PacketReader reader, bool flash_view)
    {
        var values = new object?[_schema.Parameters.Count];
        for (int index = 0; index < values.Length; index++)
        {
            OutgoingParameterSchema parameter = _schema.Parameters[index];
            if (parameter.Collection is OutgoingCollectionKind.None)
            {
                values[index] = ReadScalar(in reader, parameter.WireType, flash_view);
                continue;
            }

            int count = reader.ReadLength();
            var items = new object?[count];
            for (int item_index = 0; item_index < count; item_index++)
                items[item_index] = ReadScalar(in reader, parameter.WireType, flash_view);
            values[index] = items;
        }
        return values;
    }

    private void Write(in PacketWriter writer, object?[] values, bool flash_view)
    {
        if (values.Length != _schema.Parameters.Count)
            throw new InvalidDataException($"Expected {_schema.Parameters.Count} outgoing values, got {values.Length}.");

        for (int index = 0; index < values.Length; index++)
        {
            OutgoingParameterSchema parameter = _schema.Parameters[index];
            if (parameter.Collection is OutgoingCollectionKind.None)
            {
                WriteScalar(in writer, parameter.WireType, values[index]!, flash_view);
                continue;
            }

            var items = (object?[])values[index]!;
            writer.WriteLength((Length)items.Length);
            foreach (object? item in items)
                WriteScalar(in writer, parameter.WireType, item!, flash_view);
        }
    }

    private static object ReadScalar(
        in PacketReader reader,
        OutgoingWireType wire_type,
        bool flash_view)
    {
        if (flash_view && IsProjectedInteger(wire_type))
            return reader.ReadInt();

        return wire_type switch
        {
            OutgoingWireType.Boolean => reader.ReadBool(),
            OutgoingWireType.Int8 => unchecked((sbyte)reader.ReadByte()),
            OutgoingWireType.UInt8 => reader.ReadByte(),
            OutgoingWireType.Int16 => reader.ReadShort(),
            OutgoingWireType.UInt16 => unchecked((ushort)reader.ReadShort()),
            OutgoingWireType.Int32 => reader.ReadInt(),
            OutgoingWireType.UInt32 => unchecked((uint)reader.ReadInt()),
            OutgoingWireType.Int64 => reader.ReadLong(),
            OutgoingWireType.UInt64 => unchecked((ulong)reader.ReadLong()),
            OutgoingWireType.Float32 => reader.ReadFloat(),
            OutgoingWireType.Float64 => reader.ReadDouble(),
            OutgoingWireType.Character => ReadCharacter(in reader),
            OutgoingWireType.String => reader.ReadString(),
            _ => throw new InvalidOperationException($"Unsupported outgoing wire type '{wire_type}'.")
        };
    }

    private static void WriteScalar(
        in PacketWriter writer,
        OutgoingWireType wire_type,
        object value,
        bool flash_view)
    {
        if (flash_view && IsProjectedInteger(wire_type))
        {
            writer.WriteInt(ProjectInteger(wire_type, value));
            return;
        }

        switch (wire_type)
        {
            case OutgoingWireType.Boolean:
                writer.WriteBool((bool)value);
                break;
            case OutgoingWireType.Int8:
                writer.WriteByte(unchecked((byte)(sbyte)value));
                break;
            case OutgoingWireType.UInt8:
                writer.WriteByte((byte)value);
                break;
            case OutgoingWireType.Int16:
                writer.WriteShort((short)value);
                break;
            case OutgoingWireType.UInt16:
                writer.WriteShort(unchecked((short)(ushort)value));
                break;
            case OutgoingWireType.Int32:
                writer.WriteInt((int)value);
                break;
            case OutgoingWireType.UInt32:
                writer.WriteInt(unchecked((int)(uint)value));
                break;
            case OutgoingWireType.Int64:
                writer.WriteLong((long)value);
                break;
            case OutgoingWireType.UInt64:
                writer.WriteLong(unchecked((long)(ulong)value));
                break;
            case OutgoingWireType.Float32:
                writer.WriteFloat((float)value);
                break;
            case OutgoingWireType.Float64:
                writer.WriteDouble((double)value);
                break;
            case OutgoingWireType.Character:
                writer.WriteString(((char)value).ToString());
                break;
            case OutgoingWireType.String:
                writer.WriteString((string)value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported outgoing wire type '{wire_type}'.");
        }
    }

    private static object Restore(OutgoingWireType wire_type, object original, object changed)
    {
        if (!IsProjectedInteger(wire_type))
            return changed;

        int projected = (int)changed;
        return wire_type switch
        {
            OutgoingWireType.Int8 when projected is >= sbyte.MinValue and <= sbyte.MaxValue => (sbyte)projected,
            OutgoingWireType.UInt8 when projected is >= byte.MinValue and <= byte.MaxValue => (byte)projected,
            OutgoingWireType.Int16 when projected is >= short.MinValue and <= short.MaxValue => (short)projected,
            OutgoingWireType.UInt16 when projected is >= ushort.MinValue and <= ushort.MaxValue => (ushort)projected,
            OutgoingWireType.Int32 => projected,
            OutgoingWireType.UInt32 => unchecked((uint)projected),
            OutgoingWireType.Int64 => RestoreInt64((long)original, projected),
            OutgoingWireType.UInt64 => RestoreUInt64((ulong)original, projected),
            _ => throw new InvalidOperationException($"Flash integer {projected} is outside the Unity '{wire_type}' range.")
        };
    }

    private static object?[] RestoreCollection(
        OutgoingWireType wire_type,
        object?[] original,
        object?[] changed)
    {
        if (!IsProjectedInteger(wire_type))
            return [.. changed];

        if (wire_type is OutgoingWireType.Int64 or OutgoingWireType.UInt64)
            return RestoreProjectedCollection(wire_type, original, changed);

        var merged = new object?[changed.Length];
        for (int index = 0; index < changed.Length; index++)
            merged[index] = Restore(wire_type, DefaultInteger(wire_type), changed[index]!);
        return merged;
    }

    private static object?[] RestoreProjectedCollection(
        OutgoingWireType wire_type,
        object?[] original,
        object?[] changed)
    {
        var originals_by_projection = new Dictionary<int, List<object>>();
        var original_counts = new Dictionary<int, int>();
        foreach (object? value in original)
        {
            object item = value!;
            int projection = ProjectInteger(wire_type, item);
            original_counts[projection] = original_counts.GetValueOrDefault(projection) + 1;
            if (!originals_by_projection.TryGetValue(projection, out List<object>? candidates))
            {
                candidates = [];
                originals_by_projection.Add(projection, candidates);
            }
            if (!candidates.Contains(item))
                candidates.Add(item);
        }

        foreach (IGrouping<int, object?> group in changed.GroupBy(value => (int)value!))
        {
            if (!originals_by_projection.TryGetValue(group.Key, out List<object>? candidates) ||
                candidates.All(candidate => IsNativeProjectionValue(wire_type, candidate, group.Key)))
                continue;
            if (group.Count() > original_counts[group.Key])
                throw new InvalidOperationException($"Flash integer projection {group.Key} has an ambiguous changed multiplicity.");
        }

        var restored = new object?[changed.Length];
        for (int index = 0; index < changed.Length; index++)
        {
            int projection = (int)changed[index]!;
            if (!originals_by_projection.TryGetValue(projection, out List<object>? candidates))
            {
                restored[index] = Restore(wire_type, DefaultInteger(wire_type), changed[index]!);
                continue;
            }
            if (candidates.Count != 1)
                throw new InvalidOperationException($"Flash integer projection {projection} matches multiple Unity values.");
            restored[index] = Restore(wire_type, candidates[0], changed[index]!);
        }
        return restored;
    }

    private static long RestoreInt64(long original, int changed) =>
        unchecked((int)original) == changed ? original : changed;

    private static ulong RestoreUInt64(ulong original, int changed) =>
        unchecked((int)original) == changed ? original : unchecked((uint)changed);

    private static bool IsNativeProjectionValue(OutgoingWireType wire_type, object value, int projection) => wire_type switch
    {
        OutgoingWireType.Int64 => (long)value == projection,
        OutgoingWireType.UInt64 => (ulong)value == unchecked((uint)projection),
        _ => true
    };

    private static int ProjectInteger(OutgoingWireType wire_type, object value) => wire_type switch
    {
        OutgoingWireType.Int8 => (sbyte)value,
        OutgoingWireType.UInt8 => (byte)value,
        OutgoingWireType.Int16 => (short)value,
        OutgoingWireType.UInt16 => (ushort)value,
        OutgoingWireType.Int32 => (int)value,
        OutgoingWireType.UInt32 => unchecked((int)(uint)value),
        OutgoingWireType.Int64 => unchecked((int)(long)value),
        OutgoingWireType.UInt64 => unchecked((int)(ulong)value),
        _ => throw new ArgumentOutOfRangeException(nameof(wire_type))
    };

    private static object DefaultInteger(OutgoingWireType wire_type) => wire_type switch
    {
        OutgoingWireType.Int8 => (sbyte)0,
        OutgoingWireType.UInt8 => (byte)0,
        OutgoingWireType.Int16 => (short)0,
        OutgoingWireType.UInt16 => (ushort)0,
        OutgoingWireType.Int32 => 0,
        OutgoingWireType.UInt32 => 0U,
        OutgoingWireType.Int64 => 0L,
        OutgoingWireType.UInt64 => 0UL,
        _ => throw new ArgumentOutOfRangeException(nameof(wire_type))
    };

    private static char ReadCharacter(in PacketReader reader)
    {
        string value = reader.ReadString();
        if (value.Length != 1)
            throw new InvalidDataException("Outgoing character field must contain exactly one character.");
        return value[0];
    }

    private static bool IsProjectedInteger(OutgoingWireType wire_type) => wire_type is
        OutgoingWireType.Int8 or
        OutgoingWireType.UInt8 or
        OutgoingWireType.Int16 or
        OutgoingWireType.UInt16 or
        OutgoingWireType.Int32 or
        OutgoingWireType.UInt32 or
        OutgoingWireType.Int64 or
        OutgoingWireType.UInt64;

    private static bool IsSupported(OutgoingMessageSchema schema) =>
        schema.Parameters.All(parameter =>
            parameter.WireType is not OutgoingWireType.Unknown and not OutgoingWireType.Decimal);

    private static bool IsFlashCompatible(
        OutgoingMessageSchema schema,
        IReadOnlyList<OutgoingMessageSchema> flash_schemas) =>
        flash_schemas.Any(flash_schema => SameFlashShape(schema, flash_schema));

    private static bool SameFlashShape(
        OutgoingMessageSchema unity_schema,
        OutgoingMessageSchema flash_schema)
    {
        if (unity_schema.Parameters.Count != flash_schema.Parameters.Count)
            return false;

        for (int index = 0; index < unity_schema.Parameters.Count; index++)
        {
            OutgoingParameterSchema unity = unity_schema.Parameters[index];
            OutgoingParameterSchema flash = flash_schema.Parameters[index];
            if ((unity.Collection is OutgoingCollectionKind.None) !=
                (flash.Collection is OutgoingCollectionKind.None) ||
                ProjectedFlashWireType(unity.WireType) != DeclaredFlashWireType(flash.WireType))
                return false;
        }
        return true;
    }

    private static OutgoingWireType ProjectedFlashWireType(OutgoingWireType wire_type) => wire_type switch
    {
        OutgoingWireType.Int8 or
        OutgoingWireType.UInt8 or
        OutgoingWireType.Int16 or
        OutgoingWireType.UInt16 or
        OutgoingWireType.Int32 or
        OutgoingWireType.UInt32 or
        OutgoingWireType.Int64 or
        OutgoingWireType.UInt64 => OutgoingWireType.Int32,
        OutgoingWireType.Character => OutgoingWireType.String,
        _ => wire_type
    };

    private static OutgoingWireType DeclaredFlashWireType(OutgoingWireType wire_type) => wire_type switch
    {
        OutgoingWireType.Int32 or OutgoingWireType.UInt32 => OutgoingWireType.Int32,
        OutgoingWireType.Character => OutgoingWireType.String,
        _ => wire_type
    };

    private static object ExportScalar(OutgoingWireType wire_type, object value) => wire_type switch
    {
        OutgoingWireType.UInt32 => unchecked((uint)(int)value),
        OutgoingWireType.UInt64 => unchecked((ulong)(uint)(int)value),
        _ => value
    };

    private static string Signature(OutgoingMessageSchema schema) => string.Join(
        ';',
        schema.Parameters.Select(parameter => $"{(int)parameter.WireType}:{(int)parameter.Collection}"));
}
