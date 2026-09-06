using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;
using Flazzy.ABC.AVM2.Instructions.Containers;

namespace Qx.Headers.Flash;

public static class StructuralSignature
{
    public static string? ForIncoming(
        ASInstance message,
        Avm2CallTargetResolver types)
    {
        ASInstance? parser = ParserOf(message, types);
        IReadOnlyList<Avm2MethodBinding> matches = parser is null
            ? []
            : types.ResolvePublicMethods(parser, "parse");
        ASMethod? method = matches.Count == 1 ? matches[0].Method : null;
        return method?.Body is null ? null : Fingerprint("in3", method);
    }

    public static string? ForOutgoing(ASInstance composer)
    {
        ASMethod? constructor = composer?.Constructor;
        return constructor?.Body is null ? null : Fingerprint("out3", constructor);
    }

    static string Fingerprint(string direction, ASMethod method)
    {
        var canonical = new StringBuilder(direction);
        canonical.Append('|').Append(method.Parameters.Count);
        foreach (ASParameter parameter in method.Parameters)
            canonical.Append(':').Append(NormalizeName(parameter.Type));
        canonical.Append('>').Append(NormalizeName(method.ReturnType));

        ASMethodBody body = method.Body ??
            throw new InvalidDataException(
                "A structural signature requires a method body.");
        ASCode code = body.ParseCode();
        foreach (ASInstruction instruction in code)
        {
            if (instruction.OP is OPCode.Nop or OPCode.Debug or OPCode.DebugFile or OPCode.DebugLine)
                continue;

            canonical.Append('|').Append(((byte)instruction.OP).ToString("X2", CultureInfo.InvariantCulture));
            AppendOperand(canonical, instruction);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"{direction}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    static void AppendOperand(StringBuilder canonical, ASInstruction instruction)
    {
        if (instruction is IPropertyContainer property)
        {
            canonical.Append(':').Append(NormalizeName(property.PropertyName));
            int? argument_count = PropertyArgumentCount(instruction);
            if (argument_count is not null)
                canonical.Append('#').Append(argument_count.Value);
        }

        switch (instruction)
        {
            case Primitive primitive:
                canonical.Append(':').Append(PrimitiveKind(primitive.Value));
                break;
            case Local local:
                canonical.Append(':').Append(local.Register);
                break;
            case NewArrayIns array:
                canonical.Append(':').Append(array.ArgCount);
                break;
            case NewObjectIns value:
                canonical.Append(':').Append(value.ArgCount);
                break;
            case CallIns call:
                canonical.Append(':').Append(call.ArgCount);
                break;
            case ConstructIns construct:
                canonical.Append(':').Append(construct.ArgCount);
                break;
            case ConstructSuperIns construct_super:
                canonical.Append(':').Append(construct_super.ArgCount);
                break;
            case ApplyTypeIns apply:
                canonical.Append(':').Append(apply.ParamCount);
                break;
            case GetSlotIns get_slot:
                canonical.Append(':').Append(get_slot.SlotIndex);
                break;
            case SetSlotIns set_slot:
                canonical.Append(':').Append(set_slot.SlotIndex);
                break;
        }
    }

    static int? PropertyArgumentCount(ASInstruction instruction) => instruction switch
    {
        CallPropertyIns call => call.ArgCount,
        CallPropVoidIns call => call.ArgCount,
        CallPropLexIns call => call.ArgCount,
        CallSuperIns call => call.ArgCount,
        CallSuperVoidIns call => call.ArgCount,
        ConstructPropIns construct => construct.ArgCount,
        _ => null
    };

    static char PrimitiveKind(object? value) => value switch
    {
        null => 'n',
        bool => 'b',
        sbyte or byte or short or int or uint => 'i',
        float or double or decimal => 'd',
        string => 's',
        _ => 'x'
    };

    static string NormalizeName(ASMultiname? name)
    {
        if (name is null || name.Kind == MultinameKind.TypeName)
            return "?";
        if (name.IsNameNeeded)
            return "<runtime>";
        if (name.IsAnyName)
            return "*";
        if (!Avm2MethodAnalyzer.TryGetStaticName(name, out string value))
            return "<invalid>";
        return NormalizeName(value);
    }

    static string NormalizeName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "?";
        return name.StartsWith("_-", StringComparison.Ordinal) || name.StartsWith("§_-", StringComparison.Ordinal) || name.Length < 3
            ? "_"
            : name;
    }

    static ASInstance? ParserOf(
        ASInstance message,
        Avm2CallTargetResolver types)
    {
        return ParserBindingResolver.Resolve(message, types);
    }

    internal static bool ImplementsParser(ASInstance instance)
    {
        try
        {
            return instance.GetInterfaces().Any(candidate =>
                    Avm2MethodAnalyzer.TryGetStaticName(
                        candidate,
                        out string name) &&
                    name == "IMessageParser") ||
                instance.Traits.Any(trait =>
                    trait.Kind is TraitKind.Method or TraitKind.Getter or TraitKind.Setter &&
                    Avm2MethodAnalyzer.TryGetStaticName(
                        trait.QName,
                        out string name) &&
                    name == "parse");
        }
        catch
        {
            return false;
        }
    }
}
