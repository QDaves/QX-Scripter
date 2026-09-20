namespace Qx.Presentation.Services.Wardrobe;

public static class WardrobeText
{
    public const string DefaultGender = "M";
    public const string Shelf = "Outfits you keep here, however many you like.";
    public const string NothingKept = "Nothing kept yet.";
    public const string AddOrImport = "Add what you are wearing, or import the hotel's ten slots.";
    public const string ConnectFirst = "Connect, then add what you are wearing or pull in the hotel's ten slots.";
    public const string NoMatches = "Nothing matches";
    public const string AskingTheHotel = "Asking the hotel for your wardrobe…";
    public const string KeptCurrent = "Kept what you are wearing.";
    public const string AlreadyKept = "You are already keeping that one.";
    public const string NothingToCopy = "Connect first — there is nothing to copy yet.";
    public const string FigureUnknown = "The hotel has not said what you are wearing yet.";
    public const string HotelWardrobeEmpty = "The hotel's wardrobe is empty.";
    public const string NothingNew = "Nothing new — you are already keeping all of them.";
    public const string FigureCopied = "Figure copied.";
    public const string ClipboardUnreachable = "Could not reach the clipboard.";
    public const string SnapshotChanged = "The wardrobe snapshot changed while it was being read.";
    public const string IncompleteResult = "The wardrobe returned an incomplete result.";
    public const string SessionChanged = "The hotel session changed while the wardrobe was loading.";

    public static string Gender(string? gender) =>
        string.IsNullOrWhiteSpace(gender) || string.Equals(gender, "None", StringComparison.OrdinalIgnoreCase)
            ? DefaultGender
            : gender.Trim()[..1].ToUpperInvariant();

    public static string Title(string? figure, string? name)
    {
        if (!string.IsNullOrEmpty(name))
            return name;
        string worn = figure ?? "";
        string[] parts = worn.Split('.');
        return parts.Length <= 2 ? worn : $"{parts[0]}.{parts[1]}…";
    }

    public static string Slot(int slot) => $"Slot {slot}";

    public static string Counts(int kept, int shown) =>
        kept == shown ? $"{kept:N0} kept" : $"{kept:N0} kept, {shown:N0} shown";

    public static string Deleted(int count) => count == 1 ? "Deleted one outfit." : $"Deleted {count} outfits.";

    public static string Imported(int added, int offered) => (added, offered) switch
    {
        (0, 0) => HotelWardrobeEmpty,
        (0, _) => NothingNew,
        (1, _) => "Kept one outfit from the hotel.",
        _ => $"Kept {added} outfits from the hotel."
    };

    public static string Worn(string title) => $"Wearing {title}.";

    public static string DeleteTitle(int count) => count == 1 ? "Delete outfit?" : "Delete outfits?";

    public static string DeleteMessage(int count, string title) =>
        count == 1
            ? $"“{title}” will be removed from your wardrobe."
            : $"{count} outfits will be removed from your wardrobe.";
}
