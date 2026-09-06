using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Qx.Scripting;

internal static class ReflectionFormat
{
    private static readonly ThreadLocal<NullabilityInfoContext> Nullability =
        new(() => new NullabilityInfoContext());

    private static NullabilityInfo NullabilityOf(ParameterInfo parameter) =>
        Nullability.Value!.Create(parameter);

    private static NullabilityInfo NullabilityOf(PropertyInfo property) =>
        Nullability.Value!.Create(property);

    private static NullabilityInfo NullabilityOf(EventInfo @event) =>
        Nullability.Value!.Create(@event);

    private static NullabilityInfo NullabilityOf(FieldInfo field) =>
        Nullability.Value!.Create(field);

    public static string FriendlyName(
        Type type,
        NullabilityInfo? nullability = null,
        NullabilityState? genericParameterState = null)
    {
        if (type.IsByRef)
            return FriendlyName(type.GetElementType()!, nullability?.ElementType);

        Type? nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return FriendlyName(nullable, nullability?.GenericTypeArguments.FirstOrDefault()) + "?";

        if (type.IsArray)
        {
            string commas = new(',', type.GetArrayRank() - 1);
            string name = $"{FriendlyName(type.GetElementType()!, nullability?.ElementType)}[{commas}]";
            return NullableSuffix(type, nullability, name);
        }

        if (type.IsPointer)
            return FriendlyName(type.GetElementType()!) + "*";

        if (Alias(type) is { } alias)
            return NullableSuffix(type, nullability, alias);

        if (type.IsGenericParameter)
            return NullableSuffix(type, genericParameterState ?? nullability?.ReadState, type.Name);

        string formatted;
        if (type.IsGenericType)
        {
            Type[] arguments = type.GetGenericArguments();
            NullabilityInfo[] nullableArguments = nullability?.GenericTypeArguments ?? [];
            int inheritedArguments = type.IsNested && type.DeclaringType?.IsGenericType == true
                ? type.DeclaringType.GetGenericArguments().Length
                : 0;
            string name = StripArity(type.Name);
            string ownArguments = string.Join(", ", arguments
                .Skip(inheritedArguments)
                .Select((argument, index) =>
                {
                    int nullableIndex = inheritedArguments + index;
                    NullabilityInfo? argumentNullability = nullableIndex < nullableArguments.Length
                        ? nullableArguments[nullableIndex]
                        : null;
                    return FriendlyName(argument, argumentNullability);
                }));
            string ownName = ownArguments.Length == 0 ? name : $"{name}<{ownArguments}>";
            formatted = type.IsNested
                ? $"{FriendlyName(type.DeclaringType!)}.{ownName}"
                : ownName;
        }
        else
        {
            formatted = type.IsNested
                ? $"{FriendlyName(type.DeclaringType!)}.{type.Name}"
                : type.Name;
        }

        return NullableSuffix(type, nullability, formatted);
    }

    public static string TypeDeclaration(Type type)
    {
        string kind = TypeKind(type);
        string name = FriendlyName(type);
        var inherited = new List<string>();

        if (MeaningfulBaseType(type.BaseType))
            inherited.Add(FriendlyName(type.BaseType!));
        inherited.AddRange(type.GetInterfaces().Select(value => FriendlyName(value)).OrderBy(value => value, StringComparer.Ordinal));

        string declaration = inherited.Count == 0
            ? $"{kind} {name}"
            : $"{kind} {name} : {string.Join(", ", inherited)}";

        return declaration + GenericConstraints(type.GetGenericArguments().Where(argument => argument.IsGenericParameter));
    }

    public static string Constructor(ConstructorInfo constructor)
    {
        string modifier = constructor.IsStatic ? "static " : "";
        string parameters = string.Join(", ", constructor.GetParameters().Select(parameter => Parameter(parameter)));
        return $"{modifier}{FriendlyName(constructor.DeclaringType!)}({parameters})";
    }

    public static string Method(MethodInfo method)
    {
        string modifier = method.IsStatic ? "static " : "";
        string returnType = FriendlyName(
            method.ReturnType,
            NullabilityOf(method.ReturnParameter),
            GenericParameterState(method.ReturnParameter, method));
        Type[] genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : [];
        string generic = genericArguments.Length == 0
            ? ""
            : $"<{string.Join(", ", genericArguments.Select(argument => argument.Name))}>";
        ParameterInfo[] parameters = method.GetParameters();
        string parameterList = string.Join(", ", parameters.Select((parameter, index) =>
            Parameter(parameter, method.IsDefined(typeof(ExtensionAttribute), false) && index == 0)));
        string constraints = GenericConstraints(genericArguments);
        return $"{modifier}{returnType} {Escape(method.Name)}{generic}({parameterList}){constraints}";
    }

    public static string Property(PropertyInfo property)
    {
        MethodInfo? accessor = property.GetMethod ?? property.SetMethod;
        string modifier = accessor?.IsStatic == true ? "static " : "";
        string type = FriendlyName(
            property.PropertyType,
            NullabilityOf(property),
            GenericParameterState(property, property));
        ParameterInfo[] indexes = property.GetIndexParameters();
        string name = indexes.Length == 0
            ? Escape(property.Name)
            : $"this[{string.Join(", ", indexes.Select(parameter => Parameter(parameter)))}]";
        var accessors = new List<string>(2);
        if (property.GetMethod?.IsPublic == true)
            accessors.Add("get;");
        if (property.SetMethod?.IsPublic == true)
            accessors.Add(IsInitOnly(property.SetMethod) ? "init;" : "set;");
        return $"{modifier}{type} {name} {{ {string.Join(" ", accessors)} }}";
    }

    public static string Event(EventInfo @event)
    {
        MethodInfo? accessor = @event.AddMethod ?? @event.RemoveMethod;
        string modifier = accessor?.IsStatic == true ? "static " : "";
        return $"{modifier}event {FriendlyName(
            @event.EventHandlerType!,
            NullabilityOf(@event),
            GenericParameterState(@event, @event))} {Escape(@event.Name)}";
    }

    public static string Field(FieldInfo field)
    {
        string modifier = field.IsLiteral
            ? "const "
            : field.IsStatic
                ? field.IsInitOnly ? "static readonly " : "static "
                : field.IsInitOnly ? "readonly " : "";
        string value = field.IsLiteral ? $" = {DefaultValue(field.GetRawConstantValue(), field.FieldType)}" : "";
        return $"{modifier}{FriendlyName(field.FieldType, NullabilityOf(field), GenericParameterState(field, field))} {Escape(field.Name)}{value}";
    }

    public static string EnumValue(Type type, string name)
    {
        object value = Enum.Parse(type, name);
        Type underlying = Enum.GetUnderlyingType(type);
        string number = underlying == typeof(ulong)
            ? Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        return $"{Escape(name)} = {number}";
    }

    /// <summary>
    /// Builds the ECMA-334 documentation comment identifier for <paramref name="type"/>, the
    /// <c>T:</c> key under which the C# compiler writes the type into the generated XML
    /// documentation file. Generic types keep their metadata arity (<c>Outer`1.Inner`1</c>)
    /// and nested types are joined with dots.
    /// </summary>
    public static string DocumentationId(Type type) => "T:" + DefinitionId(type);

    /// <summary>
    /// Builds the ECMA-334 documentation comment identifier for a member, matching the keys the
    /// C# compiler emits into the generated XML documentation file. Handles constructors
    /// (<c>#ctor</c>), generic arity, indexer and method parameter lists, ref/in/out parameters
    /// (<c>@</c>), arrays, pointers and conversion operator return suffixes.
    /// </summary>
    /// <returns>
    /// The identifier, or an empty string when the member kind has no documentation identifier.
    /// </returns>
    public static string DocumentationId(MemberInfo member) =>
        member switch
        {
            Type type => DocumentationId(type),
            MethodBase method => MethodId(method),
            PropertyInfo property => PropertyId(property),
            EventInfo @event => "E:" + DeclaringId(@event) + EncodeName(@event.Name),
            FieldInfo field => "F:" + DeclaringId(field) + EncodeName(field.Name),
            _ => ""
        };

    private static string MethodId(MethodBase method)
    {
        var builder = new StringBuilder("M:");
        builder.Append(DeclaringId(method));
        builder.Append(method is ConstructorInfo
            ? method.IsStatic ? "#cctor" : "#ctor"
            : EncodeName(method.Name));

        if (method is MethodInfo { IsGenericMethod: true } generic)
            builder.Append("``").Append(generic.GetGenericArguments().Length);

        AppendParameters(builder, method.GetParameters());

        if (method is MethodInfo { Name: "op_Implicit" or "op_Explicit" } conversion)
            builder.Append('~').Append(ReferenceId(conversion.ReturnType));

        return builder.ToString();
    }

    private static string PropertyId(PropertyInfo property)
    {
        var builder = new StringBuilder("P:");
        builder.Append(DeclaringId(property)).Append(EncodeName(property.Name));
        AppendParameters(builder, property.GetIndexParameters());
        return builder.ToString();
    }

    private static void AppendParameters(StringBuilder builder, ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
            return;
        builder.Append('(');
        for (int index = 0; index < parameters.Length; index++)
        {
            if (index > 0)
                builder.Append(',');
            builder.Append(ReferenceId(parameters[index].ParameterType));
        }
        builder.Append(')');
    }

    private static string DeclaringId(MemberInfo member) =>
        member.DeclaringType is { } declaring ? DefinitionId(declaring) + "." : "";

    private static string DefinitionId(Type type)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            type = type.GetGenericTypeDefinition();

        var builder = new StringBuilder();
        AppendNamespace(builder, type);
        AppendNesting(builder, type, static (target, level) => target.Append(level.Name));
        return builder.ToString();
    }

    private static string ReferenceId(Type type)
    {
        if (type.IsByRef)
            return ReferenceId(type.GetElementType()!) + "@";
        if (type.IsPointer)
            return ReferenceId(type.GetElementType()!) + "*";
        if (type.IsArray)
        {
            string element = ReferenceId(type.GetElementType()!);
            return type.IsSZArray
                ? element + "[]"
                : $"{element}[{string.Join(",", Enumerable.Repeat("0:", type.GetArrayRank()))}]";
        }

        if (type.IsGenericParameter)
        {
            return type.DeclaringMethod is null
                ? "`" + type.GenericParameterPosition
                : "``" + type.GenericParameterPosition;
        }

        Type[] arguments = type.IsGenericType ? type.GetGenericArguments() : [];
        Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        var builder = new StringBuilder();
        AppendNamespace(builder, definition);

        int consumed = 0;
        AppendNesting(builder, definition, (target, level) =>
        {
            target.Append(StripArity(level.Name));
            int total = level.GetGenericArguments().Length;
            if (total <= consumed)
                return target;
            target.Append('{');
            for (int index = consumed; index < total; index++)
            {
                if (index > consumed)
                    target.Append(',');
                target.Append(ReferenceId(arguments[index]));
            }
            consumed = total;
            return target.Append('}');
        });
        return builder.ToString();
    }

    private static void AppendNamespace(StringBuilder builder, Type type)
    {
        if (type.Namespace is { Length: > 0 } @namespace)
            builder.Append(@namespace).Append('.');
    }

    private static void AppendNesting(
        StringBuilder builder,
        Type type,
        Func<StringBuilder, Type, StringBuilder> append)
    {
        var nesting = new List<Type>();
        for (Type? current = type; current is not null; current = current.DeclaringType)
            nesting.Add(current);

        for (int index = nesting.Count - 1; index >= 0; index--)
        {
            append(builder, nesting[index]);
            if (index > 0)
                builder.Append('.');
        }
    }

    private static string EncodeName(string name) => name.Replace('.', '#');

    public static string TypeKind(Type type) =>
        type.IsEnum ? "enum" :
        type.IsInterface ? "interface" :
        type.IsValueType ? "struct" :
        typeof(Delegate).IsAssignableFrom(type) ? "delegate" :
        type.IsAbstract && type.IsSealed ? "static class" :
        "class";

    private static string Parameter(ParameterInfo parameter, bool extension = false)
    {
        Type type = parameter.ParameterType;
        NullabilityInfo nullability = NullabilityOf(parameter);
        string modifier;
        if (extension)
            modifier = "this ";
        else if (parameter.IsDefined(typeof(ParamArrayAttribute), false))
            modifier = "params ";
        else if (parameter.IsOut)
            modifier = "out ";
        else if (type.IsByRef && parameter.IsIn)
            modifier = "in ";
        else if (type.IsByRef)
            modifier = "ref ";
        else
            modifier = "";

        if (type.IsByRef)
        {
            type = type.GetElementType()!;
            nullability = nullability.ElementType ?? nullability;
        }

        string formatted = $"{modifier}{FriendlyName(type, nullability, GenericParameterState(parameter, parameter.Member))} {Escape(parameter.Name ?? "value")}";
        if (parameter.HasDefaultValue)
            formatted += $" = {DefaultValue(parameter.DefaultValue, type)}";
        else if (parameter.IsOptional)
            formatted += " = default";
        return formatted;
    }

    private static string GenericConstraints(IEnumerable<Type> arguments)
    {
        var clauses = new List<string>();
        foreach (Type argument in arguments)
        {
            GenericParameterAttributes attributes = argument.GenericParameterAttributes;
            var constraints = new List<string>();
            if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                constraints.Add("class");
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                constraints.Add("struct");

            constraints.AddRange(argument.GetGenericParameterConstraints()
                .Where(type => type != typeof(ValueType))
                .Select(type => FriendlyName(type)));

            if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0 &&
                (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
                clauses.Add($" where {argument.Name} : {string.Join(", ", constraints)}");
        }
        return string.Concat(clauses);
    }

    private static string DefaultValue(object? value, Type type)
    {
        if (value is null)
            return "null";
        if (value is DBNull or Missing)
            return "default";
        if (type.IsEnum)
        {
            string? name = Enum.GetName(type, value);
            return name is null
                ? $"({FriendlyName(type)}){Convert.ToInt64(value, CultureInfo.InvariantCulture)}"
                : $"{FriendlyName(type)}.{Escape(name)}";
        }

        return value switch
        {
            string text => SymbolDisplay.FormatLiteral(text, true),
            char character => SymbolDisplay.FormatLiteral(character, true),
            bool boolean => boolean ? "true" : "false",
            float number when float.IsNaN(number) => "float.NaN",
            float number when float.IsPositiveInfinity(number) => "float.PositiveInfinity",
            float number when float.IsNegativeInfinity(number) => "float.NegativeInfinity",
            float number => number.ToString("R", CultureInfo.InvariantCulture) + "f",
            double number when double.IsNaN(number) => "double.NaN",
            double number when double.IsPositiveInfinity(number) => "double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) => "double.NegativeInfinity",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
            uint number => number.ToString(CultureInfo.InvariantCulture) + "u",
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            ulong number => number.ToString(CultureInfo.InvariantCulture) + "UL",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "default"
        };
    }

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static NullabilityState? GenericParameterState(object annotated, MemberInfo member)
    {
        byte? state = NullableAttribute(CustomAttributes(annotated)) ?? NullableContext(member);
        return state switch
        {
            1 => NullabilityState.NotNull,
            2 => NullabilityState.Nullable,
            _ => null
        };
    }

    private static byte? NullableAttribute(IEnumerable<CustomAttributeData> attributes)
    {
        CustomAttributeData? attribute = attributes.FirstOrDefault(value =>
            value.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (attribute is null || attribute.ConstructorArguments.Count == 0)
            return null;

        CustomAttributeTypedArgument argument = attribute.ConstructorArguments[0];
        if (argument.Value is byte state)
            return state;
        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> states)
            return states.FirstOrDefault().Value as byte?;
        return null;
    }

    private static byte? NullableContext(MemberInfo member)
    {
        for (MemberInfo? current = member; current is not null; current = current.DeclaringType)
        {
            byte? state = NullableContext(CustomAttributes(current));
            if (state is not null)
                return state;
        }

        return NullableContext(member.Module.CustomAttributes) ??
               NullableContext(member.Module.Assembly.CustomAttributes);
    }

    private static byte? NullableContext(IEnumerable<CustomAttributeData> attributes)
    {
        CustomAttributeData? attribute = attributes.FirstOrDefault(value =>
            value.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
        return attribute?.ConstructorArguments.FirstOrDefault().Value as byte?;
    }

    private static IEnumerable<CustomAttributeData> CustomAttributes(object provider) =>
        provider switch
        {
            MemberInfo member => member.CustomAttributes,
            ParameterInfo parameter => parameter.CustomAttributes,
            Module module => module.CustomAttributes,
            Assembly assembly => assembly.CustomAttributes,
            _ => []
        };

    private static bool MeaningfulBaseType(Type? type) =>
        type is not null &&
        type != typeof(object) &&
        type != typeof(ValueType) &&
        type != typeof(Enum) &&
        type != typeof(MulticastDelegate);

    private static string NullableSuffix(Type type, NullabilityInfo? nullability, string name) =>
        !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? name + "?" : name;

    private static string NullableSuffix(Type type, NullabilityState? state, string name) =>
        !type.IsValueType && state == NullabilityState.Nullable ? name + "?" : name;

    private static string? Alias(Type type) =>
        type == typeof(void) ? "void" :
        type == typeof(object) ? "object" :
        type == typeof(string) ? "string" :
        type == typeof(bool) ? "bool" :
        type == typeof(byte) ? "byte" :
        type == typeof(sbyte) ? "sbyte" :
        type == typeof(short) ? "short" :
        type == typeof(ushort) ? "ushort" :
        type == typeof(int) ? "int" :
        type == typeof(uint) ? "uint" :
        type == typeof(long) ? "long" :
        type == typeof(ulong) ? "ulong" :
        type == typeof(float) ? "float" :
        type == typeof(double) ? "double" :
        type == typeof(decimal) ? "decimal" :
        type == typeof(char) ? "char" :
        type == typeof(nint) ? "nint" :
        type == typeof(nuint) ? "nuint" :
        null;

    private static string Escape(string value) =>
        SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None &&
        SyntaxFacts.GetContextualKeywordKind(value) == SyntaxKind.None
            ? value
            : "@" + value;

    private static string StripArity(string name)
    {
        int index = name.IndexOf('`');
        return index < 0 ? name : name[..index];
    }
}
