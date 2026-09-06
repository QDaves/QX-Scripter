using System.Collections;
using System.Globalization;
using Qx.Messages;

namespace Qx.Protocol;

public static class OutgoingSchemaWriter
{
    public static bool TryWrite(
        in PacketWriter writer,
        IReadOnlyList<OutgoingMessageSchema> schemas,
        object[] values)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        ArgumentNullException.ThrowIfNull(values);

        var candidates = new List<SchemaCandidate>();
        bool has_supported_schema = false;
        foreach (OutgoingMessageSchema schema in schemas)
        {
            if (schema.Parameters.Any(parameter =>
                    parameter.WireType is OutgoingWireType.Unknown or OutgoingWireType.Decimal))
                continue;
            has_supported_schema = true;

            var bound = new object?[schema.Parameters.Count];
            if (!TryBind(schema.Parameters, values, 0, 0, bound, 0, out int score))
                continue;

            byte[] payload;
            try
            {
                using var packet = new Packet(writer.Header, writer.Client);
                WriteBound(packet.Writer(), schema.Parameters, bound);
                payload = packet.Buffer.Span.ToArray();
            }
            catch (Exception error) when (error is ArgumentException or InvalidCastException or OverflowException)
            {
                continue;
            }
            candidates.Add(new SchemaCandidate(payload, score));
        }

        if (candidates.Count == 0 && has_supported_schema)
            throw new ArgumentException("Unity outgoing values do not match any verified wire schema.", nameof(values));
        if (candidates.Count == 0)
            return false;

        int best_score = candidates.Max(candidate => candidate.Score);
        SchemaCandidate[] best = candidates.Where(candidate => candidate.Score == best_score).ToArray();
        byte[][] bodies = best
            .Select(candidate => candidate.Payload)
            .Distinct(ByteArrayComparer.Instance)
            .ToArray();
        if (bodies.Length != 1)
            throw new InvalidOperationException("Unity outgoing payload matches multiple incompatible wire schemas.");

        writer.WriteSpan(bodies[0]);
        return true;
    }

    private static bool TryBind(
        IReadOnlyList<OutgoingParameterSchema> parameters,
        object[] values,
        int parameter_index,
        int value_index,
        object?[] bound,
        int score,
        out int result_score)
    {
        if (parameter_index == parameters.Count)
        {
            result_score = score;
            return value_index == values.Length;
        }

        OutgoingParameterSchema parameter = parameters[parameter_index];
        if (parameter.Collection is not OutgoingCollectionKind.None)
        {
            if (value_index < values.Length &&
                values[value_index] is IEnumerable sequence &&
                values[value_index] is not string)
            {
                object[] items = sequence.Cast<object>().ToArray();
                if (TryNormalizeCollection(parameter.WireType, items, out object?[] normalized, out int collection_score))
                {
                    bound[parameter_index] = normalized;
                    if (TryBind(
                            parameters,
                            values,
                            parameter_index + 1,
                            value_index + 1,
                            bound,
                            score + collection_score + 12,
                            out result_score))
                        return true;
                }
            }

            if (value_index < values.Length && TryReadCount(values[value_index], out int count) &&
                count <= values.Length - value_index - 1)
            {
                object[] items = values[(value_index + 1)..(value_index + 1 + count)];
                if (TryNormalizeCollection(parameter.WireType, items, out object?[] normalized, out int collection_score))
                {
                    bound[parameter_index] = normalized;
                    if (TryBind(
                            parameters,
                            values,
                            parameter_index + 1,
                            value_index + count + 1,
                            bound,
                            score + collection_score + 8,
                            out result_score))
                        return true;
                }
            }

            if (value_index >= values.Length && IsEmptyDefault(parameter.DefaultValue))
            {
                bound[parameter_index] = Array.Empty<object?>();
                if (TryBind(
                        parameters,
                        values,
                        parameter_index + 1,
                        value_index,
                        bound,
                        score - 4,
                        out result_score))
                    return true;
            }

            result_score = 0;
            return false;
        }

        if (value_index < values.Length &&
            TryNormalize(parameter.WireType, values[value_index], out object? normalized_value, out int value_score))
        {
            bound[parameter_index] = normalized_value;
            if (TryBind(
                    parameters,
                    values,
                    parameter_index + 1,
                    value_index + 1,
                    bound,
                    score + value_score + 10,
                    out result_score))
                return true;
        }

        if (TryReadDefault(parameter, out object? default_value))
        {
            bound[parameter_index] = default_value;
            if (TryBind(
                    parameters,
                    values,
                    parameter_index + 1,
                    value_index,
                    bound,
                    score - 4,
                    out result_score))
                return true;
        }

        result_score = 0;
        return false;
    }

    private static bool TryNormalizeCollection(
        OutgoingWireType wire_type,
        object[] items,
        out object?[] normalized,
        out int score)
    {
        normalized = new object?[items.Length];
        score = 0;
        for (int index = 0; index < items.Length; index++)
        {
            if (!TryNormalize(wire_type, items[index], out normalized[index], out int item_score))
                return false;
            score += item_score;
        }
        return true;
    }

    private static bool TryNormalize(
        OutgoingWireType wire_type,
        object? value,
        out object? normalized,
        out int score)
    {
        normalized = null;
        score = 0;
        if (value is null)
            return false;

        switch (wire_type)
        {
            case OutgoingWireType.Boolean:
                if (!TryBoolean(value, out bool state))
                    return false;
                normalized = state;
                score = value is bool ? 6 : 2;
                return true;
            case OutgoingWireType.Int8:
                if (!TrySigned(value, sbyte.MinValue, sbyte.MaxValue, out long int8))
                    return false;
                normalized = unchecked((byte)(sbyte)int8);
                score = value is sbyte ? 6 : 3;
                return true;
            case OutgoingWireType.UInt8:
                if (!TryUnsigned(value, byte.MaxValue, out ulong uint8))
                    return false;
                normalized = (byte)uint8;
                score = value is byte ? 6 : 3;
                return true;
            case OutgoingWireType.Int16:
                if (!TrySigned(value, short.MinValue, short.MaxValue, out long int16))
                    return false;
                normalized = (short)int16;
                score = value is short ? 6 : 3;
                return true;
            case OutgoingWireType.UInt16:
                if (!TryUnsigned(value, ushort.MaxValue, out ulong uint16))
                    return false;
                normalized = unchecked((short)(ushort)uint16);
                score = value is ushort ? 6 : 3;
                return true;
            case OutgoingWireType.Int32:
                if (!TrySigned(value, int.MinValue, int.MaxValue, out long int32))
                    return false;
                normalized = (int)int32;
                score = value is int ? 6 : 3;
                return true;
            case OutgoingWireType.UInt32:
                if (!TryUnsigned(value, uint.MaxValue, out ulong uint32))
                    return false;
                normalized = unchecked((int)(uint)uint32);
                score = value is uint ? 6 : 3;
                return true;
            case OutgoingWireType.Int64:
                if (!TrySigned(value, long.MinValue, long.MaxValue, out long int64))
                    return false;
                normalized = int64;
                score = value is long or Id ? 6 : 3;
                return true;
            case OutgoingWireType.UInt64:
                if (!TryUnsigned(value, ulong.MaxValue, out ulong uint64))
                    return false;
                normalized = unchecked((long)uint64);
                score = value is ulong ? 6 : 3;
                return true;
            case OutgoingWireType.Float32:
                if (!TryDouble(value, out double float32) || float32 is < -float.MaxValue or > float.MaxValue)
                    return false;
                normalized = (float)float32;
                score = value is float ? 6 : 3;
                return true;
            case OutgoingWireType.Float64:
                if (!TryDouble(value, out double float64))
                    return false;
                normalized = float64;
                score = value is double ? 6 : 3;
                return true;
            case OutgoingWireType.Character:
                if (value is char character)
                {
                    normalized = character;
                    score = 6;
                    return true;
                }
                if (value is string character_text && character_text.Length == 1)
                {
                    normalized = character_text[0];
                    score = 4;
                    return true;
                }
                return false;
            case OutgoingWireType.String:
                if (value is string text)
                {
                    normalized = text;
                    score = 6;
                    return true;
                }
                if (value is char string_character)
                {
                    normalized = string_character.ToString();
                    score = 3;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static void WriteBound(
        in PacketWriter writer,
        IReadOnlyList<OutgoingParameterSchema> parameters,
        object?[] values)
    {
        for (int index = 0; index < parameters.Count; index++)
        {
            OutgoingParameterSchema parameter = parameters[index];
            if (parameter.Collection is not OutgoingCollectionKind.None)
            {
                var items = (object?[])values[index]!;
                writer.WriteLength((Length)items.Length);
                foreach (object? item in items)
                    WriteScalar(in writer, parameter.WireType, item!);
                continue;
            }
            WriteScalar(in writer, parameter.WireType, values[index]!);
        }
    }

    private static void WriteScalar(in PacketWriter writer, OutgoingWireType wire_type, object value)
    {
        switch (wire_type)
        {
            case OutgoingWireType.Boolean:
                writer.WriteBool((bool)value);
                break;
            case OutgoingWireType.Int8:
            case OutgoingWireType.UInt8:
                writer.WriteByte((byte)value);
                break;
            case OutgoingWireType.Int16:
            case OutgoingWireType.UInt16:
                writer.WriteShort((short)value);
                break;
            case OutgoingWireType.Int32:
            case OutgoingWireType.UInt32:
                writer.WriteInt((int)value);
                break;
            case OutgoingWireType.Int64:
            case OutgoingWireType.UInt64:
                writer.WriteLong((long)value);
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
                throw new InvalidOperationException($"Unsupported Unity outgoing wire type '{wire_type}'.");
        }
    }

    private static bool TryReadDefault(OutgoingParameterSchema parameter, out object? value)
    {
        value = null;
        string? literal = parameter.DefaultValue;
        if (literal is null)
            return false;
        literal = literal.Trim();

        object? parsed = parameter.WireType switch
        {
            OutgoingWireType.Boolean when bool.TryParse(literal, out bool state) => state,
            OutgoingWireType.Character when TryUnquote(literal, '\'', out string? character) && character.Length == 1 => character[0],
            OutgoingWireType.String when TryUnquote(literal, '"', out string? text) => text,
            OutgoingWireType.Float32 when float.TryParse(RemoveNumericSuffix(literal), NumberStyles.Float, CultureInfo.InvariantCulture, out float number) => number,
            OutgoingWireType.Float64 when double.TryParse(RemoveNumericSuffix(literal), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) => number,
            _ when long.TryParse(RemoveNumericSuffix(literal), NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) => number,
            _ => null
        };
        return parsed is not null && TryNormalize(parameter.WireType, parsed, out value, out _);
    }

    private static bool TryUnquote(string value, char quote, out string text)
    {
        if (value.Length >= 2 && value[0] == quote && value[^1] == quote)
        {
            text = value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\'", "'", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
            return true;
        }
        text = string.Empty;
        return false;
    }

    private static string RemoveNumericSuffix(string value) =>
        value.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'l', 'L', 'u', 'U');

    private static bool IsEmptyDefault(string? value) =>
        value is not null && value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadCount(object value, out int count)
    {
        switch (value)
        {
            case Length length:
                count = length;
                return true;
            case byte number:
                count = number;
                return true;
            case short number when number >= 0:
                count = number;
                return true;
            case int number when number >= 0:
                count = number;
                return true;
            case long number when number is >= 0 and <= int.MaxValue:
                count = (int)number;
                return true;
            default:
                count = 0;
                return false;
        }
    }

    private static bool TryBoolean(object value, out bool result)
    {
        switch (value)
        {
            case bool state:
                result = state;
                return true;
            case byte number when number <= 1:
                result = number != 0;
                return true;
            case short number when number is 0 or 1:
                result = number != 0;
                return true;
            case int number when number is 0 or 1:
                result = number != 0;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool TrySigned(object value, long minimum, long maximum, out long result)
    {
        switch (value)
        {
            case sbyte number: result = number; break;
            case byte number: result = number; break;
            case short number: result = number; break;
            case ushort number: result = number; break;
            case int number: result = number; break;
            case uint number: result = number; break;
            case long number: result = number; break;
            case Id id: result = id; break;
            default:
                result = 0;
                return false;
        }
        return result >= minimum && result <= maximum;
    }

    private static bool TryUnsigned(object value, ulong maximum, out ulong result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                break;
            case ushort number:
                result = number;
                break;
            case uint number:
                result = number;
                break;
            case ulong number:
                result = number;
                break;
            case sbyte number when number >= 0:
                result = (ulong)number;
                break;
            case short number when number >= 0:
                result = (ulong)number;
                break;
            case int number when number >= 0:
                result = (ulong)number;
                break;
            case long number when number >= 0:
                result = (ulong)number;
                break;
            case Id id when (long)id >= 0:
                result = (ulong)(long)id;
                break;
            default:
                result = 0;
                return false;
        }
        return result <= maximum;
    }

    private static bool TryDouble(object value, out double result)
    {
        switch (value)
        {
            case byte number: result = number; return true;
            case sbyte number: result = number; return true;
            case short number: result = number; return true;
            case ushort number: result = number; return true;
            case int number: result = number; return true;
            case uint number: result = number; return true;
            case long number: result = number; return true;
            case ulong number: result = number; return true;
            case float number when float.IsFinite(number): result = number; return true;
            case double number when double.IsFinite(number): result = number; return true;
            case decimal number: result = (double)number; return true;
            default: result = 0; return false;
        }
    }

    private sealed record SchemaCandidate(byte[] Payload, int Score);

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public bool Equals(byte[]? left, byte[]? right) =>
            ReferenceEquals(left, right) || left is not null && right is not null && left.AsSpan().SequenceEqual(right);

        public int GetHashCode(byte[] value)
        {
            var hash = new HashCode();
            hash.AddBytes(value);
            return hash.ToHashCode();
        }
    }
}
