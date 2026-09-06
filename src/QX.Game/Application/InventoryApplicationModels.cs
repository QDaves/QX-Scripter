using Qx.Game.Snapshots;
using Qx.Model;

namespace Qx.Game.Application;

public sealed record InventoryStateRequest;

public sealed record InventoryCollectionStateView(
    long SnapshotRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total);

public sealed record InventoryStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    InventoryCollectionStateView Furni,
    InventoryCollectionStateView Pets);

public sealed record InventoryFurniPageRequest(
    Id? ItemId = null,
    int Offset = 0,
    int Limit = 200,
    long? SnapshotRevision = null);

public sealed record InventoryFurniRefreshRequest(
    Id? ItemId = null,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

public sealed record InventoryFurniPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long InventoryRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Matched,
    int Offset,
    int? NextOffset,
    IReadOnlyList<InventoryItemSnapshot> Items) : IInventoryApplicationPage<InventoryItemSnapshot>
{
    IReadOnlyList<InventoryItemSnapshot> IInventoryApplicationPage<InventoryItemSnapshot>.Values =>
        Items;
}

public sealed record InventoryPetPageRequest(
    Id? PetId = null,
    string? Name = null,
    int Offset = 0,
    int Limit = 200,
    long? SnapshotRevision = null);

public sealed record InventoryPetRefreshRequest(
    Id? PetId = null,
    string? Name = null,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

public sealed record InventoryPetPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long InventoryRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Matched,
    int Offset,
    int? NextOffset,
    IReadOnlyList<InventoryPetSnapshot> Pets) : IInventoryApplicationPage<InventoryPetSnapshot>
{
    IReadOnlyList<InventoryPetSnapshot> IInventoryApplicationPage<InventoryPetSnapshot>.Values =>
        Pets;
}

public sealed record InventoryAvatarEffectRequest(int EffectId);

public sealed record InventoryDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long Revision,
    int EffectId);

public enum InventoryChangeKind
{
    Loaded,
    Invalidated,
    Added,
    Updated,
    Removed,
    Reset
}

public sealed record InventoryFurniChanged(
    InventoryChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long LoadGeneration,
    InventoryItemSnapshot? Item);

public sealed record InventoryPetChanged(
    InventoryChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long LoadGeneration,
    InventoryPetSnapshot? Pet,
    bool? OpenInventory);

public static class InventoryApplicationPages
{
    private const int page_limit = 500;

    public static InventoryFurniPage ReadFurni(
        IApplicationRuntime application,
        Id? item_id = null,
        int? max_items = null,
        CancellationToken cancellation_token = default) =>
        ReadFurniAsync(application, item_id, max_items, cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static async ValueTask<InventoryFurniPage> ReadFurniAsync(
        IApplicationRuntime application,
        Id? item_id = null,
        int? max_items = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ValidateMaximum(max_items);
        InventoryFurniPage first = await application
            .InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniList,
                new InventoryFurniPageRequest(
                    ItemId: item_id,
                    Limit: FirstLimit(max_items)),
                cancellation_token)
            .ConfigureAwait(false);
        return await CompleteFurniAsync(
            application,
            first,
            item_id,
            max_items,
            cancellation_token).ConfigureAwait(false);
    }

    public static InventoryFurniPage CompleteFurni(
        IApplicationRuntime application,
        InventoryFurniPage first,
        Id? item_id = null,
        int? max_items = null,
        CancellationToken cancellation_token = default) =>
        CompleteFurniAsync(
                application,
                first,
                item_id,
                max_items,
                cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static ValueTask<InventoryFurniPage> CompleteFurniAsync(
        IApplicationRuntime application,
        InventoryFurniPage first,
        Id? item_id = null,
        int? max_items = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        ValidateMaximum(max_items);
        cancellation_token.ThrowIfCancellationRequested();
        return Complete<InventoryFurniPage, InventoryItemSnapshot>(
            first,
            max_items,
            async (offset, limit) => await application
                .InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniList,
                    new InventoryFurniPageRequest(
                        item_id,
                        offset,
                        limit,
                        first.SnapshotRevision),
                    cancellation_token)
                .ConfigureAwait(false),
            static (page, values, next_offset) => page with
            {
                Offset = 0,
                NextOffset = next_offset,
                Items = Array.AsReadOnly(values.ToArray())
            },
            "furni inventory");
    }

    public static InventoryPetPage ReadPets(
        IApplicationRuntime application,
        Id? pet_id = null,
        string? name = null,
        int? max_pets = null,
        CancellationToken cancellation_token = default) =>
        ReadPetsAsync(application, pet_id, name, max_pets, cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static async ValueTask<InventoryPetPage> ReadPetsAsync(
        IApplicationRuntime application,
        Id? pet_id = null,
        string? name = null,
        int? max_pets = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ValidateMaximum(max_pets);
        InventoryPetPage first = await application
            .InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsList,
                new InventoryPetPageRequest(
                    PetId: pet_id,
                    Name: name,
                    Limit: FirstLimit(max_pets)),
                cancellation_token)
            .ConfigureAwait(false);
        return await CompletePetsAsync(
            application,
            first,
            pet_id,
            name,
            max_pets,
            cancellation_token).ConfigureAwait(false);
    }

    public static InventoryPetPage CompletePets(
        IApplicationRuntime application,
        InventoryPetPage first,
        Id? pet_id = null,
        string? name = null,
        int? max_pets = null,
        CancellationToken cancellation_token = default) =>
        CompletePetsAsync(
                application,
                first,
                pet_id,
                name,
                max_pets,
                cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static ValueTask<InventoryPetPage> CompletePetsAsync(
        IApplicationRuntime application,
        InventoryPetPage first,
        Id? pet_id = null,
        string? name = null,
        int? max_pets = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        ValidateMaximum(max_pets);
        cancellation_token.ThrowIfCancellationRequested();
        return Complete<InventoryPetPage, InventoryPetSnapshot>(
            first,
            max_pets,
            async (offset, limit) => await application
                .InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                    ApplicationMemberIds.InventoryPetsList,
                    new InventoryPetPageRequest(
                        pet_id,
                        name,
                        offset,
                        limit,
                        first.SnapshotRevision),
                    cancellation_token)
                .ConfigureAwait(false),
            static (page, values, next_offset) => page with
            {
                Offset = 0,
                NextOffset = next_offset,
                Pets = Array.AsReadOnly(values.ToArray())
            },
            "pet inventory");
    }

    private static async ValueTask<TPage> Complete<TPage, TValue>(
        TPage first,
        int? maximum,
        Func<int, int, ValueTask<TPage>> read,
        Func<TPage, IReadOnlyList<TValue>, int?, TPage> rebuild,
        string name)
        where TPage : class, IInventoryApplicationPage<TValue>
    {
        int first_consumed = first.Values.Count;
        int? expected_first_next = first_consumed < first.Matched ? first_consumed : null;
        if (first.Offset != 0 ||
            first.SnapshotRevision <= 0 ||
            first.Total < 0 ||
            first.Matched < 0 ||
            first.Matched > first.Total ||
            first_consumed > first.Matched ||
            first.NextOffset != expected_first_next)
        {
            throw new InvalidOperationException($"The {name} returned an invalid first page.");
        }

        int target = Math.Min(maximum ?? first.Matched, first.Matched);
        var values = new List<TValue>(target);
        values.AddRange(first.Values.Take(target));
        int? next_offset = first.NextOffset;

        while (values.Count < target && next_offset is int offset)
        {
            int limit = Math.Min(target - values.Count, page_limit);
            TPage page = await read(offset, limit).ConfigureAwait(false);
            ValidatePage(first, page, offset, limit, name);
            values.AddRange(page.Values);
            next_offset = page.NextOffset;
        }

        if (values.Count != target)
            throw new InvalidOperationException($"The {name} returned an incomplete snapshot.");
        next_offset = values.Count < first.Matched ? values.Count : null;
        return rebuild(first, Array.AsReadOnly(values.ToArray()), next_offset);
    }

    private static void ValidatePage<TValue>(
        IInventoryApplicationPage<TValue> first,
        IInventoryApplicationPage<TValue> page,
        int offset,
        int limit,
        string name)
    {
        int consumed = checked(offset + page.Values.Count);
        int? expected_next = consumed < page.Matched ? consumed : null;
        if (page.Connected != first.Connected ||
            page.Client != first.Client ||
            page.SessionGeneration != first.SessionGeneration ||
            page.Revision != first.Revision ||
            page.SnapshotRevision != first.SnapshotRevision ||
            page.InventoryRevision != first.InventoryRevision ||
            page.LoadGeneration != first.LoadGeneration ||
            page.Loaded != first.Loaded ||
            page.Loading != first.Loading ||
            page.Stale != first.Stale ||
            page.RecoveryPending != first.RecoveryPending ||
            page.ExpectedFragments != first.ExpectedFragments ||
            page.ReceivedFragments != first.ReceivedFragments ||
            page.Total != first.Total ||
            page.Matched != first.Matched ||
            page.Offset != offset ||
            offset < 0 ||
            offset > page.Matched ||
            page.Values.Count > limit ||
            consumed > page.Matched ||
            page.NextOffset != expected_next ||
            expected_next is int next && next <= offset)
        {
            throw new InvalidOperationException($"The {name} snapshot changed while it was being read.");
        }
    }

    private static int FirstLimit(int? maximum) =>
        maximum is null ? page_limit : Math.Max(1, Math.Min(maximum.Value, page_limit));

    private static void ValidateMaximum(int? maximum)
    {
        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
    }
}

public interface IInventoryApplicationPage<out TValue>
{
    bool Connected { get; }
    ClientType? Client { get; }
    long SessionGeneration { get; }
    long Revision { get; }
    long SnapshotRevision { get; }
    long InventoryRevision { get; }
    long LoadGeneration { get; }
    bool Loaded { get; }
    bool Loading { get; }
    bool Stale { get; }
    bool RecoveryPending { get; }
    int ExpectedFragments { get; }
    int ReceivedFragments { get; }
    int Total { get; }
    int Matched { get; }
    int Offset { get; }
    int? NextOffset { get; }
    IReadOnlyList<TValue> Values { get; }
}
