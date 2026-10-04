using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Friend list loading, user search, relationships and friend list events.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the friend list, loading it from the hotel when it has not been received yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Friends"/> is the cached snapshot and is empty until the hotel has sent the list.
    /// A script that attached to a session already in progress therefore reads nothing from it and
    /// concludes the account has no friends. This asks instead.
    /// </para>
    /// <para>
    /// The list is read in pages of 500 and retried up to three times when it changes while being
    /// read, so the result is one consistent snapshot.
    /// </para>
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for loading the list, from 1 to 120000.</param>
    /// <returns>The friends on the list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeoutMs"/> is zero or negative, or above 120000 when the list has to be loaded.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the hotel session changed while the list was read, or the list kept changing on every attempt.
    /// </exception>
    public Task<IReadOnlyCollection<Friend>> GetFriends(int timeoutMs = 10000) =>
        LoadFriends(timeoutMs, Ct);

    /// <summary>Gets the friends who are online, loading the list first when needed.</summary>
    /// <param name="timeoutMs">The timeout in milliseconds for loading the list.</param>
    /// <returns>The friends whose <see cref="Friend.IsOnline"/> is <see langword="true"/>.</returns>
    public async Task<IReadOnlyList<Friend>> GetOnlineFriends(int timeoutMs = 10000)
    {
        IReadOnlyCollection<Friend> friends = await GetFriends(timeoutMs);
        return friends.Where(friend => friend.IsOnline).ToArray();
    }

    /// <summary>
    /// Asks the hotel to search for users by name.
    /// </summary>
    /// <remarks>
    /// The search runs in the background and the call returns at once. The answer arrives as a
    /// separate message that also reaches the game client; read it with
    /// <c>OnIn&lt;UserSearchResults&gt;("HabboSearchResult", result =&gt; ...)</c>, or await
    /// <see cref="SearchUsers(string, int)"/> instead. A failure, such as no answer within 10000
    /// milliseconds, is reported as a background script error.
    /// </remarks>
    /// <param name="query">The name or fragment to search for.</param>
    public void RequestUserSearch(string query) => StartObservedTask(
        async () =>
        {
            await _application.InvokeAsync(
                ApplicationMemberIds.FriendsSearch,
                new FriendsSearchRequest(query),
                Ct);
        },
        Ct);

    /// <summary>Asks the hotel to send the pending friend requests.</summary>
    /// <remarks>
    /// The request runs in the background and the call returns at once. The reply also reaches the
    /// game client. A failure, such as no reply within 10000 milliseconds, is reported as a
    /// background script error.
    /// </remarks>
    public void RequestFriendRequests() => StartObservedTask(
        async () =>
        {
            await _application.InvokeAsync<FriendRequestsListRequest, PendingFriendRequests>(
                ApplicationMemberIds.FriendRequestsList,
                new FriendRequestsListRequest(),
                Ct);
        },
        Ct);

    /// <summary>
    /// Sets the relationship shown against a friend, which is the heart, smile or bobba the client
    /// draws on their entry.
    /// </summary>
    /// <param name="friendId">The id of the friend.</param>
    /// <param name="relationship">The relationship to show, or <see cref="RelationshipType.None"/> to clear it.</param>
    public void SetRelationship(Id friendId, RelationshipType relationship) =>
        _application.Invoke<FriendRelationshipSetRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendRelationshipSet,
            new FriendRelationshipSetRequest(friendId, relationship),
            Ct);

    /// <summary>Sets the relationship shown against a friend, by name.</summary>
    /// <param name="name">The friend's name.</param>
    /// <param name="relationship">The relationship to show.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no friend by that name in the cached friend list.</exception>
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
        _application.Invoke<FriendsRemoveRequest, FriendOperationResult>(
            ApplicationMemberIds.FriendsRemove,
            new FriendsRemoveRequest(friendIds),
            Ct);

    /// <summary>Removes several friends in one message.</summary>
    /// <param name="friendIds">The account ids to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friendIds"/> is <see langword="null"/>.</exception>
    public void RemoveFriends(IEnumerable<Id> friendIds)
    {
        ArgumentNullException.ThrowIfNull(friendIds);
        RemoveFriends(friendIds.ToArray());
    }

    /// <summary>Removes several friends in one message, skipping null entries.</summary>
    /// <param name="friends">The friends to remove; only their ids are used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="friends"/> is <see langword="null"/>.</exception>
    public void RemoveFriends(IEnumerable<Friend> friends)
    {
        ArgumentNullException.ThrowIfNull(friends);
        RemoveFriends(friends
            .Where(friend => friend is not null)
            .Select(friend => friend.Id)
            .ToArray());
    }

    private async Task<IReadOnlyCollection<Friend>> LoadFriends(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_milliseconds, 0);
        cancellation_token.ThrowIfCancellationRequested();
        var expected_session = Session;

        FriendListPage first = _application.Invoke<FriendsListRequest, FriendListPage>(
            ApplicationMemberIds.FriendsList,
            new FriendsListRequest(Limit: 500),
            cancellation_token);
        RequireSameSession();
        if (!first.Loaded)
        {
            first = await _application.InvokeAsync<FriendsRefreshRequest, FriendListPage>(
                ApplicationMemberIds.FriendsRefresh,
                new FriendsRefreshRequest(Limit: 500, TimeoutMilliseconds: timeout_milliseconds),
                cancellation_token);
            RequireSameSession();
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt != 0)
            {
                first = _application.Invoke<FriendsListRequest, FriendListPage>(
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
                FriendListPage page = _application.Invoke<FriendsListRequest, FriendListPage>(
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
            LastOnline = snapshot.LastOnline
        };
    }

    /// <summary>Registers a handler that runs when someone joins the friend list.</summary>
    /// <param name="handler">The handler to call with the new friend.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnFriendAdded(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Added, handler);

    /// <summary>
    /// Registers a handler that runs when a friend's details change, which is also how going
    /// online and offline is reported.
    /// </summary>
    /// <param name="handler">The handler to call with the friend as they now stand.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnFriendUpdated(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Updated, handler);

    /// <summary>Registers a handler that runs when someone leaves the friend list.</summary>
    /// <param name="handler">The handler to call with the removed friend.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnFriendRemoved(Action<Friend> handler)
        => OnFriendChange(FriendChangeKind.Removed, handler);

    private IDisposable OnFriendChange(FriendChangeKind kind, Action<Friend> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<FriendChanged>(
            ApplicationMemberIds.FriendsChanged,
            Guarded<FriendChanged>(change =>
            {
                if (change.Kind == kind && change.Friend is { } friend)
                    handler(FriendFromSnapshot(friend));
            })));
    }
}
