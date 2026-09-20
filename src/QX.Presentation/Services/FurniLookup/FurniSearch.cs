namespace Qx.Presentation.Services.FurniLookup;

public static class FurniSearch
{
    public static int? Rank(string? name, string? identifier, string? description, string? term)
    {
        string text = term?.Trim() ?? "";
        if (text.Length == 0)
            return 0;
        string display = name ?? "";
        string code = identifier ?? "";
        if (display.Equals(text, StringComparison.CurrentCultureIgnoreCase))
            return 0;
        if (display.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
            return 1;
        if (display.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            return 2;
        if (code.Equals(text, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (code.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (code.Contains(text, StringComparison.OrdinalIgnoreCase))
            return 5;
        if ((description ?? "").Contains(text, StringComparison.CurrentCultureIgnoreCase))
            return 6;
        return null;
    }
}
