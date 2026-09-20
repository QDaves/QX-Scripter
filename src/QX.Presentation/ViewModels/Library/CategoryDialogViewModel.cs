using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Library;

public sealed partial class CategoryDialogViewModel : DialogViewModel<ScriptMeta?>
{
    public const string FieldLabel = "CATEGORY";
    public const string Placeholder = "Category";
    public const string Hint = "Pick an existing category or type a new one. Leave it empty for none.";
    public const string AcceptText = "Save";

    readonly string _script;

    public CategoryDialogViewModel(string script_name, ScriptMeta current, IReadOnlyList<string> categories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script_name);
        ArgumentNullException.ThrowIfNull(current);
        _script = script_name;
        Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        Text = current.Category?.Trim() ?? "";
    }

    public override string Title => $"Category of “{_script}”";

    public override IconKind Icon => IconKind.Category;

    public IReadOnlyList<string> Categories { get; }

    public bool HasCategories => Categories.Count > 0;

    [ObservableProperty]
    public partial string Text { get; set; }

    protected override ScriptMeta? DismissResult => null;

    [RelayCommand]
    void Pick(string? category) => Text = category ?? "";

    [RelayCommand]
    void Accept()
    {
        string typed = Text.Trim();
        Close(new ScriptMeta { Category = typed.Length == 0 ? null : typed });
    }
}
