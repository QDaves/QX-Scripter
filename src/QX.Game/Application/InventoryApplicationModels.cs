using Qx.Game.Snapshots;
using Qx.Model;

namespace Qx.Game.Application;

/// <summary>Represents a request for the inventory state view.</summary>
/// <remarks>Used by the <c>inventory.state</c> query, which reads the current furni and pet inventory state.</remarks>
public sealed record InventoryStateRequest;

/// <summary>Represents the load state of one inventory collection.</summary>
/// <remarks>
/// The inventory arrives in fragments. The collection is replaced only once every fragment of a
/// load has been received.
/// </remarks>
/// <param name="SnapshotRevision">The revision of the collection, which increases with every change to it.</param>
/// <param name="LoadGeneration">The load generation, which increases each time a load begins or is abandoned, and when the collection is invalidated or cleared.</param>
/// <param name="Loaded">Whether the complete collection has been received.</param>
/// <param name="Loading">Whether a request is pending or its fragments are still arriving.</param>
/// <param name="Stale">Whether the collection holds entries that a completed load has not confirmed yet.</param>
/// <param name="RecoveryPending">Whether a completed load was discarded because it could not be matched to the active request, and the collection waits for a later complete load.</param>
/// <param name="ExpectedFragments">The number of fragments the current load expects, or -1 when it is not known yet.</param>
/// <param name="ReceivedFragments">The number of fragments received in the current load.</param>
/// <param name="Total">The number of entries in the collection.</param>
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

/// <summary>Represents the current furni and pet inventory state.</summary>
/// <remarks>Returned by the <c>inventory.state</c> query.</remarks>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the state belongs to.</param>
/// <param name="Revision">The inventory state revision, which increases with every change.</param>
/// <param name="Furni">The load state of the furni inventory.</param>
/// <param name="Pets">The load state of the pet inventory.</param>
public sealed record InventoryStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    InventoryCollectionStateView Furni,
    InventoryCollectionStateView Pets);

/// <summary>Represents a request for a page of the furni inventory.</summary>
/// <remarks>
/// Used by the <c>inventory.furni.list</c> query. The items are ordered by item id. The application
/// keeps the 16 most recent furni snapshots of the active hotel session, and a continuation page
/// must use the same <paramref name="ItemId"/> filter as the first page.
/// </remarks>
/// <param name="ItemId">The id of the only item to return, or <see langword="null"/> to return every item. The id must be positive.</param>
/// <param name="Offset">The zero-based index of the first matching item to return.</param>
/// <param name="Limit">The maximum number of items to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record InventoryFurniPageRequest(
    Id? ItemId = null,
    int Offset = 0,
    int Limit = 200,
    long? SnapshotRevision = null);

/// <summary>Represents a request to reload the furni inventory from the server and return its first page.</summary>
/// <remarks>
/// Used by the <c>inventory.furni.refresh</c> operation. The call waits until every fragment of the
/// new inventory has arrived. A request whose first fragment does not arrive in time is sent once
/// more, and the timeout covers the whole load.
/// </remarks>
/// <param name="ItemId">The id of the only item to return, or <see langword="null"/> to return every item. The id must be positive.</param>
/// <param name="Limit">The maximum number of items in the first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for every fragment, in milliseconds, from 1 to 120000.</param>
public sealed record InventoryFurniRefreshRequest(
    Id? ItemId = null,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

/// <summary>Represents a page of the furni inventory read from one snapshot.</summary>
/// <remarks>Returned by the <c>inventory.furni.list</c> query and the <c>inventory.furni.refresh</c> operation.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The inventory state revision when the snapshot was captured.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="InventoryRevision">The revision of the furni collection, which increases with every change to it.</param>
/// <param name="LoadGeneration">The furni inventory load generation.</param>
/// <param name="Loaded">Whether the complete furni inventory has been received.</param>
/// <param name="Loading">Whether a furni inventory request is pending or its fragments are still arriving.</param>
/// <param name="Stale">Whether the furni inventory holds items that a completed load has not confirmed yet.</param>
/// <param name="RecoveryPending">Whether a completed load was discarded because it could not be matched to the active request.</param>
/// <param name="ExpectedFragments">The number of fragments the current load expects, or -1 when it is not known yet.</param>
/// <param name="ReceivedFragments">The number of fragments received in the current load.</param>
/// <param name="Total">The number of items in the furni inventory.</param>
/// <param name="Matched">The number of items that match the filter.</param>
/// <param name="Offset">The zero-based index of the first matching item in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more matching items.</param>
/// <param name="Items">The items in the page.</param>
public sealed record InventoryFurniPage(
    bool Connected,
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

/// <summary>Represents a request for a page of the pet inventory.</summary>
/// <remarks>
/// Used by the <c>inventory.pets.list</c> query. The pets are ordered by pet id, and both filters
/// apply together when both are set. The application keeps the 16 most recent pet snapshots of the
/// active hotel session, and a continuation page must use the same filters as the first page.
/// </remarks>
/// <param name="PetId">The id of the only pet to return, or <see langword="null"/> to skip the id filter. The id must be positive.</param>
/// <param name="Name">The exact pet name to match, ignoring case, or <see langword="null"/> to skip the name filter. The name must not be blank and must fit in 65535 UTF-8 bytes.</param>
/// <param name="Offset">The zero-based index of the first matching pet to return.</param>
/// <param name="Limit">The maximum number of pets to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record InventoryPetPageRequest(
    Id? PetId = null,
    string? Name = null,
    int Offset = 0,
    int Limit = 200,
    long? SnapshotRevision = null);

/// <summary>Represents a request to reload the pet inventory from the server and return its first page.</summary>
/// <remarks>
/// Used by the <c>inventory.pets.refresh</c> operation. The call waits until every fragment of the
/// new inventory has arrived. A request whose first fragment does not arrive in time is sent once
/// more, and the timeout covers the whole load.
/// </remarks>
/// <param name="PetId">The id of the only pet to return, or <see langword="null"/> to skip the id filter. The id must be positive.</param>
/// <param name="Name">The exact pet name to match, ignoring case, or <see langword="null"/> to skip the name filter. The name must not be blank and must fit in 65535 UTF-8 bytes.</param>
/// <param name="Limit">The maximum number of pets in the first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for every fragment, in milliseconds, from 1 to 120000.</param>
public sealed record InventoryPetRefreshRequest(
    Id? PetId = null,
    string? Name = null,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

/// <summary>Represents a page of the pet inventory read from one snapshot.</summary>
/// <remarks>Returned by the <c>inventory.pets.list</c> query and the <c>inventory.pets.refresh</c> operation.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The inventory state revision when the snapshot was captured.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="InventoryRevision">The revision of the pet collection, which increases with every change to it.</param>
/// <param name="LoadGeneration">The pet inventory load generation.</param>
/// <param name="Loaded">Whether the complete pet inventory has been received.</param>
/// <param name="Loading">Whether a pet inventory request is pending or its fragments are still arriving.</param>
/// <param name="Stale">Whether the pet inventory holds pets that a completed load has not confirmed yet.</param>
/// <param name="RecoveryPending">Whether a completed load was discarded because it could not be matched to the active request.</param>
/// <param name="ExpectedFragments">The number of fragments the current load expects, or -1 when it is not known yet.</param>
/// <param name="ReceivedFragments">The number of fragments received in the current load.</param>
/// <param name="Total">The number of pets in the pet inventory.</param>
/// <param name="Matched">The number of pets that match the filters.</param>
/// <param name="Offset">The zero-based index of the first matching pet in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more matching pets.</param>
/// <param name="Pets">The pets in the page.</param>
public sealed record InventoryPetPage(
    bool Connected,
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

/// <summary>Represents a request to activate an avatar effect the user owns.</summary>
/// <remarks>Used by the <c>inventory.avatar_effect.activate</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="EffectId">The id of the avatar effect.</param>
public sealed record InventoryAvatarEffectRequest(int EffectId);

/// <summary>Represents the receipt for an avatar effect activation that was sent.</summary>
/// <remarks>Returned by the <c>inventory.avatar_effect.activate</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="Revision">The inventory state revision after the request was sent.</param>
/// <param name="EffectId">The id of the avatar effect.</param>
public sealed record InventoryDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long Revision,
    int EffectId);

/// <summary>Specifies the kind of an inventory change.</summary>
public enum InventoryChangeKind
{
    /// <summary>Every fragment of the collection was received.</summary>
    Loaded,
    /// <summary>The hotel marked the furni inventory as out of date, so it must be loaded again.</summary>
    Invalidated,
    /// <summary>An entry was added to the collection.</summary>
    Added,
    /// <summary>An entry in the collection was updated.</summary>
    Updated,
    /// <summary>An entry was removed from the collection.</summary>
    Removed,
    /// <summary>The inventory was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the furni inventory.</summary>
/// <remarks>
/// Published by the <c>inventory.furni.changed</c> event. A message that adds, updates or removes
/// several items publishes one change for each item.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The inventory state revision after the change.</param>
/// <param name="SnapshotRevision">The revision of the furni collection after the change.</param>
/// <param name="LoadGeneration">The furni inventory load generation after the change.</param>
/// <param name="Item">The item for <see cref="InventoryChangeKind.Added"/>, <see cref="InventoryChangeKind.Updated"/> and <see cref="InventoryChangeKind.Removed"/>; otherwise, <see langword="null"/>.</param>
public sealed record InventoryFurniChanged(
    InventoryChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long LoadGeneration,
    InventoryItemSnapshot? Item);

/// <summary>Represents a change to the pet inventory.</summary>
/// <remarks>Published by the <c>inventory.pets.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The inventory state revision after the change.</param>
/// <param name="SnapshotRevision">The revision of the pet collection after the change.</param>
/// <param name="LoadGeneration">The pet inventory load generation after the change.</param>
/// <param name="Pet">The pet for <see cref="InventoryChangeKind.Added"/>, <see cref="InventoryChangeKind.Updated"/> and <see cref="InventoryChangeKind.Removed"/>; otherwise, <see langword="null"/>.</param>
/// <param name="OpenInventory">Whether the client should open the inventory to show the pet for <see cref="InventoryChangeKind.Added"/>; otherwise, <see langword="null"/>.</param>
public sealed record InventoryPetChanged(
    InventoryChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long SessionGeneration,
    long Revision,
    long SnapshotRevision,
    long LoadGeneration,
    InventoryPetSnapshot? Pet,
    bool? OpenInventory);

/// <summary>Provides helpers that read a whole furni or pet inventory snapshot through the application runtime.</summary>
/// <remarks>
/// The helpers read pages of up to 500 entries from one retained snapshot and join them into a
/// single page with <c>Offset</c> 0. They throw <see cref="InvalidOperationException"/> when a
/// page does not continue the first one, for example because the snapshot is no longer retained.
/// </remarks>
public static class InventoryApplicationPages
{
    private const int page_limit = 500;

    /// <summary>Reads the furni inventory and joins every page into one page, blocking until it is complete.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="itemId">The id of the only item to read, or <see langword="null"/> to read every item.</param>
    /// <param name="maxItems">The maximum number of items to read, or <see langword="null"/> to read every matching item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested items, with <c>NextOffset</c> set when more matching items remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxItems"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the snapshot changes or becomes unavailable while it is read.</exception>
    public static InventoryFurniPage ReadFurni(
        IApplicationRuntime application,
        Id? itemId = null,
        int? maxItems = null,
        CancellationToken cancellationToken = default) =>
        ReadFurniAsync(application, itemId, maxItems, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>Reads the furni inventory and joins every page into one page.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="itemId">The id of the only item to read, or <see langword="null"/> to read every item.</param>
    /// <param name="maxItems">The maximum number of items to read, or <see langword="null"/> to read every matching item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested items, with <c>NextOffset</c> set when more matching items remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxItems"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the snapshot changes or becomes unavailable while it is read.</exception>
    public static async ValueTask<InventoryFurniPage> ReadFurniAsync(
        IApplicationRuntime application,
        Id? itemId = null,
        int? maxItems = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ValidateMaximum(maxItems);
        InventoryFurniPage first = await application
            .InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniList,
                new InventoryFurniPageRequest(
                    ItemId: itemId,
                    Limit: FirstLimit(maxItems)),
                cancellationToken)
            .ConfigureAwait(false);
        return await CompleteFurniAsync(
            application,
            first,
            itemId,
            maxItems,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the remaining pages of a furni inventory snapshot and joins them with the first page, blocking until it is complete.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="first">The first page of the snapshot, read with offset 0.</param>
    /// <param name="itemId">The item id filter the first page was read with.</param>
    /// <param name="maxItems">The maximum number of items to read, or <see langword="null"/> to read every matching item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested items, with <c>NextOffset</c> set when more matching items remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxItems"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="first"/> is not a valid first page, or the snapshot changes or becomes unavailable while it is read.</exception>
    public static InventoryFurniPage CompleteFurni(
        IApplicationRuntime application,
        InventoryFurniPage first,
        Id? itemId = null,
        int? maxItems = null,
        CancellationToken cancellationToken = default) =>
        CompleteFurniAsync(
                application,
                first,
                itemId,
                maxItems,
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>Reads the remaining pages of a furni inventory snapshot and joins them with the first page.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="first">The first page of the snapshot, read with offset 0.</param>
    /// <param name="itemId">The item id filter the first page was read with.</param>
    /// <param name="maxItems">The maximum number of items to read, or <see langword="null"/> to read every matching item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested items, with <c>NextOffset</c> set when more matching items remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxItems"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="first"/> is not a valid first page, or the snapshot changes or becomes unavailable while it is read.</exception>
    public static ValueTask<InventoryFurniPage> CompleteFurniAsync(
        IApplicationRuntime application,
        InventoryFurniPage first,
        Id? itemId = null,
        int? maxItems = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        ValidateMaximum(maxItems);
        cancellationToken.ThrowIfCancellationRequested();
        return Complete<InventoryFurniPage, InventoryItemSnapshot>(
            first,
            maxItems,
            async (offset, limit) => await application
                .InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniList,
                    new InventoryFurniPageRequest(
                        itemId,
                        offset,
                        limit,
                        first.SnapshotRevision),
                    cancellationToken)
                .ConfigureAwait(false),
            static (page, values, next_offset) => page with
            {
                Offset = 0,
                NextOffset = next_offset,
                Items = Array.AsReadOnly(values.ToArray())
            },
            "furni inventory");
    }

    /// <summary>Reads the pet inventory and joins every page into one page, blocking until it is complete.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="petId">The id of the only pet to read, or <see langword="null"/> to skip the id filter.</param>
    /// <param name="name">The exact pet name to match, ignoring case, or <see langword="null"/> to skip the name filter.</param>
    /// <param name="maxPets">The maximum number of pets to read, or <see langword="null"/> to read every matching pet.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested pets, with <c>NextOffset</c> set when more matching pets remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPets"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the snapshot changes or becomes unavailable while it is read.</exception>
    public static InventoryPetPage ReadPets(
        IApplicationRuntime application,
        Id? petId = null,
        string? name = null,
        int? maxPets = null,
        CancellationToken cancellationToken = default) =>
        ReadPetsAsync(application, petId, name, maxPets, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>Reads the pet inventory and joins every page into one page.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="petId">The id of the only pet to read, or <see langword="null"/> to skip the id filter.</param>
    /// <param name="name">The exact pet name to match, ignoring case, or <see langword="null"/> to skip the name filter.</param>
    /// <param name="maxPets">The maximum number of pets to read, or <see langword="null"/> to read every matching pet.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested pets, with <c>NextOffset</c> set when more matching pets remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPets"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the snapshot changes or becomes unavailable while it is read.</exception>
    public static async ValueTask<InventoryPetPage> ReadPetsAsync(
        IApplicationRuntime application,
        Id? petId = null,
        string? name = null,
        int? maxPets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ValidateMaximum(maxPets);
        InventoryPetPage first = await application
            .InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsList,
                new InventoryPetPageRequest(
                    PetId: petId,
                    Name: name,
                    Limit: FirstLimit(maxPets)),
                cancellationToken)
            .ConfigureAwait(false);
        return await CompletePetsAsync(
            application,
            first,
            petId,
            name,
            maxPets,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the remaining pages of a pet inventory snapshot and joins them with the first page, blocking until it is complete.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="first">The first page of the snapshot, read with offset 0.</param>
    /// <param name="petId">The pet id filter the first page was read with.</param>
    /// <param name="name">The pet name filter the first page was read with.</param>
    /// <param name="maxPets">The maximum number of pets to read, or <see langword="null"/> to read every matching pet.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested pets, with <c>NextOffset</c> set when more matching pets remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPets"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="first"/> is not a valid first page, or the snapshot changes or becomes unavailable while it is read.</exception>
    public static InventoryPetPage CompletePets(
        IApplicationRuntime application,
        InventoryPetPage first,
        Id? petId = null,
        string? name = null,
        int? maxPets = null,
        CancellationToken cancellationToken = default) =>
        CompletePetsAsync(
                application,
                first,
                petId,
                name,
                maxPets,
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>Reads the remaining pages of a pet inventory snapshot and joins them with the first page.</summary>
    /// <param name="application">The application runtime to read through.</param>
    /// <param name="first">The first page of the snapshot, read with offset 0.</param>
    /// <param name="petId">The pet id filter the first page was read with.</param>
    /// <param name="name">The pet name filter the first page was read with.</param>
    /// <param name="maxPets">The maximum number of pets to read, or <see langword="null"/> to read every matching pet.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page that holds the requested pets, with <c>NextOffset</c> set when more matching pets remain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPets"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="first"/> is not a valid first page, or the snapshot changes or becomes unavailable while it is read.</exception>
    public static ValueTask<InventoryPetPage> CompletePetsAsync(
        IApplicationRuntime application,
        InventoryPetPage first,
        Id? petId = null,
        string? name = null,
        int? maxPets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        ValidateMaximum(maxPets);
        cancellationToken.ThrowIfCancellationRequested();
        return Complete<InventoryPetPage, InventoryPetSnapshot>(
            first,
            maxPets,
            async (offset, limit) => await application
                .InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                    ApplicationMemberIds.InventoryPetsList,
                    new InventoryPetPageRequest(
                        petId,
                        name,
                        offset,
                        limit,
                        first.SnapshotRevision),
                    cancellationToken)
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

/// <summary>Defines a page read from one retained inventory snapshot.</summary>
/// <typeparam name="TValue">The type of the entries in the page.</typeparam>
public interface IInventoryApplicationPage<out TValue>
{
    /// <summary>Gets whether the snapshot belongs to the active hotel session.</summary>
    bool Connected { get; }
    /// <summary>Gets the generation of the hotel session the snapshot belongs to.</summary>
    long SessionGeneration { get; }
    /// <summary>Gets the inventory state revision when the snapshot was captured.</summary>
    long Revision { get; }
    /// <summary>Gets the revision of the retained snapshot, to pass when reading the next page.</summary>
    long SnapshotRevision { get; }
    /// <summary>Gets the revision of the inventory collection, which increases with every change to it.</summary>
    long InventoryRevision { get; }
    /// <summary>Gets the load generation of the inventory collection.</summary>
    long LoadGeneration { get; }
    /// <summary>Gets whether the complete collection has been received.</summary>
    bool Loaded { get; }
    /// <summary>Gets whether a request is pending or its fragments are still arriving.</summary>
    bool Loading { get; }
    /// <summary>Gets whether the collection holds entries that a completed load has not confirmed yet.</summary>
    bool Stale { get; }
    /// <summary>Gets whether a completed load was discarded because it could not be matched to the active request.</summary>
    bool RecoveryPending { get; }
    /// <summary>Gets the number of fragments the current load expects, or -1 when it is not known yet.</summary>
    int ExpectedFragments { get; }
    /// <summary>Gets the number of fragments received in the current load.</summary>
    int ReceivedFragments { get; }
    /// <summary>Gets the number of entries in the collection.</summary>
    int Total { get; }
    /// <summary>Gets the number of entries that match the filters.</summary>
    int Matched { get; }
    /// <summary>Gets the zero-based index of the first entry in the page.</summary>
    int Offset { get; }
    /// <summary>Gets the offset of the next page, or <see langword="null"/> when there are no more matching entries.</summary>
    int? NextOffset { get; }
    /// <summary>Gets the entries in the page.</summary>
    IReadOnlyList<TValue> Values { get; }
}
