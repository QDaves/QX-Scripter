using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Qx.Game.Application;
using Qx.Scripting.Hosting;

namespace Qx.Hosting;

internal static class ApplicationScript
{
    private static readonly IReadOnlyDictionary<string, string> constants = typeof(ApplicationMemberIds)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .ToDictionary(field => (string)field.GetRawConstantValue()!, field => field.Name, StringComparer.Ordinal);

    public static string Call(ApplicationDescriptor descriptor)
    {
        var namespaces = new SortedSet<string>(StringComparer.Ordinal) { typeof(ApplicationMemberIds).Namespace! };
        string id = constants.TryGetValue(descriptor.Id, out string? constant)
            ? $"{nameof(ApplicationMemberIds)}.{constant}"
            : SymbolDisplay.FormatLiteral(descriptor.Id, true);
        string result = TypeName(descriptor.ResultType, namespaces);
        string call;
        if (descriptor.RequestType is { } request_type)
        {
            string request = TypeName(request_type, namespaces);
            string arguments = string.Join(", ", request_type.GetConstructors()[0].GetParameters()
                .Where(parameter => !parameter.HasDefaultValue)
                .Select(parameter => $"{parameter.Name}: {Placeholder(parameter.ParameterType, namespaces)}"));
            call = $"var result = await Application.InvokeAsync<{request}, {result}>({id}, new {request}({arguments}));";
        }
        else
        {
            call = $"Application.Subscribe<{result}>({id}, value => Log(value));";
        }

        var script = new StringBuilder();
        foreach (string name in namespaces.Where(name => !ApiTypeCatalog.IsImported(name)))
            script.Append("using ").Append(name).Append(";\n");
        if (script.Length > 0)
            script.Append('\n');
        return script.Append(call).ToString();
    }

    private static string Placeholder(Type type, ISet<string> namespaces)
    {
        if (type == typeof(string))
            return "\"\"";
        if (type == typeof(bool))
            return "false";
        if (type == typeof(Id) ||
            type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(double))
        {
            return "0";
        }
        if (type.IsEnum && Enum.GetNames(type) is [string first, ..])
            return $"{TypeName(type, namespaces)}.{first}";
        return type.IsArray || IsSequence(type) ? "[]" : "default";
    }

    private static bool IsSequence(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(IEnumerable<>) ||
            definition == typeof(IReadOnlyCollection<>) ||
            definition == typeof(IReadOnlyList<>) ||
            definition == typeof(List<>));

    private static string TypeName(Type type, ISet<string> namespaces)
    {
        Collect(type, namespaces);
        return ReflectionFormat.FriendlyName(type);
    }

    private static void Collect(Type type, ISet<string> namespaces)
    {
        if (type.HasElementType)
        {
            Collect(type.GetElementType()!, namespaces);
            return;
        }
        if (type.IsGenericParameter)
            return;
        if (type.Namespace is { } name)
            namespaces.Add(name);
        foreach (Type argument in type.GetGenericArguments())
            Collect(argument, namespaces);
    }
}
