using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <summary>
/// The friend actions that were missing from <see cref="ScriptGlobals"/>: loading the list
/// rather than reading whatever happens to be cached, searching the hotel, relationships, and the
/// membership events.
/// </summary>
public partial class ScriptGlobals
{
    /// <summary>
    /// The friend list, loading it from the hotel when it has not been seen.
    /// </summary>
    /// <remarks>
    /// <see cref="Friends"/> is the cached snapshot and is empty until the hotel has sent the list.
    /// A script that attached to a session already in progress therefore reads nothing from it and
    /// concludes the account has no friends. This asks instead.
    /// </remarks>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<IReadOnlyCollection<Friend>> GetFriends(int timeoutMs = 10000) =>
        LoadFriends(timeoutMs, Ct);

    /// <summary>The friends who are online, loading the list first when needed.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Friend>> GetOnlineFriends(int timeoutMs = 10000)
    {
        IReadOnlyCollection<Friend> friends = await GetFriends(timeoutMs);
        return friends.Where(friend => friend.IsOnline).ToArray();
    }

    /// <summary>
    /// Searches the hotel for users by name.
    /// </summary>
    /// <remarks>
    /// The answer arrives as a separate message; read it with
    /// <c>OnIn&lt;HabboSearchResult&gt;(result =&gt; ...)</c>.
    /// </remarks>
    /// <param name="query">The name or fragment to search for.</param>
    public void SearchUsers(string query) => StartObservedTask(
        async () =>
        {
            await Application.InvokeAsync(
                ApplicationMemberIds.FriendsSearch,
                new FriendsSearchRequest(query),
                Ct);
        },
        Ct);

    /// <summary>Asks the hotel to send the pending friend requests.</summary>
    public void RequestFriendRequests() => StartObservedTask(
        async () =>
        {
            await Application.InvokeAsync<FriendRequestsListRequest, PendingFriendRequests>(
                ApplicationMemberIds.FriendRequestsList,
                new FriendRequestsListRequest(),
                Ct);
        },
        Ct);

    /// <summary>
    /// Sets the relationship shown against a friend, which is the heart, smile or bobba the client
    /// draws on their entry.
    /// </summary>
    /// <param name="friendId">The friend.</param>
    /// <param name="relationship">The relationship to show, or <see cref="RelationshipType.None"/> to clear it.</param>
    public void SetRelationship(Id friendId, RelationshipType relationship) =>
        Application.Invoke<FriendRelationshipSetRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRelationshipSet,
            new FriendRelationshipSetRequest(friendId, relationship),
            Ct);

    /// <summary>Sets the relationship shown against a friend, by name.</summary>
    /// <param name="name">The friend's name.</param>
    /// <param name="relationship">The relationship to show.</param>
    /// <exception cref="InvalidOperationException">There is no friend by that name.</exception>
    public void SetRelationship(string name, RelationshipType relationship)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Friend friend = Game.Friends.FriendByName(name)
            ?? throw new InvalidOperationException($"'{name}' is not on the friend list.");
        SetRelationship(friend.Id, relationship);
    }

    /// <summary>Removes several friends in one message.</summary>
    /// <param name="friendIds">The friends to remove.</param>
    public void RemoveFriends(params Id[] friendIds) =>
        Application.Invoke<FriendsRemoveRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendsRemove,
            new FriendsRemoveRequest(friendIds),
            Ct);

    private async Task<IReadOnlyCollection<Friend>> LoadFriends(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_milliseconds, 0);
        cancellation_token.ThrowIfCancellationRequested();
        var expected_session = Session;

        FriendListPage first = Application.Invoke<FriendsListRequest, FriendListPage>(
            ApplicationMemberIds.FriendsList,
            new FriendsListRequest(Limit: 500),
            cancellation_token);
        RequireSameSession();
        if (!first.Loaded)
        {
            first = await Application.InvokeAsync<FriendsRefreshRequest, FriendListPage>(
                ApplicationMemberIds.FriendsRefresh,
                new FriendsRefreshRequest(Limit: 500, TimeoutMilliseconds: timeout_milliseconds),
                cancellation_token);
            RequireSameSession();
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt != 0)
            {
                first = Application.Invoke<FriendsListRequest, FriendListPage>(
                    ApplicationMemberIds.FriendsList,
                    new FriendsListRequest(Limit: 500),
                    cancellation_token);
                RequireSameSession();
            }
            if (!first.Loaded)
                throw new InvalidOperationException("The friend state reset while the snapshot was being read.");

            var snapshots = new List<FriendSnapshot>(first.Friends);
            int? next_offset = first.NextOffset;
            bool consistent = true;
            while (next_offset is int offset)
            {
                FriendListPage page = Application.Invoke<FriendsListRequest, FriendListPage>(
                    ApplicationMemberIds.FriendsList,
                    new FriendsListRequest(Offset: offset, Limit: 500),
                    cancellation_token);
                RequireSameSession();
                if (page.Generation != first.Generation || page.Revision != first.Revision)
                {
                    consistent = false;
                    break;
                }
                snapshots.AddRange(page.Friends);
                next_offset = page.NextOffset;
            }
            if (consistent)
            {
                RequireSameSession();
                return snapshots.Select(FriendFromSnapshot).ToArray();
            }
        }
        throw new InvalidOperationException("The friend list changed continuously while it was being read.");

        void RequireSameSession()
        {
            if (!ReferenceEquals(Session, expected_session))
                throw new InvalidOperationException("The session changed while the friend list was being read.");
        }
    }

    private static Friend FriendFromSnapshot(FriendSnapshot snapshot)
    {
        _ = Enum.TryParse(snapshot.Gender, true, out Gender gender);
        _ = Enum.TryParse(snapshot.Relation, true, out Relation relation);
        return new Friend
        {
            Id = snapshot.Id,
            Name = snapshot.Name,
            Gender = gender,
            IsOnline = snapshot.IsOnline,
            CanFollow = snapshot.CanFollow,
            Figure = snapshot.Figure,
            CategoryId = snapshot.CategoryId,
            Motto = snapshot.Motto,
            RealName = snapshot.RealName,
            FacebookId = snapshot.FacebookId,
            IsAcceptingOfflineMessages = snapshot.IsAcceptingOfflineMessages,
            IsVipMember = snapshot.IsVipMember,
            IsPocketHabboUser = snapshot.IsPocketHabboUser,
            Relation = relation,
            LastOnline = snapshot.LastOnline,
            UnityStatus = snapshot.UnityStatus,
            UnityPlatform = snapshot.UnityPlatform
        };
    }

    /// <summary>Runs a callback whenever someone joins the friend list.</summary>
    /// <param name="handler">Receives the friend.</param>
    public void OnFriendAdded(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Added, handler);

    /// <summary>
    /// Runs a callback whenever a friend's details change, which is also how going online and
    /// offline is reported.
    /// </summary>
    /// <param name="handler">Receives the friend as they now stand.</param>
    public void OnFriendUpdated(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Updated, handler);

    /// <summary>Runs a callback whenever someone leaves the friend list.</summary>
    /// <param name="handler">Receives the friend.</param>
    public void OnFriendRemoved(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Removed, handler);

    private void OnFriendChange(FriendChangeKind kind, Action<Friend> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Track(Application.Subscribe<FriendChanged>(
            ApplicationMemberIds.FriendsChanged,
            Guarded<FriendChanged>(change =>
            {
                if (change.Kind == kind && change.Friend is { } friend)
                    handler(FriendFromSnapshot(friend));
            })));
    }
}
