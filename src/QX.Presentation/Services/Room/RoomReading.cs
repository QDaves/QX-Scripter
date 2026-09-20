using System.Globalization;
using Qx.Game;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Presentation.Services.Room;

public static class RoomReading
{
    public static RoomSnapshot Read(GameState game, bool connected)
    {
        ArgumentNullException.ThrowIfNull(game);
        RoomReadout readout = game.Room.Capture(Readout);
        if (!readout.IsInRoom)
        {
            RoomPresence away = connected
                ? readout.State is RoomSessionState.Entering ? RoomPresence.Entering : RoomPresence.Outside
                : RoomPresence.Disconnected;
            return RoomSnapshot.Empty with { Presence = away };
        }

        RoomData? data = readout.Data;
        FurniData? furni_data = game.GameData.Furni ?? game.Room.GameData?.Furni;
        RoomFurniPiece[] furni = Pieces(readout, furni_data);
        return new RoomSnapshot(
            RoomPresence.Inside,
            new RoomIdentity(
                readout.RoomId,
                readout.Generation,
                data?.Name ?? "",
                data?.OwnerName ?? "",
                data?.Description ?? "",
                data?.OfficialRoomPicRef ?? "",
                data?.UserCount ?? 0,
                data?.MaxUserCount ?? 0,
                readout.IsOwner,
                readout.HasRights),
            Facts(readout),
            Rules(readout),
            People(readout),
            Visits(game.Visitors.Visitors),
            furni,
            furni.Count(piece => piece.IsHidden));
    }

    static RoomReadout Readout(RoomManager room) => new(
        room.IsInRoom,
        room.State,
        room.RoomId,
        room.Generation,
        room.IsOwner,
        room.RightsLevel,
        room.RightsAreKnown,
        room.HasRights,
        room.Data,
        room.Details,
        room.ChatSettings,
        room.EntryTile,
        room.VisualizationSettings,
        room.Controllers.Count,
        [.. room.Avatars],
        [.. room.FloorItems],
        [.. room.WallItems]);

    static RoomPerson[] People(RoomReadout readout) =>
    [
        .. readout.Avatars
            .OrderBy(Rank)
            .ThenBy(avatar => avatar.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(avatar => Person(avatar, readout.Generation))
    ];

    static int Rank(Avatar avatar) => avatar switch
    {
        User => 0,
        Bot => 1,
        _ => 2
    };

    static RoomPerson Person(Avatar avatar, long generation) => new(
        avatar.Id,
        avatar.Index,
        avatar.Name,
        Detail(avatar),
        $"{avatar.X}, {avatar.Y}",
        avatar switch
        {
            Bot => RoomPersonKind.Bot,
            Pet => RoomPersonKind.Pet,
            _ => RoomPersonKind.User
        },
        avatar is User { IsStaff: true },
        avatar.IsIdle,
        avatar.CurrentUpdate?.IsTrading == true,
        avatar is Pet ? "" : avatar.Figure,
        avatar.Motto,
        avatar.XY,
        generation,
        avatar);

    static string Detail(Avatar avatar) => avatar switch
    {
        Pet pet => pet.OwnerName.Length > 0 ? $"owned by {pet.OwnerName}" : "",
        Bot bot => bot.OwnerName.Length > 0 ? $"owned by {bot.OwnerName}" : "",
        _ => avatar.Motto
    };

    static RoomVisit[] Visits(IReadOnlyList<RoomVisitor> visitors) =>
    [
        .. visitors.Select(visitor => new RoomVisit(
            visitor.UserId,
            visitor.Index,
            visitor.Name,
            RoomText.VisitWindow(visitor.Entered, visitor.Left),
            visitor.Visits,
            visitor.IsHere))
    ];

    static RoomFurniPiece[] Pieces(RoomReadout readout, FurniData? data) =>
    [
        .. readout.FloorItems.Cast<Furni>()
            .Concat(readout.WallItems)
            .Select(item => Piece(item, data?.GetInfo(item)))
            .OrderBy(piece => piece.Name, StringComparer.CurrentCultureIgnoreCase)
    ];

    static RoomFurniPiece Piece(Furni item, FurniInfo? info) => new(
        item.Id,
        item.Type,
        item.Kind,
        info?.Name is { Length: > 0 } named ? named : $"Furni {item.Kind}",
        info?.Identifier ?? "",
        item.OwnerName.Length > 0 ? $"owned by {item.OwnerName}" : "",
        item is FloorItem floor ? $"{floor.X}, {floor.Y}" : "wall",
        item.IsHidden,
        item is FloorItem,
        item is FloorItem placed ? placed.Location.XY : default,
        info?.Revision ?? 0,
        item);

    static RoomRules Rules(RoomReadout readout)
    {
        RoomModerationSettings? moderation = readout.Details?.Moderation;
        RoomChatSettings? chat = readout.ChatSettings;
        return new RoomRules(
            Named(readout.Data?.DoorMode),
            Named(moderation?.Mute),
            Named(moderation?.Kick),
            Named(moderation?.Ban),
            Named(chat?.Flow),
            Named(chat?.BubbleWidth),
            Named(chat?.ScrollSpeed),
            chat is null ? RoomRules.Missing : chat.TalkHearingDistance.ToString(CultureInfo.CurrentCulture),
            Named(chat?.FloodProtection));
    }

    static string Named<TValue>(TValue? value) where TValue : struct, Enum =>
        value is { } present ? RoomText.Words(present.ToString()) : RoomRules.Missing;

    static RoomFact[] Facts(RoomReadout readout)
    {
        RoomData? data = readout.Data;
        var facts = new List<RoomFact>(28)
        {
            new("ROOM ID", readout.RoomId.ToString()),
            new("YOUR RIGHTS", readout.RightsAreKnown
                ? RoomText.Rights(readout.RightsLevel, readout.IsOwner)
                : "not known yet")
        };

        if (data is not null)
        {
            facts.Add(new RoomFact("OWNER ID", data.OwnerId.ToString()));
            facts.Add(new RoomFact("TRADING", RoomText.Words(data.TradeMode.ToString())));
            facts.Add(new RoomFact("VISITORS", $"{data.UserCount} of {data.MaxUserCount}"));
            facts.Add(new RoomFact("CATEGORY", data.Category.ToString(CultureInfo.CurrentCulture)));
            facts.Add(new RoomFact("SCORE", data.Score.ToString(CultureInfo.CurrentCulture)));
            facts.Add(new RoomFact("RANKING", data.Ranking.ToString(CultureInfo.CurrentCulture)));
            facts.Add(new RoomFact("PETS", data.AllowPets ? "allowed" : "not allowed"));
            facts.Add(new RoomFact("OWNER SHOWN", data.ShowOwner ? "yes" : "no"));
            facts.Add(new RoomFact("ENTRY AD", data.DisplayRoomEntryAd ? "shown" : "hidden"));
            facts.Add(new RoomFact("OFFICIAL IMAGE", data.OfficialRoomPicRef is { Length: > 0 } ? "yes" : "no"));
            if (data.Tags.Count > 0)
                facts.Add(new RoomFact("TAGS", string.Join(", ", data.Tags)));
            if (data.HasGroup && data.GroupName.Length > 0)
            {
                facts.Add(new RoomFact("GROUP", data.GroupName));
                facts.Add(new RoomFact("GROUP ID", data.GroupId.ToString()));
            }
            if (data.HasEvent && data.EventName.Length > 0)
            {
                facts.Add(new RoomFact("EVENT", data.EventName));
                facts.Add(new RoomFact("EVENT LEFT", $"{data.EventMinutesRemaining} min"));
            }
        }

        facts.Add(new RoomFact("FLOOR ITEMS", readout.FloorItems.Length.ToString(CultureInfo.CurrentCulture)));
        facts.Add(new RoomFact("WALL ITEMS", readout.WallItems.Length.ToString(CultureInfo.CurrentCulture)));
        facts.Add(new RoomFact("CONTROLLERS", readout.Controllers.ToString(CultureInfo.CurrentCulture)));
        if (readout.EntryTile is { } entry)
            facts.Add(new RoomFact("ENTRY TILE", $"{entry.X}, {entry.Y} · {RoomText.Compass(entry.Direction)}"));
        if (readout.Visualization is { } visual)
        {
            facts.Add(new RoomFact("WALLS", visual.WallsHidden ? "hidden" : "shown"));
            facts.Add(new RoomFact("WALL THICKNESS", RoomText.Words(visual.WallThickness.ToString())));
            facts.Add(new RoomFact("FLOOR THICKNESS", RoomText.Words(visual.FloorThickness.ToString())));
        }
        if (readout.Details is { } details)
        {
            facts.Add(new RoomFact("STAFF PICK", details.IsStaffPick ? "yes" : "no"));
            facts.Add(new RoomFact("GROUP MEMBER", details.IsGroupMember ? "yes" : "no"));
            facts.Add(new RoomFact("ROOM MUTED", details.IsRoomMuted ? "yes" : "no"));
            facts.Add(new RoomFact("CAN MUTE", details.CanMute ? "yes" : "no"));
        }
        return [.. facts];
    }

    sealed record RoomReadout(
        bool IsInRoom,
        RoomSessionState State,
        Id RoomId,
        long Generation,
        bool IsOwner,
        int? RightsLevel,
        bool RightsAreKnown,
        bool HasRights,
        RoomData? Data,
        RoomResultDetails? Details,
        RoomChatSettings? ChatSettings,
        RoomEntryTile? EntryTile,
        RoomVisualizationSettings? Visualization,
        int Controllers,
        Avatar[] Avatars,
        FloorItem[] FloorItems,
        WallItem[] WallItems);
}
