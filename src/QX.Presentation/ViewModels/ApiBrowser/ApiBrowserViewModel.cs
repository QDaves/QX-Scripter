using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.ViewModels.Editor;
using Qx.Scripting;

namespace Qx.Presentation.ViewModels.ApiBrowser;

public sealed partial class ApiBrowserViewModel : ObservableObject
{
    readonly IApiInsertTarget _target;

    public ApiBrowserViewModel(IApiInsertTarget target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Filters =
        [
            new("All"),
            new("State", ScriptApiKind.State),
            new("Actions", ScriptApiKind.Action),
            new("Events", ScriptApiKind.Event),
            .. ScriptApiCatalog.Groups.Select(group => new ApiFilter(group, Group: group))
        ];
        ReturnTypes = ["Any return type", .. ScriptApiCatalog.ReturnTypes];
        SelectedFilter = Filters[0];
    }

    public IReadOnlyList<ApiFilter> Filters { get; }

    public IReadOnlyList<string> ReturnTypes { get; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial ApiFilter? SelectedFilter { get; set; }

    [ObservableProperty]
    public partial int ReturnTypeIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(CountText))]
    public partial IReadOnlyList<ScriptApiMember> Results { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    public partial ScriptApiMember? Selected { get; set; }

    [ObservableProperty]
    public partial long FocusRequest { get; private set; }

    public bool IsEmpty => Results.Count == 0;

    public string CountText => Results.Count == ScriptApiCatalog.All.Count
        ? $"{Results.Count} members"
        : $"{Results.Count} of {ScriptApiCatalog.All.Count}";

    public void Activate() => FocusRequest++;

    public void Move(int offset)
    {
        if (Results.Count == 0)
            return;
        int index = -1;
        for (int at = 0; at < Results.Count; at++)
        {
            if (ReferenceEquals(Results[at], Selected))
            {
                index = at;
                break;
            }
        }
        Selected = Results[Math.Clamp(index + offset, 0, Results.Count - 1)];
    }

    public void Escape()
    {
        if (Query.Length > 0)
            Query = "";
        else
            Close();
    }

    [RelayCommand(CanExecute = nameof(CanInsert))]
    void Insert()
    {
        if (Selected is { } member)
            _target.Insert(member.Insert, member.CaretOffset);
    }

    bool CanInsert() => Selected is not null;

    [RelayCommand]
    void Close() => _target.CloseApiBrowser();

    partial void OnQueryChanged(string value) => Refilter();

    partial void OnReturnTypeIndexChanged(int value) => Refilter();

    partial void OnSelectedFilterChanged(ApiFilter? value)
    {
        if (value is null)
            SelectedFilter = Filters[0];
        else
            Refilter();
    }

    void Refilter()
    {
        string? return_type = ReturnTypeIndex > 0 && ReturnTypeIndex < ReturnTypes.Count
            ? ReturnTypes[ReturnTypeIndex]
            : null;
        Results = ScriptApiCatalog.Search(Query, SelectedFilter?.Kind, SelectedFilter?.Group, return_type);
        Selected = Results.FirstOrDefault();
    }
}

public sealed record ApiFilter(string Text, ScriptApiKind? Kind = null, string? Group = null);

public sealed class ApiBrowserFactory : IApiBrowserFactory
{
    public ApiBrowserViewModel Create(IApiInsertTarget target) => new(target);
}
