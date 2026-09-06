using Qx.Game.Protocol;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Scripting;

/// <content>
/// Private messages between friends, which the hotel calls the console. This is a different channel
/// from room chat: a message here reaches a friend wherever they are, including while they are
/// offline.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Subscribes to private messages from friends.
    /// </summary>
    /// <remarks>
    /// Messages that were waiting while the account was away arrive shortly after connecting and
    /// carry their age, so a handler that answers every message will also answer the backlog.
    /// Check <see cref="NewConsoleMessage.IsOffline"/> to tell the two apart. A message carries
    /// either text or an icon, never both.
    /// </remarks>
    /// <param name="handler">Receives the message, its sender and its age.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnPrivateMessage(Action<NewConsoleMessage> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<FriendMessageEntry>(
            ApplicationMemberIds.FriendMessageReceived,
            Guarded<FriendMessageEntry>(entry => handler(LegacyMessage(entry)))));
    }

    /// <summary>
    /// Subscribes to private messages, receiving the sender and the text directly.
    /// </summary>
    /// <remarks>
    /// Icon-only messages carry no text and are skipped by this overload; take the full message if
    /// they matter.
    /// </remarks>
    /// <param name="handler">Receives the sender's name and the text.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnPrivateMessage(Action<string, string> handler)
    {
        void Wrapper(FriendMessageEntry message)
        {
            if (message.ContentType != ConsoleMessageContent.TypeHabbicon)
                handler(message.SenderName, message.Text);
        }

        return Track(Application.Subscribe<FriendMessageEntry>(
            ApplicationMemberIds.FriendMessageReceived,
            Guarded<FriendMessageEntry>(Wrapper)));
    }

    /// <summary>Subscribes to the hotel refusing a messenger operation.</summary>
    /// <param name="handler">Receives the failed request and the reason.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnMessengerError(Action<MessengerError> handler)
    {
        return Track(Application.Subscribe(
            ApplicationMemberIds.FriendOperationFailed,
            Guarded(handler)));
    }

    /// <summary>Subscribes to a private message failing to reach its recipient.</summary>
    /// <param name="handler">Receives the reason and who it was meant for.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnPrivateMessageFailed(Action<InstantMessageError> handler)
    {
        return Track(Application.Subscribe(
            ApplicationMemberIds.FriendMessageFailed,
            Guarded(handler)));
    }

    private static NewConsoleMessage LegacyMessage(FriendMessageEntry entry) => new(
        entry.ChatId,
        new ConsoleMessageContent(entry.ContentType, entry.Text, entry.HabbiconId),
        entry.SecondsSinceSent,
        entry.MessageId,
        entry.ConfirmationId,
        entry.SenderId,
        entry.SenderName,
        entry.SenderFigure,
        entry.LegacyCompact);

    /// <summary>Everyone the local user has blocked, whose chat the client hides.</summary>
    public IReadOnlyCollection<long> BlockedUsers =>
        ReadProfileIds(ApplicationMemberIds.ProfileBlocksList)
            .Select(user_id => (long)user_id)
            .ToArray();

    /// <summary>Whether a user is on the local user's block list.</summary>
    /// <param name="userId">The user to check.</param>
    public bool IsBlocked(Id userId) =>
        ReadProfileIds(ApplicationMemberIds.ProfileBlocksList).Contains(userId);

    /// <summary>The wardrobe figure parts the local user owns beyond the default set.</summary>
    public IReadOnlyCollection<int> OwnedFigureSets =>
        ReadFigureSets().Select(entry => entry.FigureSetId).ToArray();

    public IReadOnlyDictionary<int, int> OwnedFigureSetMetadata
    {
        get
        {
            var metadata = new Dictionary<int, int>();
            foreach (FigureSetEntry entry in ReadFigureSets())
                metadata[entry.FigureSetId] = entry.Metadata;
            return metadata;
        }
    }

    /// <summary>
    /// The sanctions recorded against the local user, or <see langword="null"/> until the hotel
    /// reports them.
    /// </summary>
    public MySanctionStatus? MySanctions
    {
        get
        {
            ProfileSanctionsPage sanctions = ReadSanctions();
            return sanctions.Loaded && sanctions.Kind is AccountSanctionStatusKind.Sanctions
                ? new MySanctionStatus(sanctions.Sanctions)
                : null;
        }
    }

    public CfhSanctionStatus? UnitySanctions => ReadSanctions().CallForHelp;

    /// <summary>Subscribes to the block list arriving or changing.</summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnBlockListChanged(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            Guarded<ProfileChanged>(change =>
            {
                if (change.Kind is ProfileChangeKind.BlockList or
                    ProfileChangeKind.BlockResult or
                    ProfileChangeKind.Reset)
                {
                    handler();
                }
            })));
    }

    /// <summary>Subscribes to the owned wardrobe parts changing.</summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFigureSetsChanged(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            Guarded<ProfileChanged>(change =>
            {
                if (change.Kind is ProfileChangeKind.FigureSets or ProfileChangeKind.Reset)
                    handler();
            })));
    }

    /// <summary>Subscribes to the hotel reporting the account's sanctions.</summary>
    /// <param name="handler">Receives the sanction status.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnSanctionsChanged(Action<MySanctionStatus> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            Guarded<ProfileChanged>(change =>
            {
                if (change.Kind is ProfileChangeKind.Sanctions && MySanctions is { } sanctions)
                    handler(sanctions);
            })));
    }

    public IDisposable OnUnitySanctionsChanged(Action<CfhSanctionStatus> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<ProfileChanged>(
            ApplicationMemberIds.ProfileChanged,
            Guarded<ProfileChanged>(change =>
            {
                if (change.Kind is ProfileChangeKind.Sanctions && UnitySanctions is { } sanctions)
                    handler(sanctions);
            })));
    }

    /// <summary>
    /// Subscribes to a room avatar's favourite-group badge changing.
    /// </summary>
    /// <remarks>
    /// The avatar is named by its room index, so resolve it through the room rather than the
    /// friend list.
    /// </remarks>
    /// <param name="handler">Receives the change.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFavouriteGroupChanged(Action<FavouriteMembershipUpdate> handler) =>
        OnIn(
            MessageContracts.Room.Occupants.Identity.FavoriteGroup,
            message => handler(new FavouriteMembershipUpdate(
                message.Index,
                message.GroupId,
                message.Status,
                message.GroupName)));

    /// <summary>Subscribes to a Flash special-system chat signal associated with an avatar.</summary>
    /// <param name="handler">Receives the avatar index and the special chat signal.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnSpecialSystemChat(Action<SpecialSystemChat> handler) =>
        OnIn(MessageContracts.Room.Chat.SpecialSystem, handler);

    /// <summary>Subscribes to the Flash hotel's messages of the day, which arrive on connect.</summary>
    /// <param name="handler">Receives the notices.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnMessageOfTheDay(Action<MOTDNotification> handler) =>
        OnIn(MessageContracts.Notifications.MessageOfTheDay, handler);

    /// <summary>Subscribes to the Flash recycler's state, which reports whether one is running.</summary>
    /// <param name="handler">Receives the state and the remaining seconds.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnRecyclerStatus(Action<RecyclerStatus> handler) =>
        OnIn(MessageContracts.Recycler.Status, handler);

    /// <summary>Subscribes to a Flash recycler session ending.</summary>
    /// <param name="handler">Receives how it ended and what it produced.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnRecyclerFinished(Action<RecyclerFinished> handler) =>
        OnIn(MessageContracts.Recycler.Finished, handler);

    /// <summary>
    /// Returns the block list, asking the hotel for it when this session never saw it.
    /// </summary>
    /// <remarks>
    /// The hotel sends the list once, early on. QX can be attached to a session that is already
    /// running, in which case it never saw that message and <see cref="BlockedUsers"/> reads empty -
    /// which is indistinguishable from "nobody is blocked". Use this when the answer has to be
    /// right rather than merely available.
    /// </remarks>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <exception cref="TimeoutException">The hotel did not answer in time.</exception>
    public async Task<IReadOnlyCollection<long>> GetBlockedUsers(int timeoutMs = 10000)
    {
        ProfileIdPage first_page = await Application.InvokeAsync<ProfileIdRefreshRequest, ProfileIdPage>(
            ApplicationMemberIds.ProfileBlocksRefresh,
            new ProfileIdRefreshRequest(Limit: 500, TimeoutMilliseconds: timeoutMs),
            Ct);
        return ReadProfileIds(ApplicationMemberIds.ProfileBlocksList, first_page)
            .Select(user_id => (long)user_id)
            .ToArray();
    }

    private IReadOnlyList<Id> ReadProfileIds(
        string member_id,
        ProfileIdPage? first_page = null)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ProfileIdPage page = first_page ?? Application.Invoke<ProfileIdPageRequest, ProfileIdPage>(
                member_id,
                new ProfileIdPageRequest(Limit: 500),
                Ct);
            first_page = null;
            long generation = page.Generation;
            long revision = page.Revision;
            int total = page.Total;
            var values = new List<Id>(total);
            values.AddRange(page.UserIds);
            int? next_offset = page.NextOffset;
            bool stable = page.Offset == 0;

            while (stable && next_offset is int offset)
            {
                ProfileIdPage next_page = Application.Invoke<ProfileIdPageRequest, ProfileIdPage>(
                    member_id,
                    new ProfileIdPageRequest(offset, 500),
                    Ct);
                stable = next_page.Generation == generation &&
                    next_page.Revision == revision &&
                    next_page.Total == total &&
                    next_page.Offset == offset &&
                    (next_page.NextOffset is not int following || following > offset);
                if (!stable)
                    break;
                values.AddRange(next_page.UserIds);
                next_offset = next_page.NextOffset;
            }

            if (stable && values.Count == total)
                return Array.AsReadOnly(values.ToArray());
        }

        throw new InvalidOperationException("The profile list changed while it was being read.");
    }

    private IReadOnlyList<FigureSetEntry> ReadFigureSets()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ProfileFigureSetsPage page = Application.Invoke<ProfileFigureSetsRequest, ProfileFigureSetsPage>(
                ApplicationMemberIds.ProfileFigureSetsList,
                new ProfileFigureSetsRequest(Limit: 500),
                Ct);
            long generation = page.Generation;
            long revision = page.Revision;
            int total = page.Total;
            var values = new List<FigureSetEntry>(total);
            values.AddRange(page.FigureSets);
            int? next_offset = page.NextOffset;
            bool stable = page.Offset == 0;

            while (stable && next_offset is int offset)
            {
                ProfileFigureSetsPage next_page = Application.Invoke<ProfileFigureSetsRequest, ProfileFigureSetsPage>(
                    ApplicationMemberIds.ProfileFigureSetsList,
                    new ProfileFigureSetsRequest(offset, 500),
                    Ct);
                stable = next_page.Generation == generation &&
                    next_page.Revision == revision &&
                    next_page.Total == total &&
                    next_page.Offset == offset &&
                    (next_page.NextOffset is not int following || following > offset);
                if (!stable)
                    break;
                values.AddRange(next_page.FigureSets);
                next_offset = next_page.NextOffset;
            }

            if (stable && values.Count == total)
                return Array.AsReadOnly(values.ToArray());
        }

        throw new InvalidOperationException("The figure-set list changed while it was being read.");
    }

    private ProfileSanctionsPage ReadSanctions()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ProfileSanctionsPage page = Application.Invoke<ProfileSanctionsRequest, ProfileSanctionsPage>(
                ApplicationMemberIds.ProfileSanctionsList,
                new ProfileSanctionsRequest(Limit: 500),
                Ct);
            if (page.Kind is not AccountSanctionStatusKind.Sanctions || page.NextOffset is null)
                return page;

            long generation = page.Generation;
            long revision = page.Revision;
            int total = page.Total;
            var values = new List<Sanction>(total);
            values.AddRange(page.Sanctions);
            int? next_offset = page.NextOffset;
            bool stable = page.Offset == 0;

            while (stable && next_offset is int offset)
            {
                ProfileSanctionsPage next_page = Application.Invoke<ProfileSanctionsRequest, ProfileSanctionsPage>(
                    ApplicationMemberIds.ProfileSanctionsList,
                    new ProfileSanctionsRequest(offset, 500),
                    Ct);
                stable = next_page.Generation == generation &&
                    next_page.Revision == revision &&
                    next_page.Kind == page.Kind &&
                    next_page.Total == total &&
                    next_page.Offset == offset &&
                    (next_page.NextOffset is not int following || following > offset);
                if (!stable)
                    break;
                values.AddRange(next_page.Sanctions);
                next_offset = next_page.NextOffset;
            }

            if (stable && values.Count == total)
            {
                return page with
                {
                    Offset = 0,
                    NextOffset = null,
                    Sanctions = Array.AsReadOnly(values.ToArray())
                };
            }
        }

        throw new InvalidOperationException("The sanction list changed while it was being read.");
    }
}
