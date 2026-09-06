using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Interception;

namespace Qx.Game.Snapshots;

/// <summary>
/// Projects live game state into the immutable snapshot records that every read query and
/// every MCP read tool serialises.
/// </summary>
/// <remarks>
/// <para>
/// Every method here is a pure projection: it copies out of the live managers and returns a
/// detached value. Nothing blocks, nothing touches the network and nothing waits for data to
/// arrive. Whatever has not been received yet simply shows up as <see langword="null"/> or as
/// an empty collection, which is why the callers pair these snapshots with a
/// <see cref="QueryMetadataSnapshot"/> that says whether that emptiness is real.
/// </para>
/// <para>
/// Two independent limits guard the projections. The <c>sourceItemLimit</c> parameters are a
/// safety valve against an unbounded or runaway source: reaching
/// <see cref="DefaultSourceItemLimit"/> items throws
/// <see cref="SnapshotSourceLimitExceededException"/> rather than materialising forever. The
/// <c>maxItems</c>, <c>maxItemsPerType</c> and <c>maxTiles</c> parameters are the ordinary
/// output cap and silently drop items, which the returned snapshot then reports through its
/// own truncation flag and its returned-versus-total counts. Collections without a
/// <c>maxItems</c> parameter (avatars, friends, controllers) are never truncated.
/// </para>
/// </remarks>
public static partial class SnapshotFactory
{
    /// <summary>
    /// Describes the state of the interceptor link, the hotel session and the analysis of the
    /// connected client build.
    /// </summary>
    /// <param name="session">The open hotel session, or <see langword="null"/> when none is open.</param>
    /// <param name="interceptorConnected">Whether the packet interceptor is attached.</param>
    /// <param name="messageCatalogLoaded">Whether the message-name catalog is available.</param>
    /// <param name="wireProfileAnalyzed">Whether the connected client build has been analysed.</param>
    /// <param name="wireProfileExact">Whether the analysis matched this build exactly rather than falling back.</param>
    /// <param name="missingWireCapabilities">
    /// Wire capabilities the connected build lacks; <see langword="null"/> is treated as none.
    /// </param>
    /// <returns>
    /// The connection snapshot. With no session the client, host, port, hotel version and
    /// client identifier are all <see langword="null"/>.
    /// </returns>
    public static ConnectionSnapshot Connection(
        Session? session,
        bool interceptorConnected,
        bool messageCatalogLoaded = false,
        bool wireProfileAnalyzed = false,
        bool wireProfileExact = false,
        IReadOnlyList<string>? missingWireCapabilities = null) =>
        new(
            interceptorConnected,
            session is not null,
            messageCatalogLoaded,
            wireProfileAnalyzed,
            wireProfileExact,
            missingWireCapabilities?.ToArray() ?? [],
            session?.Client.ToString(),
            session?.Host,
            session?.Port,
            session?.HotelVersion,
            session?.ClientIdentifier);

    /// <summary>Projects a navigator room record, flattening its enumerations to their wire values.</summary>
    /// <param name="data">The room record to project.</param>
    /// <returns>The room data snapshot, with tags copied into a detached array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static RoomDataSnapshot From(RoomData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new RoomDataSnapshot(
            data.Id,
            data.Name,
            data.OwnerId,
            data.OwnerName,
            (int)data.DoorMode,
            data.UserCount,
            data.MaxUserCount,
            data.Description,
            (int)data.TradeMode,
            data.Score,
            data.Ranking,
            data.Category,
            data.Tags.ToArray(),
            data.OfficialRoomPicRef,
            data.HasGroup,
            data.GroupId,
            data.GroupName,
            data.GroupBadge,
            data.HasEvent,
            data.EventName,
            data.EventDescription,
            data.EventMinutesRemaining,
            data.ShowOwner,
            data.AllowPets,
            data.DisplayRoomEntryAd);
    }

    /// <summary>
    /// Projects the entry side of the room session: door state, queues, the last connection
    /// failure and how the previous session ended.
    /// </summary>
    /// <param name="room">The room manager to read.</param>
    /// <returns>
    /// The access snapshot. The queue, failure, kick and exit blocks are
    /// <see langword="null"/> when nothing of that kind has happened.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="room"/> is <see langword="null"/>.</exception>
    public static RoomAccessSnapshot RoomAccess(RoomManager room)
    {
        ArgumentNullException.ThrowIfNull(room);

        RoomQueueSnapshot? queue = room.QueueStatus is { } status
            ? new RoomQueueSnapshot(
                status.RoomId,
                status.ActiveTarget is { } target ? (int)target : null,
                status.ActiveTarget?.ToString(),
                status.Position,
                status.Sets.Select(set => new RoomQueueSetSnapshot(
                    set.Name,
                    (int)set.Target,
                    set.Target.ToString(),
                    status.ActiveTarget == set.Target,
                    set.Position,
                    set.Queues.Select(entry =>
                        new RoomQueueEntrySnapshot(entry.Type, entry.Size)).ToArray())).ToArray())
            : null;

        RoomConnectionFailureSnapshot? failure = room.ConnectionFailure is { } connection_failure
            ? new RoomConnectionFailureSnapshot(
                connection_failure.Kind.ToString(),
                connection_failure.ReasonCode,
                connection_failure.Parameter)
            : null;

        return new RoomAccessSnapshot(
            room.AccessState.ToString(),
            room.AccessRoomId,
            room.IsRingingDoorbell,
            room.IsInQueue,
            room.QueuePosition,
            queue,
            failure,
            room.LastKick is { } last_kick ? From(last_kick) : null,
            room.LastExit is { } last_exit ? From(last_exit) : null,
            room.WasKicked);
    }

    /// <summary>Projects a kick of the local user out of a room.</summary>
    /// <param name="kick">The kick to project.</param>
    /// <returns>The kick snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kick"/> is <see langword="null"/>.</exception>
    public static RoomKickSnapshot From(RoomKick kick)
    {
        ArgumentNullException.ThrowIfNull(kick);
        return new RoomKickSnapshot(kick.RoomId, kick.ErrorCode, kick.WasEntered);
    }

    /// <summary>Projects how a room session ended, resolving the classified cause alongside the raw transport.</summary>
    /// <param name="exit">The exit state to project.</param>
    /// <returns>The exit snapshot, including the consumed kick when the exit was a kick.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exit"/> is <see langword="null"/>.</exception>
    public static RoomExitSnapshot From(RoomExitState exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        return new RoomExitSnapshot(
            exit.RoomId,
            exit.WasEntered,
            exit.Source.ToString(),
            exit.Cause.ToString(),
            exit.Reason,
            exit.HasNativeReason,
            exit.WasKicked,
            exit.Kick is { } kick ? From(kick) : null);
    }

    /// <summary>Projects the room's door tile and the facing an arriving avatar is given.</summary>
    /// <param name="tile">The entry tile to project.</param>
    /// <returns>The entry tile snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static RoomEntryTileSnapshot From(RoomEntryTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return new RoomEntryTileSnapshot(tile.X, tile.Y, tile.Direction);
    }

    /// <summary>
    /// Projects the room's wall and floor thickness, keeping both the wire value and the
    /// drawing multiplier the client derives from it.
    /// </summary>
    /// <param name="settings">The visualization settings to project.</param>
    /// <returns>The visualization snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static RoomVisualizationSettingsSnapshot From(RoomVisualizationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new RoomVisualizationSettingsSnapshot(
            settings.WallsHidden,
            (int)settings.WallThickness,
            (int)settings.FloorThickness,
            settings.WallThicknessMultiplier,
            settings.FloorThicknessMultiplier);
    }

    /// <summary>Projects the room's chat configuration, flattening its enumerations to their wire values.</summary>
    /// <param name="settings">The chat settings to project.</param>
    /// <returns>
    /// The chat snapshot. On the compact Flash guest-room layout only the flood setting comes
    /// from the hotel; the other four fields carry their defaults.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static RoomChatSettingsSnapshot From(RoomChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new RoomChatSettingsSnapshot(
            (int)settings.Flow,
            (int)settings.BubbleWidth,
            (int)settings.ScrollSpeed,
            settings.TalkHearingDistance,
            (int)settings.FloodProtection);
    }

    /// <summary>
    /// Projects the room's moderation permissions as the numeric values of
    /// <see cref="RoomModerationPermission"/>.
    /// </summary>
    /// <param name="settings">The moderation settings to project.</param>
    /// <returns>The moderation snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static RoomModerationSettingsSnapshot From(RoomModerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new RoomModerationSettingsSnapshot(
            (int)settings.Mute,
            (int)settings.Kick,
            (int)settings.Ban);
    }

    /// <summary>Projects the detail block that accompanies a guest room result.</summary>
    /// <param name="details">The detail block to project.</param>
    /// <returns>
    /// The details snapshot. The opening-connection flag is Flash only; the context identifier
    /// and thumbnail are Unity only, and the fields that do not apply keep their defaults.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="details"/> is <see langword="null"/>.</exception>
    public static RoomResultDetailsSnapshot From(RoomResultDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new RoomResultDetailsSnapshot(
            details.Forward,
            details.IsStaffPick,
            details.IsGroupMember,
            details.IsRoomMuted,
            From(details.Moderation),
            details.CanMute,
            From(details.Chat),
            details.OpeningConnection,
            details.UnityContextId,
            details.UnityThumbnail is { } thumbnail
                ? new RoomThumbnailSnapshot(thumbnail.RoomId, thumbnail.Reference, thumbnail.ImageUrl)
                : null);
    }

    /// <summary>Projects the room's decoration, door tile and chat configuration.</summary>
    /// <param name="room">The room manager to read.</param>
    /// <returns>
    /// The environment snapshot. Every block is <see langword="null"/> and the property map is
    /// empty until the corresponding packets arrive; the map is a detached copy.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="room"/> is <see langword="null"/>.</exception>
    public static RoomEnvironmentSnapshot RoomEnvironment(RoomManager room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return new RoomEnvironmentSnapshot(
            room.EntryTile is { } entry_tile ? From(entry_tile) : null,
            room.Properties,
            room.FloorProperty,
            room.WallpaperProperty,
            room.LandscapeProperty,
            room.AnimatedLandscapeProperty,
            room.VisualizationSettings is { } visualization ? From(visualization) : null,
            room.ChatSettings is { } chat ? From(chat) : null);
    }

    /// <summary>Projects what the local user is permitted to do in the current room.</summary>
    /// <param name="room">The room manager to read.</param>
    /// <returns>
    /// The authority snapshot. The muted, mute-permission and moderation members come from the
    /// guest room details and stay <see langword="null"/> until those details arrive.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="room"/> is <see langword="null"/>.</exception>
    public static RoomAuthoritySnapshot RoomAuthority(RoomManager room)
    {
        ArgumentNullException.ThrowIfNull(room);
        RoomResultDetails? details = room.Details;
        return new RoomAuthoritySnapshot(
            room.IsOwner,
            room.RightsLevel,
            room.RightsAreKnown,
            room.HasRights,
            room.IsSpectating,
            details?.IsRoomMuted,
            details?.CanMute,
            details is null ? null : From(details.Moderation));
    }

    /// <summary>Projects the room's static geometry, including its decoded height grid.</summary>
    /// <param name="floorPlan">The floor plan to project.</param>
    /// <returns>
    /// The floor plan snapshot. The camera fields are <see langword="null"/> when the client
    /// sent no camera hint, which happens on Unity builds that omit that tail.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="floorPlan"/> is <see langword="null"/>.</exception>
    public static FloorPlanSnapshot From(FloorPlan floorPlan)
    {
        ArgumentNullException.ThrowIfNull(floorPlan);

        return new FloorPlanSnapshot(
            floorPlan.UseLegacyScale,
            floorPlan.WallHeight,
            floorPlan.Map,
            floorPlan.Width,
            floorPlan.Length,
            floorPlan.Scale,
            floorPlan.Tiles.ToArray(),
            floorPlan.HiddenAreas
                .Select(area => new HiddenAreaSnapshot(
                    area.FurniId,
                    area.On,
                    area.RootX,
                    area.RootY,
                    area.Width,
                    area.Length,
                    area.Invert))
                .ToArray(),
            floorPlan.HasCameraData,
            floorPlan.HasCameraData ? floorPlan.CameraX : null,
            floorPlan.HasCameraData ? floorPlan.CameraY : null,
            floorPlan.HasCameraData ? floorPlan.CameraZ : null);
    }

    /// <summary>
    /// Counts the live heightmap without projecting its tiles, so walkability can be judged
    /// cheaply.
    /// </summary>
    /// <param name="heightmap">The heightmap to count.</param>
    /// <returns>
    /// The counts over every tile. Blocked counts only floor tiles that are blocked, and
    /// walkable counts floor tiles that are not blocked.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="heightmap"/> is <see langword="null"/>.</exception>
    public static HeightmapSummarySnapshot HeightmapSummary(Heightmap heightmap)
    {
        ArgumentNullException.ThrowIfNull(heightmap);

        int tile_count = 0;
        int floor_tiles = 0;
        int blocked_tiles = 0;
        int walkable_tiles = 0;
        foreach (HeightmapTile tile in heightmap.Tiles)
        {
            tile_count++;
            if (tile.IsFloor)
                floor_tiles++;
            if (tile.IsFloor && tile.IsBlocked)
                blocked_tiles++;
            if (tile.IsFree)
                walkable_tiles++;
        }

        return new HeightmapSummarySnapshot(
            heightmap.Width,
            heightmap.Length,
            tile_count,
            floor_tiles,
            walkable_tiles,
            blocked_tiles,
            tile_count - floor_tiles);
    }

    /// <summary>Projects every avatar in the sequence with no room context attached.</summary>
    /// <param name="avatars">The avatars to project.</param>
    /// <returns>The avatar collection, ordered ascending by room index and never truncated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="avatars"/> is <see langword="null"/>.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// The sequence yields <see cref="DefaultSourceItemLimit"/> items or more.
    /// </exception>
    public static AvatarCollectionSnapshot Avatars(IEnumerable<Avatar> avatars) =>
        Avatars(avatars, null, 0, DefaultSourceItemLimit);

    /// <summary>Projects every avatar in the sequence, tagged with the room session it was read under.</summary>
    /// <remarks>
    /// This projection has no output cap: every avatar is returned. Only the safety valve
    /// applies, and it throws rather than truncating.
    /// </remarks>
    /// <param name="avatars">The avatars to project.</param>
    /// <param name="roomId">The room the avatars belong to, or <see langword="null"/> when outside a room.</param>
    /// <param name="generation">The room session counter to stamp onto the snapshot.</param>
    /// <param name="sourceItemLimit">
    /// The safety valve against an unbounded source; reaching it throws.
    /// </param>
    /// <returns>The avatar collection, ordered ascending by room index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="avatars"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceItemLimit"/> is negative.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// The sequence yields <paramref name="sourceItemLimit"/> items or more.
    /// </exception>
    public static AvatarCollectionSnapshot Avatars(
        IEnumerable<Avatar> avatars,
        Id? roomId = null,
        long generation = 0,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        AvatarSnapshot[] projected = MaterializeBounded(
                avatars,
                sourceItemLimit,
                nameof(avatars))
            .OrderBy(avatar => avatar.Index)
            .Select(From)
            .ToArray();

        return new AvatarCollectionSnapshot(roomId, generation, projected.Length, projected);
    }

    /// <summary>
    /// Projects one avatar, filling in whichever of the user, pet and bot blocks matches its
    /// concrete type.
    /// </summary>
    /// <param name="avatar">The avatar to project.</param>
    /// <returns>
    /// The avatar snapshot. Its status block is <see langword="null"/> when no status update
    /// has been received yet, which also leaves a user's rights level at 0.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="avatar"/> is <see langword="null"/>.</exception>
    public static AvatarSnapshot From(Avatar avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        AvatarStatusSnapshot? status = avatar.CurrentUpdate is null
            ? null
            : From(avatar.CurrentUpdate);

        UserAvatarSnapshot? user = avatar is User roomUser
            ? new UserAvatarSnapshot(
                roomUser.Gender.ToString(),
                roomUser.GroupId,
                roomUser.GroupStatus,
                roomUser.GroupName,
                roomUser.FigureExtra,
                roomUser.AchievementScore,
                roomUser.IsStaff,
                roomUser.BadgeCode,
                roomUser.GroupBadge,
                roomUser.GroupPayload.ToArray(),
                roomUser.BadgeRank,
                status?.RightsLevel ?? 0,
                status?.RightsLevel > 0)
            : null;

        PetAvatarSnapshot? pet = avatar is Pet roomPet
            ? new PetAvatarSnapshot(
                roomPet.PetType,
                roomPet.OwnerId,
                roomPet.OwnerName,
                roomPet.RarityLevel,
                roomPet.HasSaddle,
                roomPet.IsRiding,
                roomPet.CanBreed,
                roomPet.CanHarvest,
                roomPet.CanRevive,
                roomPet.HasBreedingPermission,
                roomPet.Level,
                roomPet.Posture)
            : null;

        BotAvatarSnapshot? bot = avatar is Bot roomBot
            ? new BotAvatarSnapshot(
                roomBot.IsPublicBot,
                roomBot.IsPrivateBot,
                roomBot.Gender.ToString(),
                roomBot.OwnerId,
                roomBot.OwnerName,
                roomBot.Skills.ToArray())
            : null;

        return new AvatarSnapshot(
            avatar.Type.ToString(),
            avatar.IsRemoved,
            avatar.Id,
            avatar.Index,
            avatar.Name,
            avatar.Motto,
            avatar.Figure,
            Position(avatar.Location),
            Area(avatar.Location, 1, 1),
            avatar.Direction,
            avatar.HeadDirection,
            avatar.Dance,
            avatar.Effect,
            avatar.HandItem,
            avatar.IsIdle,
            avatar.IsTyping,
            status,
            user,
            pet,
            bot);
    }

    /// <summary>Projects a user's profile card.</summary>
    /// <param name="profile">The profile to project.</param>
    /// <returns>
    /// The profile snapshot. The trade lock, name colour and respect allowance fields are only
    /// sent by newer hotels and are otherwise at their defaults.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    public static ProfileSnapshot From(UserData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileSnapshot(
            profile.Id,
            profile.Name,
            profile.Figure,
            profile.Gender.ToString(),
            profile.Motto,
            profile.RealName,
            profile.DirectMail,
            profile.RespectTotal,
            profile.RespectLeft,
            profile.PetRespectLeft,
            profile.StreamPublishingAllowed,
            profile.LastAccessDate,
            profile.IsNameChangeable,
            profile.IsSafetyLocked,
            profile.IsTradeLocked,
            profile.NameColor,
            profile.RespectReplenishesLeft,
            profile.MaxRespectPerDay);
    }

    /// <summary>Projects the friend list with no categories and no capacity limits.</summary>
    /// <param name="friends">The friends to project.</param>
    /// <returns>The friend collection, online first then by name, and never truncated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="friends"/> is <see langword="null"/>.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// The sequence yields <see cref="DefaultSourceItemLimit"/> items or more.
    /// </exception>
    public static FriendCollectionSnapshot Friends(IEnumerable<Friend> friends) =>
        Friends(friends, null, 0, 0, 0, DefaultSourceItemLimit);

    /// <summary>Projects the friend list together with its categories and capacity limits.</summary>
    /// <remarks>
    /// This projection has no output cap: every friend and every category is returned. Only the
    /// safety valve applies, and it throws rather than truncating.
    /// </remarks>
    /// <param name="friends">The friends to project.</param>
    /// <param name="categories">The friend-list categories; <see langword="null"/> is treated as none.</param>
    /// <param name="userLimit">The friend slots this account has; 0 when the hotel has not reported it.</param>
    /// <param name="normalLimit">The friend slots a non-club account gets; 0 when not reported.</param>
    /// <param name="extendedLimit">The friend slots a club account gets; 0 when not reported.</param>
    /// <param name="sourceItemLimit">
    /// The safety valve applied to the friends and to the categories separately; reaching it throws.
    /// </param>
    /// <returns>
    /// The friend collection. Friends are ordered online first, then by name case-insensitively;
    /// categories by name, then by identifier. The online count is computed from the projection.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="friends"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceItemLimit"/> is negative.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Either sequence yields <paramref name="sourceItemLimit"/> items or more.
    /// </exception>
    public static FriendCollectionSnapshot Friends(
        IEnumerable<Friend> friends,
        IEnumerable<FriendCategory>? categories = null,
        int userLimit = 0,
        int normalLimit = 0,
        int extendedLimit = 0,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        FriendSnapshot[] projected = MaterializeBounded(
                friends,
                sourceItemLimit,
                nameof(friends))
            .OrderByDescending(friend => friend.IsOnline)
            .ThenBy(friend => friend.Name, StringComparer.OrdinalIgnoreCase)
            .Select(friend => new FriendSnapshot(
                friend.Id,
                friend.Name,
                friend.Figure,
                friend.Gender.ToString(),
                friend.Motto,
                friend.RealName,
                friend.IsOnline,
                friend.CanFollow,
                friend.CategoryId,
                friend.FacebookId,
                friend.IsAcceptingOfflineMessages,
                friend.IsVipMember,
                friend.IsPocketHabboUser,
                friend.Relation.ToString(),
                friend.LastOnline,
                friend.UnityStatus,
                friend.UnityPlatform))
            .ToArray();
        FriendCategorySnapshot[] projected_categories = MaterializeBounded(
                categories ?? [],
                sourceItemLimit,
                nameof(categories))
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => (long)category.Id)
            .Select(category => new FriendCategorySnapshot(category.Id, category.Name))
            .ToArray();
        return new FriendCollectionSnapshot(
            projected.Length,
            projected.Count(friend => friend.IsOnline),
            userLimit,
            normalLimit,
            extendedLimit,
            projected_categories,
            projected);
    }

    /// <summary>Projects the room's rights list.</summary>
    /// <remarks>
    /// The hotel only sends this list to the room owner, so on any other room it projects an
    /// empty collection. This projection has no output cap: every controller is returned.
    /// </remarks>
    /// <param name="controllers">The rights holders to project.</param>
    /// <param name="roomId">The room the list belongs to, or <see langword="null"/> when outside a room.</param>
    /// <param name="generation">The room session counter to stamp onto the snapshot.</param>
    /// <param name="isOwner">Whether the local user owns the room.</param>
    /// <param name="sourceItemLimit">The safety valve against an unbounded source; reaching it throws.</param>
    /// <returns>The controller collection, ordered by name case-insensitively, then by identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="controllers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceItemLimit"/> is negative.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// The sequence yields <paramref name="sourceItemLimit"/> items or more.
    /// </exception>
    public static ControllerCollectionSnapshot Controllers(
        IEnumerable<IdName> controllers,
        Id? roomId = null,
        long generation = 0,
        bool isOwner = false,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        ControllerSnapshot[] projected = MaterializeBounded(
                controllers,
                sourceItemLimit,
                nameof(controllers))
            .OrderBy(controller => controller.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(controller => (long)controller.Id)
            .Select(controller => new ControllerSnapshot(controller.Id, controller.Name))
            .ToArray();
        return new ControllerCollectionSnapshot(roomId, generation, isOwner, projected.Length, projected);
    }

    /// <summary>
    /// Projects an avatar status update, decoding its fragment string into the posture,
    /// rights, sign and walking target it encodes.
    /// </summary>
    /// <param name="status">The status to project.</param>
    /// <returns>
    /// The status snapshot, including the recompiled raw string and a case-insensitive copy of
    /// every fragment, so fragments QX does not model are still reachable.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is <see langword="null"/>.</exception>
    public static AvatarStatusSnapshot From(AvatarStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        IReadOnlyDictionary<string, IReadOnlyList<string>> fragments = status.Fragments.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return new AvatarStatusSnapshot(
            status.StatusId,
            Position(status.Location),
            status.Direction,
            status.HeadDirection,
            status.CompileStatus(),
            status.Stance.ToString(),
            status.IsController,
            status.RightsLevel,
            status.IsTrading,
            status.SittingOnFloor,
            status.Sign,
            status.MovingTo is { } movingTo ? Position(movingTo) : null,
            fragments)
        {
            JumpingPower = status.JumpingPower,
            TargetId = status.TargetId,
            ActionHeight = status.ActionHeight
        };
    }

    /// <summary>Projects the room's furni with the default per-type cap of 200 items and no room context.</summary>
    /// <param name="floorItems">The floor items to project.</param>
    /// <param name="wallItems">The wall items to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog. When <see langword="null"/> no item gets a definition and
    /// each item's area falls back to the size in its own packet.
    /// </param>
    /// <param name="maxItemsPerType">The output cap applied to each list separately.</param>
    /// <returns>The furni collection with its truncation flags and total counts.</returns>
    /// <exception cref="ArgumentNullException">Either item sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxItemsPerType"/> is negative.</exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Either sequence yields <see cref="DefaultSourceItemLimit"/> items or more.
    /// </exception>
    public static FurniCollectionSnapshot Furni(
        IEnumerable<FloorItem> floorItems,
        IEnumerable<WallItem> wallItems,
        FurniData? furniData = null,
        int maxItemsPerType = 200) =>
        Furni(
            floorItems,
            wallItems,
            furniData,
            maxItemsPerType,
            null,
            0,
            DefaultSourceItemLimit);

    /// <summary>
    /// Projects the room's furni, capping floor and wall items separately and reporting what
    /// was dropped.
    /// </summary>
    /// <remarks>
    /// The cap is not a blind take: both lists keep the items with the lowest identifiers, ties
    /// broken by source order, and return them in ascending identifier order. That makes a
    /// truncated result a stable prefix rather than an arbitrary subset. A cap of 0 returns no
    /// items at all while still reporting the true totals, and whenever fewer items are
    /// returned than exist the matching truncation flag is set.
    /// </remarks>
    /// <param name="floorItems">The floor items to project.</param>
    /// <param name="wallItems">The wall items to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog. When <see langword="null"/> no item gets a definition, the
    /// collection reports definitions as not loaded, and each item's area falls back to the
    /// size in its own packet.
    /// </param>
    /// <param name="maxItemsPerType">The output cap applied to each list separately.</param>
    /// <param name="roomId">The room the items belong to, or <see langword="null"/> when outside a room.</param>
    /// <param name="generation">The room session counter to stamp onto the snapshot.</param>
    /// <param name="sourceItemLimitPerType">
    /// The safety valve applied to each sequence separately; reaching it throws.
    /// </param>
    /// <returns>The furni collection with its truncation flags and total counts.</returns>
    /// <exception cref="ArgumentNullException">Either item sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxItemsPerType"/> or <paramref name="sourceItemLimitPerType"/> is negative.
    /// </exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Either sequence yields <paramref name="sourceItemLimitPerType"/> items or more.
    /// </exception>
    public static FurniCollectionSnapshot Furni(
        IEnumerable<FloorItem> floorItems,
        IEnumerable<WallItem> wallItems,
        FurniData? furniData,
        int maxItemsPerType,
        Id? roomId = null,
        long generation = 0,
        int sourceItemLimitPerType = DefaultSourceItemLimit)
    {
        ArgumentNullException.ThrowIfNull(floorItems);
        ArgumentNullException.ThrowIfNull(wallItems);
        ArgumentOutOfRangeException.ThrowIfNegative(maxItemsPerType);

        CappedSource<FloorItem> floor = SelectCapped(
            floorItems,
            maxItemsPerType,
            sourceItemLimitPerType,
            nameof(floorItems),
            Comparer<FloorItem>.Create(
                (left, right) => ((long)left.Id).CompareTo((long)right.Id)));
        CappedSource<WallItem> wall = SelectCapped(
            wallItems,
            maxItemsPerType,
            sourceItemLimitPerType,
            nameof(wallItems),
            Comparer<WallItem>.Create(
                (left, right) => ((long)left.Id).CompareTo((long)right.Id)));

        FloorItemSnapshot[] projectedFloor = floor.Items
            .Select(item => From(item, furniData))
            .ToArray();

        WallItemSnapshot[] projectedWall = wall.Items
            .Select(item => From(item, furniData))
            .ToArray();

        return new FurniCollectionSnapshot(
            roomId,
            generation,
            furniData is not null,
            floor.Total,
            wall.Total,
            projectedFloor.Length,
            projectedWall.Length,
            maxItemsPerType,
            projectedFloor.Length < floor.Total,
            projectedWall.Length < wall.Total,
            projectedFloor,
            projectedWall);
    }

    /// <summary>Projects a single floor item, resolving its definition and footprint.</summary>
    /// <param name="item">The floor item to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog, or <see langword="null"/> to project without one. Without
    /// it the definition is <see langword="null"/>, the identifier falls back to whatever the
    /// packet carried, and the area falls back to the item's own size.
    /// </param>
    /// <returns>The floor item snapshot, with its area already rotated for the item's direction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static FloorItemSnapshot From(FloorItem item, FurniData? furniData = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        FurniInfo? info = furniData?.GetInfo(item);
        int width = info?.Width ?? item.SizeX;
        int length = info?.Length ?? item.SizeZ;
        string? identifier = string.IsNullOrWhiteSpace(item.Identifier)
            ? info?.Identifier
            : item.Identifier;

        return new FloorItemSnapshot(
            item.Id,
            item.IsRemoved,
            item.Kind,
            identifier,
            Definition(info),
            item.OwnerId,
            item.OwnerName,
            Position(item.Location),
            Area(item.AreaFor(Math.Max(1, width), Math.Max(1, length))),
            item.Direction,
            item.Height,
            item.Extra,
            From(item.Data),
            item.State,
            item.SecondsToExpiration,
            item.Usage.ToString(),
            item.IsHidden);
    }

    /// <summary>Projects a single wall item, resolving its definition and wall location.</summary>
    /// <param name="item">The wall item to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog, or <see langword="null"/> to project without one. Without
    /// it both the definition and the identifier are <see langword="null"/>, since wall item
    /// packets carry no class name of their own.
    /// </param>
    /// <returns>
    /// The wall item snapshot, carrying the location both decomposed and in the client's own
    /// <c>:w=wx,wy l=lx,ly o</c> text form.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static WallItemSnapshot From(WallItem item, FurniData? furniData = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        FurniInfo? info = furniData?.GetInfo(item);
        string? identifier = string.IsNullOrWhiteSpace(item.Identifier)
            ? info?.Identifier
            : item.Identifier;

        return new WallItemSnapshot(
            item.Id,
            item.IsRemoved,
            item.Kind,
            identifier,
            Definition(info),
            item.OwnerId,
            item.OwnerName,
            new WallLocationSnapshot(
                item.WX,
                item.WY,
                item.LX,
                item.LY,
                item.Orientation.ToString(),
                item.Location.ToString()),
            item.Data,
            item.State,
            item.SecondsToExpiration,
            item.Usage.ToString(),
            item.IsHidden);
    }

    /// <summary>
    /// Projects the local user's hand, capping the item list and carrying the fragmented-load
    /// bookkeeping that says whether it can be trusted yet.
    /// </summary>
    /// <remarks>
    /// The cap keeps the items with the lowest inventory slot identifiers, ties broken by
    /// source order, and returns them in ascending identifier order, so a truncated result is a
    /// stable prefix rather than an arbitrary subset. A cap of 0 returns no items while still
    /// reporting the true total.
    /// </remarks>
    /// <param name="items">The inventory items to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog. When <see langword="null"/> no item gets a definition and
    /// the snapshot reports definitions as not loaded.
    /// </param>
    /// <param name="maxItems">The output cap.</param>
    /// <param name="isLoading">Whether an inventory load is in flight.</param>
    /// <param name="isStale">Whether the listed items are left over from an invalidated load.</param>
    /// <param name="generation">The inventory load counter to stamp onto the snapshot.</param>
    /// <param name="expectedFragments">
    /// How many fragments the current load consists of; -1 means that is not yet known.
    /// </param>
    /// <param name="receivedFragments">How many fragments have arrived.</param>
    /// <param name="sourceItemLimit">The safety valve against an unbounded source; reaching it throws.</param>
    /// <returns>The inventory snapshot with its truncation flag and total count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxItems"/> or <paramref name="sourceItemLimit"/> is negative.
    /// </exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// The sequence yields <paramref name="sourceItemLimit"/> items or more.
    /// </exception>
    public static InventorySnapshot Inventory(
        IEnumerable<InventoryItem> items,
        FurniData? furniData = null,
        int maxItems = 500,
        bool isLoading = false,
        bool isStale = false,
        long generation = 0,
        int expectedFragments = -1,
        int receivedFragments = 0,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        CappedSource<InventoryItem> inventory = SelectCapped(
            items,
            maxItems,
            sourceItemLimit,
            nameof(items),
            Comparer<InventoryItem>.Create(
                (left, right) => ((long)left.ItemId).CompareTo((long)right.ItemId)));
        InventoryItemSnapshot[] projected = inventory.Items
            .Select(item => From(item, furniData))
            .ToArray();

        return new InventorySnapshot(
            furniData is not null,
            isLoading,
            isStale,
            generation,
            expectedFragments,
            receivedFragments,
            inventory.Total,
            projected.Length,
            maxItems,
            projected.Length < inventory.Total,
            projected);
    }

    /// <summary>Projects a single inventory item, resolving its definition from the catalog.</summary>
    /// <param name="item">The inventory item to project.</param>
    /// <param name="furniData">
    /// The furni definition catalog, or <see langword="null"/> to project without a definition.
    /// </param>
    /// <returns>The inventory item snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static InventoryItemSnapshot From(InventoryItem item, FurniData? furniData = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new InventoryItemSnapshot(
            item.ItemId,
            item.Type.ToString(),
            item.Id,
            item.Kind,
            Definition(furniData?.GetInfo(item.Type, item.Kind)),
            item.Category,
            From(item.Data),
            item.IsRecyclable,
            item.IsTradeable,
            item.IsGroupable,
            item.IsSellable,
            item.SecondsToExpiration,
            item.HasRentPeriodStarted,
            item.RoomId,
            item.IsUnseen,
            item.Timestamp,
            item.IsNft,
            item.NftName,
            item.IsExternalImage,
            item.SlotId,
            item.Extra);
    }

    public static InventoryItemSnapshot WithDefinition(
        InventoryItemSnapshot item,
        FurniData? furni_data)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!Enum.TryParse(item.Type, false, out ItemType item_type) ||
            item_type is not (ItemType.Floor or ItemType.Wall))
        {
            throw new InvalidDataException($"Unsupported inventory item type '{item.Type}'.");
        }
        return item with
        {
            Definition = Definition(furni_data?.GetInfo(item_type, item.Kind))
        };
    }

    /// <summary>
    /// Projects the live heightmap tile by tile, capping the tile list while still counting
    /// every tile.
    /// </summary>
    /// <remarks>
    /// Unlike the item projections this cap is a plain prefix: the first
    /// <paramref name="maxTiles"/> tiles in the heightmap's own row-major order are kept and
    /// the tail is dropped, so a truncated snapshot covers the top of the room and not the
    /// bottom. The four aggregate counts are always computed over the whole heightmap.
    /// </remarks>
    /// <param name="heightmap">The heightmap to project.</param>
    /// <param name="maxTiles">The output cap on the tile list; 0 returns counts only.</param>
    /// <param name="roomId">The room the heightmap belongs to, or <see langword="null"/> when outside a room.</param>
    /// <param name="generation">The room session counter to stamp onto the snapshot.</param>
    /// <returns>The heightmap snapshot with its truncation flag and aggregate counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="heightmap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTiles"/> is negative.</exception>
    public static HeightmapSnapshot Heightmap(
        Heightmap heightmap,
        int maxTiles = 4096,
        Id? roomId = null,
        long generation = 0)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTiles);

        var projected = new List<HeightmapTileSnapshot>(Math.Min(maxTiles, heightmap.Count));
        int tile_count = 0;
        int floor_tiles = 0;
        int blocked_tiles = 0;
        int walkable_tiles = 0;
        foreach (HeightmapTile tile in heightmap.Tiles)
        {
            if (projected.Count < maxTiles)
            {
                projected.Add(new HeightmapTileSnapshot(
                    tile.X,
                    tile.Y,
                    tile.Value,
                    tile.IsFloor,
                    tile.IsBlocked,
                    tile.IsFree,
                    tile.Height));
            }

            tile_count++;
            if (tile.IsFloor)
                floor_tiles++;
            if (tile.IsFloor && tile.IsBlocked)
                blocked_tiles++;
            if (tile.IsFree)
                walkable_tiles++;
        }

        return new HeightmapSnapshot(
            roomId,
            generation,
            heightmap.Width,
            heightmap.Length,
            tile_count,
            projected.Count,
            maxTiles,
            projected.Count < tile_count,
            floor_tiles,
            walkable_tiles,
            blocked_tiles,
            tile_count - floor_tiles,
            projected.ToArray());
    }

    /// <summary>
    /// Projects a furni payload, flattening the concrete payload shape into one record whose
    /// shape-specific members are populated only where they apply.
    /// </summary>
    /// <param name="data">The payload to project.</param>
    /// <returns>
    /// The payload snapshot. The map, string list, integer list, vote, high-score and
    /// crackable members are <see langword="null"/> unless the payload is of the matching type,
    /// and are then omitted from JSON entirely.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static ItemDataSnapshot From(ItemData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        IReadOnlyDictionary<string, string>? mapEntries = data is MapData map
            ? new Dictionary<string, string>(map.Entries, StringComparer.Ordinal)
            : null;

        IReadOnlyList<string>? stringValues = data is StringArrayData strings
            ? strings.Values.ToArray()
            : null;

        IReadOnlyList<int>? intValues = data is IntArrayData integers
            ? integers.Values.ToArray()
            : null;

        IReadOnlyList<HighScoreSnapshot>? highScores = data is HighScoreData scores
            ? scores.Scores
                .Select(score => new HighScoreSnapshot(score.Score, score.Names.ToArray()))
                .ToArray()
            : null;

        return new ItemDataSnapshot(
            data.Type.ToString(),
            (int)data.Flags,
            data.Value,
            data.State,
            data.IsLimitedRare,
            data.UniqueSerialNumber,
            data.UniqueSeriesSize,
            data.UniqueLimitedData,
            mapEntries,
            stringValues,
            intValues,
            data is VoteResultData vote ? vote.Result : null,
            data is HighScoreData highScore ? highScore.ScoreType : null,
            data is HighScoreData clearScore ? clearScore.ClearType : null,
            highScores,
            data is CrackableFurniData crackable ? crackable.Hits : null,
            data is CrackableFurniData target ? target.Target : null);
    }

    /// <summary>
    /// Projects a pet's statistics without a pet type, for callers that cannot see the room
    /// entity.
    /// </summary>
    /// <param name="pet">The pet statistics to project.</param>
    /// <returns>The pet snapshot with its pet type left <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pet"/> is <see langword="null"/>.</exception>
    public static PetInfoSnapshot From(PetInfo pet) => From(pet, null);

    /// <summary>Projects a pet's statistics, optionally stamped with the pet type from the room.</summary>
    /// <remarks>
    /// The pet info message carries only the breed variant, never the pet type. Supplying
    /// <paramref name="petType"/> from the room entity is what makes the breed resolvable,
    /// since the client keys its breed names on both values together.
    /// </remarks>
    /// <param name="pet">The pet statistics to project.</param>
    /// <param name="petType">
    /// What kind of animal this is, or <see langword="null"/> when the pet is not in the room.
    /// </param>
    /// <returns>The pet snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pet"/> is <see langword="null"/>.</exception>
    public static PetInfoSnapshot From(PetInfo pet, int? petType)
    {
        ArgumentNullException.ThrowIfNull(pet);

        return new PetInfoSnapshot(
            pet.Id,
            pet.Name,
            pet.Level,
            pet.MaxLevel,
            pet.Experience,
            pet.MaxExperience,
            pet.Energy,
            pet.MaxEnergy,
            pet.Happiness,
            pet.MaxHappiness,
            pet.Scratches,
            pet.OwnerId,
            pet.Age,
            pet.OwnerName,
            pet.BreedId,
            pet.HasFreeSaddle,
            pet.IsRiding,
            pet.SkillThresholds.ToArray(),
            pet.AccessRights,
            pet.CanBreed,
            pet.CanHarvest,
            pet.CanRevive,
            pet.RarityLevel,
            pet.MaxWellbeingSeconds,
            pet.RemainingWellbeingSeconds,
            pet.RemainingGrowingSeconds,
            pet.HasBreedingPermission)
        {
            PetType = petType
        };
    }

    private static FurniDefinitionSnapshot? Definition(FurniInfo? info) =>
        info is null
            ? null
            : new FurniDefinitionSnapshot(
                info.Type.ToString(),
                info.Kind,
                info.Identifier,
                info.Name,
                info.Width,
                info.Length,
                info.Category,
                info.Line)
            {
                ClassName = info.ClassName,
                Revision = info.Revision,
                DefaultDirection = info.DefaultDirection,
                PartColors = info.PartColors.ToArray(),
                Description = info.Description,
                AdUrl = info.AdUrl,
                OfferId = info.OfferId,
                BuyOut = info.BuyOut,
                RentOfferId = info.RentOfferId,
                RentBuyOut = info.RentBuyOut,
                IsBuildersClub = info.IsBuildersClub,
                BuildersClubOfferId = info.BuildersClubOfferId,
                ExcludedDynamic = info.ExcludedDynamic,
                CustomParams = info.CustomParams,
                SpecialType = info.SpecialType,
                CanStandOn = info.CanStandOn,
                CanSitOn = info.CanSitOn,
                CanLayOn = info.CanLayOn,
                CanPutStuffOn = info.CanPutStuffOn,
                Height = info.Height,
                Environment = info.Environment,
                IsRare = info.IsRare,
                Tradeable = info.Tradeable,
                Recyclable = info.Recyclable,
                HasIndexedColor = info.HasIndexedColor,
                ColorIndex = info.ColorIndex,
                IsWalkable = info.IsWalkable,
                IsUnwalkable = info.IsUnwalkable
            };

    private static PositionSnapshot Position(Tile tile) => new(tile.X, tile.Y, tile.Z);

    private static AreaSnapshot Area(Tile origin, int width, int length) =>
        new(Position(origin), Math.Max(1, width), Math.Max(1, length));

    private static AreaSnapshot Area(Area area) =>
        new(Position(area.Origin), area.Width, area.Length);
}
