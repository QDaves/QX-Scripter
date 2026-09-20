using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomUsersViewModel : RoomPeopleViewModel
{
    static readonly PersonOrder _order = new();

    public RoomUsersViewModel(RoomPageContext context)
        : base(context, StringComparer.Ordinal)
    {
    }

    [ObservableProperty]
    public partial bool ShowBots { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowPets { get; set; } = true;

    protected override IComparer<PersonRowViewModel> Order => _order;

    protected override void Fill(RoomSnapshot snapshot)
    {
        RoomPerson[] wanted =
        [
            .. snapshot.People.Where(person => person.Kind switch
            {
                RoomPersonKind.Bot => ShowBots,
                RoomPersonKind.Pet => ShowPets,
                _ => true
            })
        ];
        All.Sync(
            wanted,
            PersonRowViewModel.PersonKey,
            person => PersonRowViewModel.FromPerson(person, Context.WebHost),
            (row, person) => row.Take(person, Context.WebHost));
        SortRefresh.Request();
    }

    protected override void Recount()
    {
        if (IsAway)
        {
            CountText = "";
            State.ShowReady();
            return;
        }
        int total = Rows.SourceCount;
        int visible = Rows.Visible.Count;
        CountText = total == visible
            ? $"{total:N0} {(total == 1 ? "person" : "people")}"
            : $"{visible:N0} of {total:N0} people";
        if (total == 0)
            State.ShowEmpty(IconKind.Users, "Nobody is here", "Nobody is standing in this room right now.");
        else if (visible == 0)
            ShowNoMatches();
        else
            State.ShowReady();
    }

    protected override IEnumerable<string> Columns(PersonRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        yield return row.Name;
        yield return row.Detail;
        yield return row.Position;
        yield return row.Index.ToString();
        yield return row.IdText;
    }

    partial void OnShowBotsChanged(bool value) => Show(Snapshot);

    partial void OnShowPetsChanged(bool value) => Show(Snapshot);

    sealed class PersonOrder : IComparer<PersonRowViewModel>
    {
        public int Compare(PersonRowViewModel? left, PersonRowViewModel? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int rank = left.Kind.CompareTo(right.Kind);
            return rank != 0
                ? rank
                : string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
