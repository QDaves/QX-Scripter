namespace Qx.Presentation.Services.FurniLookup;

public static class FurniText
{
    public static string Display(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Named(string identifier, string? localized_name, Func<string, string?> texts)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(texts);
        string resolved = Display(Resolve(localized_name, texts));
        return resolved.Length <= 2 ? identifier : resolved;
    }

    public static string DescriptionOf(string? description, Func<string, string?> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return Display(Resolve(description, texts));
    }

    static string? Resolve(string? value, Func<string, string?> texts)
    {
        if (value is not { Length: > 2 })
            return value;
        if (value[0] != '$' || value[^1] != '$')
            return value;
        return texts(value[1..^1]);
    }
}
