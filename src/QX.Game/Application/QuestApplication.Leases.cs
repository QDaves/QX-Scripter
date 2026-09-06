using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Game.Application;

internal sealed partial class QuestApplication
{
    private const int snapshot_lease_limit = 4;
    private readonly object leases_sync = new();
    private readonly Dictionary<long, QuestSnapshotLease> leases = [];
    private readonly LinkedList<long> lease_order = [];
    private long lease_revision;

    private QuestSnapshotLease StoreCurrentLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            QuestState state = quests.State;
            QuestSnapshotLease lease;
            lock (leases_sync)
            {
                ThrowIfDisposed();
                lease = StoreLeaseUnsafe(state);
            }
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The quest state changed while its snapshot was being captured.");
    }

    private QuestSnapshotLease StoreLease(QuestState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            if (!StateSessionActive(state))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the quest snapshot was stored.");
            }
            return StoreLeaseUnsafe(state);
        }
    }

    private QuestSnapshotLease StoreLeaseUnsafe(QuestState state)
    {
        QuestSnapshotLease? existing = leases.Values.FirstOrDefault(
            lease => StatesEquivalent(lease.State, state));
        if (existing is not null)
            return existing;
        QuestEntryView[] available = state.Available
            .Select((quest, ordinal) => EntryView(
                quest,
                ordinal,
                QuestCollection.Available,
                ordinal))
            .ToArray();
        QuestEntryView[] seasonal = state.Seasonal
            .Select((quest, ordinal) => EntryView(
                quest,
                ordinal,
                QuestCollection.Seasonal,
                ordinal))
            .ToArray();
        QuestEntryView[] combined = new QuestEntryView[available.Length + seasonal.Length];
        for (int index = 0; index < available.Length; index++)
        {
            combined[index] = available[index] with
            {
                Ordinal = index
            };
        }
        for (int index = 0; index < seasonal.Length; index++)
        {
            combined[available.Length + index] = seasonal[index] with
            {
                Ordinal = available.Length + index
            };
        }
        long revision = checked(++lease_revision);
        var lease = new QuestSnapshotLease(
            revision,
            state,
            Array.AsReadOnly(available),
            Array.AsReadOnly(seasonal),
            Array.AsReadOnly(combined));
        leases.Add(revision, lease);
        lease_order.AddLast(revision);
        while (leases.Count > snapshot_lease_limit)
        {
            LinkedListNode<long>? oldest = lease_order.First;
            if (oldest is null)
                throw new InvalidOperationException("The quest snapshot lease order is invalid.");
            lease_order.RemoveFirst();
            leases.Remove(oldest.Value);
        }
        return lease;
    }

    private QuestSnapshotLease ReadLease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out QuestSnapshotLease? lease) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The quest snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool LeaseActive(QuestSnapshotLease lease) => StateSessionActive(lease.State);

    private bool StateSessionActive(QuestState state)
    {
        QuestState current = quests.State;
        return ReferenceEquals(current.Session, state.Session) &&
            current.SessionGeneration == state.SessionGeneration &&
            ReferenceEquals(connection.Session, state.Session);
    }

    private void RemoveLease(long revision)
    {
        lock (leases_sync)
        {
            leases.Remove(revision);
            LinkedListNode<long>? node = lease_order.Find(revision);
            if (node is not null)
                lease_order.Remove(node);
        }
    }

    private void ClearLeases()
    {
        lock (leases_sync)
        {
            leases.Clear();
            lease_order.Clear();
        }
    }

    private static bool StatesEquivalent(QuestState left, QuestState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.Revision == right.Revision;

    private static QuestEntryView EntryView(
        QuestData quest,
        int ordinal,
        QuestCollection collection,
        int collection_ordinal) => new(
            ordinal,
            collection,
            collection_ordinal,
            View(quest));

    private static QuestView View(QuestData quest) => new(
        quest.CampaignCode,
        quest.CompletedQuestsInCampaign,
        quest.QuestCountInCampaign,
        quest.ActivityPointType,
        quest.Id,
        quest.IsAccepted,
        quest.Type,
        quest.ImageVersion,
        quest.RewardCurrencyAmount,
        quest.LocalizationCode,
        quest.CompletedSteps,
        quest.TotalSteps,
        quest.SortOrder,
        quest.CatalogPageName,
        quest.ChainCode,
        quest.IsEasy,
        quest.IsSeasonal,
        quest.SeasonalSecondsLeft,
        quest.IsCompleted,
        quest.IsCampaignCompleted,
        quest.IsLastQuestInCampaign,
        quest.CampaignChainCode);

    private static QuestCompletionView View(QuestCompleted completed) => new(
        View(completed.Data),
        completed.ShowDialog);

    private static QuestCancellationView View(QuestCancelled cancelled) => new(
        cancelled.IsExpired,
        View(cancelled.Data));

    private static QuestDailyView View(QuestDaily daily) => new(
        daily.Data is null ? null : View(daily.Data),
        daily.EasyQuestCount,
        daily.HardQuestCount,
        daily.HasQuest);

    private static QuestSummary Summary(QuestState state) => new(
        state.AvailableLoaded,
        state.SeasonalLoaded,
        state.DailyLoaded,
        state.OpenWindow,
        state.Available.Count,
        state.Seasonal.Count,
        state.Current is not null,
        state.LastCompletion is not null,
        state.LastCancellation is not null,
        state.Daily?.HasQuest is true);

    private sealed record QuestSnapshotLease(
        long Revision,
        QuestState State,
        IReadOnlyList<QuestEntryView> Available,
        IReadOnlyList<QuestEntryView> Seasonal,
        IReadOnlyList<QuestEntryView> Combined);
}
