using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomVisitorsViewModel : RoomPeopleViewModel
{
    static readonly VisitOrder _order = new();

    public RoomVisitorsViewModel(RoomPageContext context)
        : base(context, StringComparer.OrdinalIgnoreCase)
    {
    }

    protected override IComparer<PersonRowViewModel> Order => _order;

    [RelayCommand]
    void Clear()
    {
        Context.Gateway.Game.Visitors.Clear();
        Context.Note("The list starts again from now.");
    }

    protected override void Fill(RoomSnapshot snapshot)
    {
        All.Sync(
            snapshot.Visits,
            PersonRowViewModel.VisitKey,
            visit => PersonRowViewModel.FromVisit(visit, Context.WebHost),
            (row, visit) => row.Take(visit, Context.WebHost));
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
        int here = Rows.Visible.Count(row => row.HasTag);
        CountText = total == 0
            ? ""
            : $"{total:N0} seen, {here:N0} still here" + (visible == total ? "" : $" · {visible:N0} shown");
        if (total == 0)
        {
            State.ShowEmpty(
                IconKind.Visitors,
                "Nobody has come or gone yet",
                "The hotel keeps no such list, so this one starts when you walk in.");
        }
        else if (visible == 0)
        {
            ShowNoMatches();
        }
        else
        {
            State.ShowReady();
        }
    }

    protected override IEnumerable<string> Columns(PersonRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        yield return row.Name;
        yield return row.Detail;
        yield return row.Position;
        yield return row.IdText;
    }

    sealed class VisitOrder : IComparer<PersonRowViewModel>
    {
        public int Compare(PersonRowViewModel? left, PersonRowViewModel? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int order = right.Index.CompareTo(left.Index);
            return order != 0
                ? order
                : string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
