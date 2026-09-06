using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Qx.Unity;

internal sealed record Il2CppEnumMember(string Name, short Value, int Ordinal);

internal sealed record Il2CppEnumDefinition(
    string Name,
    string Namespace,
    int TypeIndex,
    IReadOnlyList<Il2CppEnumMember> Members)
{
    public string QualifiedName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
}

internal sealed record Il2CppProtocolEnumPair(
    int IncomingTypeIndex,
    int OutgoingTypeIndex,
    string AttributeType,
    int IncomingConstructorCount,
    int PairConstructorCount);

internal sealed class Il2CppMetadataReader
{
    const uint MetadataMagic = 0xFAB11BAF;
    const int MaximumMetadataBytes = 536_870_912;
    const int HeaderSize = 256;
    const int TypeDefinitionSize = 88;
    const int FieldDefinitionSize = 12;
    const int FieldDefaultValueSize = 12;
    const int ParameterDefinitionSize = 12;
    const int ImageDefinitionSize = 40;
    static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    readonly byte[] _data;
    readonly Region _strings;
    readonly Region _type_definitions;
    readonly Region _methods;
    readonly Region _parameters;
    readonly Region _fields;
    readonly Region _field_defaults;
    readonly Region _default_data;
    readonly Dictionary<int, int> _default_offsets;
    readonly int _method_definition_size;

    Il2CppMetadataReader(byte[] data)
    {
        _data = data;
        if (_data.Length < HeaderSize)
            throw new InvalidDataException("IL2CPP metadata header is truncated.");
        if (ReadUInt32(0) != MetadataMagic)
            throw new InvalidDataException("File is not IL2CPP global metadata.");

        Version = ReadInt32(4);
        if (Version is < 27 or > 64)
            throw new NotSupportedException($"IL2CPP metadata version {Version} is not supported.");

        _strings = ReadRegion(24, "metadata strings");
        _methods = ReadRegion(48, "methods");
        _field_defaults = ReadRegion(64, "field defaults");
        _default_data = ReadRegion(72, "default values");
        _parameters = ReadRegion(88, "parameters");
        _fields = ReadRegion(96, "fields");
        _type_definitions = ReadRegion(160, "type definitions");
        _method_definition_size = Version >= 31 ? 36 : 32;

        RequireDivisible(_methods, _method_definition_size);
        RequireDivisible(_field_defaults, FieldDefaultValueSize);
        RequireDivisible(_parameters, ParameterDefinitionSize);
        RequireDivisible(_fields, FieldDefinitionSize);
        RequireDivisible(_type_definitions, TypeDefinitionSize);

        _default_offsets = new Dictionary<int, int>(_field_defaults.Length / FieldDefaultValueSize);
        for (int offset = _field_defaults.Offset; offset < _field_defaults.End; offset += FieldDefaultValueSize)
        {
            int field_index = ReadInt32(offset);
            int data_index = ReadInt32(offset + 8);
            if (field_index < 0 || data_index < 0 || data_index > _default_data.Length - sizeof(short))
                continue;
            _default_offsets.TryAdd(field_index, data_index);
        }
    }

    public int Version { get; }
    public long Length => _data.LongLength;
    public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(_data));

    public static Il2CppMetadataReader Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("IL2CPP global metadata was not found.", path);
        if (file.Length is < HeaderSize or > MaximumMetadataBytes)
            throw new InvalidDataException($"Invalid IL2CPP metadata length: {file.Length} bytes.");
        return new Il2CppMetadataReader(UnityBoundedFile.ReadAllBytes(path, MaximumMetadataBytes));
    }

    public IReadOnlyList<Il2CppEnumDefinition> ReadEnums(int minimum_members)
    {
        if (minimum_members < 1)
            throw new ArgumentOutOfRangeException(nameof(minimum_members));

        int field_total = _fields.Length / FieldDefinitionSize;
        var definitions = new List<Il2CppEnumDefinition>();
        int type_index = 0;
        for (int offset = _type_definitions.Offset; offset < _type_definitions.End; offset += TypeDefinitionSize, type_index++)
        {
            uint bitfield = ReadUInt32(offset + 80);
            if (((bitfield >> 1) & 1) == 0)
                continue;

            int field_start = ReadInt32(offset + 32);
            int field_count = ReadUInt16(offset + 68);
            if (field_start < 0 || field_count < minimum_members || field_start > field_total - field_count)
                continue;

            var members = new List<Il2CppEnumMember>(field_count);
            int ordinal = 0;
            for (int field_index = field_start; field_index < field_start + field_count; field_index++)
            {
                if (!_default_offsets.TryGetValue(field_index, out int data_index))
                    continue;

                int field_offset = checked(_fields.Offset + field_index * FieldDefinitionSize);
                string field_name = ReadString(ReadUInt32(field_offset));
                short value = ReadInt16(checked(_default_data.Offset + data_index));
                members.Add(new Il2CppEnumMember(field_name, value, ordinal++));
            }

            if (members.Count < minimum_members)
                continue;

            string name = ReadString(ReadUInt32(offset));
            string type_namespace = ReadString(ReadUInt32(offset + 4));
            definitions.Add(new Il2CppEnumDefinition(name, type_namespace, type_index, members));
        }
        return definitions;
    }

    public IReadOnlyList<string> ReadImageNames()
    {
        if (Version is not (29 or 31))
        {
            throw new NotSupportedException(
                $"Safe Il2CppDumper execution supports only validated metadata image-table layouts 29 and 31; " +
                $"metadata version {Version} is outside this safety boundary.");
        }

        Region images = ReadRegion(168, "images");
        RequireDivisible(images, ImageDefinitionSize);
        int count = images.Length / ImageDefinitionSize;
        var names = new string[count];
        for (int index = 0; index < count; index++)
        {
            int offset = checked(images.Offset + index * ImageDefinitionSize);
            names[index] = ReadString(ReadUInt32(offset));
        }
        return names;
    }

    public Il2CppProtocolEnumPair? FindProtocolEnumPair(IReadOnlyList<Il2CppEnumDefinition> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count < 2)
            return null;

        var candidate_indexes = candidates.Select(candidate => candidate.TypeIndex).ToHashSet();
        var type_indexes = new Dictionary<int, int>();
        int type_index = 0;
        for (int offset = _type_definitions.Offset; offset < _type_definitions.End; offset += TypeDefinitionSize, type_index++)
        {
            int byval_type_index = ReadInt32(offset + 8);
            if (byval_type_index >= 0)
                type_indexes.TryAdd(byval_type_index, type_index);
        }

        int method_total = _methods.Length / _method_definition_size;
        int parameter_total = _parameters.Length / ParameterDefinitionSize;
        var matches = new List<Il2CppProtocolEnumPair>();
        type_index = 0;
        for (int offset = _type_definitions.Offset; offset < _type_definitions.End; offset += TypeDefinitionSize, type_index++)
        {
            int method_start = ReadInt32(offset + 36);
            int method_count = ReadUInt16(offset + 64);
            if (method_start < 0 || method_count == 0 || method_start > method_total - method_count)
                continue;

            var signatures = new List<int[]>();
            for (int method_index = method_start; method_index < method_start + method_count; method_index++)
            {
                int method_offset = checked(_methods.Offset + method_index * _method_definition_size);
                if (!ReadString(ReadUInt32(method_offset)).Equals(".ctor", StringComparison.Ordinal))
                    continue;

                int parameter_start = ReadInt32(method_offset + (Version >= 31 ? 16 : 12));
                int parameter_count = ReadUInt16(method_offset + (Version >= 31 ? 34 : 30));
                if (parameter_start < 0 || parameter_count == 0 || parameter_start > parameter_total - parameter_count)
                    continue;

                var signature = new int[parameter_count];
                Array.Fill(signature, -1);
                for (int parameter_index = 0; parameter_index < parameter_count; parameter_index++)
                {
                    int parameter_offset = checked(
                        _parameters.Offset + (parameter_start + parameter_index) * ParameterDefinitionSize);
                    int parameter_type_index = ReadInt32(parameter_offset + 8);
                    if (type_indexes.TryGetValue(parameter_type_index, out int definition_index) &&
                        candidate_indexes.Contains(definition_index))
                    {
                        signature[parameter_index] = definition_index;
                    }
                }
                signatures.Add(signature);
            }

            foreach (int[] pair in signatures.Where(signature =>
                signature.Length >= 2 &&
                signature[0] >= 0 &&
                signature[1] >= 0 &&
                signature[0] != signature[1]))
            {
                int outgoing_index = pair[0];
                int incoming_index = pair[1];
                int incoming_constructors = signatures.Count(signature =>
                    signature.Length >= 1 &&
                    signature[0] == incoming_index &&
                    !signature.Contains(outgoing_index));
                int outgoing_constructors = signatures.Count(signature =>
                    signature.Length >= 1 &&
                    signature[0] == outgoing_index &&
                    !signature.Contains(incoming_index));
                if (incoming_constructors == 0 || outgoing_constructors != 0)
                    continue;

                int pair_constructors = signatures.Count(signature =>
                    signature.Length >= 2 &&
                    signature[0] == outgoing_index &&
                    signature[1] == incoming_index);
                string name = ReadString(ReadUInt32(offset));
                string type_namespace = ReadString(ReadUInt32(offset + 4));
                string attribute_type = string.IsNullOrEmpty(type_namespace) ? name : $"{type_namespace}.{name}";
                matches.Add(new Il2CppProtocolEnumPair(
                    incoming_index,
                    outgoing_index,
                    attribute_type,
                    incoming_constructors,
                    pair_constructors));
            }
        }

        Il2CppProtocolEnumPair[] distinct = matches
            .GroupBy(match => (match.IncomingTypeIndex, match.OutgoingTypeIndex))
            .Select(group => group
                .OrderByDescending(match => match.PairConstructorCount)
                .ThenByDescending(match => match.IncomingConstructorCount)
                .First())
            .ToArray();
        return distinct.Length switch
        {
            0 => null,
            1 => distinct[0],
            _ => throw new InvalidDataException(
                $"Multiple structurally valid Unity protocol enum pairs were found: " +
                string.Join(", ", distinct.Select(match =>
                    $"{match.OutgoingTypeIndex}>{match.IncomingTypeIndex}")))
        };
    }

    Region ReadRegion(int header_offset, string name)
    {
        int offset = ReadInt32(header_offset);
        int length = ReadInt32(header_offset + 4);
        if (offset < 0 || length < 0 || offset > _data.Length - length)
            throw new InvalidDataException($"Invalid {name} region: offset {offset}, length {length}.");
        return new Region(offset, length);
    }

    string ReadString(uint relative_offset)
    {
        if (relative_offset >= _strings.Length)
            throw new InvalidDataException($"Metadata string offset {relative_offset} exceeds the string table.");
        int start = checked(_strings.Offset + (int)relative_offset);
        int end = Array.IndexOf(_data, (byte)0, start, _strings.End - start);
        if (end < 0)
            throw new InvalidDataException($"Metadata string at offset {relative_offset} is not terminated.");
        try
        {
            return StrictUtf8.GetString(_data, start, end - start);
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException(
                $"Metadata string at offset {relative_offset} is not valid UTF-8.",
                error);
        }
    }

    static void RequireDivisible(Region region, int record_size)
    {
        if (region.Length % record_size != 0)
            throw new InvalidDataException($"Metadata table length {region.Length} is not divisible by record size {record_size}.");
    }

    short ReadInt16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(offset, sizeof(short)));
    ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset, sizeof(ushort)));
    int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(offset, sizeof(int)));
    uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, sizeof(uint)));

    readonly record struct Region(int Offset, int Length)
    {
        public int End => checked(Offset + Length);
    }
}
