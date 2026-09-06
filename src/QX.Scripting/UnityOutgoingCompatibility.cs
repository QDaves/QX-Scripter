using System.Collections;
using System.Globalization;
using Qx;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Scripting;

internal readonly record struct UnityOutgoingMessage(string HeaderName, string SchemaName, object[] Values);
internal readonly record struct UnityForumReadMarker(Id GroupId, int LastReadMessageId, bool MarkAsRead);

internal static class UnityOutgoingCompatibility
{
    private static readonly Dictionary<string, string> CanonicalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ActivateAvatarEffect"] = "AvatarEffectActivated",
        ["ChangeAvatarMotto"] = "ChangeMotto",
        ["CreateNewFlat"] = "CreateFlat",
        ["DeleteFlat"] = "DeleteRoom",
        ["FlatOpc"] = "OpenFlatConnection",
        ["GetAvailableBadges"] = "GetBadges",
        ["GetMessages"] = "GetForumThreadMessages",
        ["GetNewPetInfo"] = "GetPetInfo",
        ["GetThread"] = "GetForumThread",
        ["GetThreads"] = "GetForumThreads",
        ["IgnoreAvatarId"] = "IgnoreUser",
        ["CallForHelpFromForumMessage"] = "ReportForumMessage",
        ["CallForHelpFromForumThread"] = "ReportForumThread",
        ["MarketplaceBuyOffer"] = "BuyMarketplaceOffer",
        ["MarketplaceBuyTokens"] = "BuyMarketplaceTokens",
        ["MarketplaceCancelAllOffers"] = "CancelAllMarketplaceOffers",
        ["MarketplaceCancelOffer"] = "CancelMarketplaceOffer",
        ["MarketplaceCanMakeOffer"] = "GetMarketplaceCanMakeOffer",
        ["MarketplaceGetConfiguration"] = "GetMarketplaceConfiguration",
        ["MarketplaceGetItemStats"] = "GetMarketplaceItemStats",
        ["MarketplaceListOwnOffers"] = "GetMarketplaceOwnOffers",
        ["MarketplaceMakeOffer"] = "MakeOffer",
        ["MarketplaceRedeemOfferCredits"] = "RedeemMarketplaceOfferCredits",
        ["MarketplaceSearchOffers"] = "GetMarketplaceOffers",
        ["ModerateMessage"] = "ModerateForumMessage",
        ["ModerateThread"] = "ModerateForumThread",
        ["MoveRoomItem"] = "MoveObject",
        ["Navigator2Search"] = "NewNavigatorSearch",
        ["PickItemUpFromRoom"] = "PickupObject",
        ["PostMessage"] = "PostForumMessage",
        ["RoomBanWithDuration"] = "BanUserWithDuration",
        ["RoomMuteUser"] = "MuteUser",
        ["SendMessage"] = "SendMsg",
        ["ShowSign"] = "Sign",
        ["ToggleRoomStaffPick"] = "ToggleStaffPick",
        ["TradeAccept"] = "AcceptTrading",
        ["TradeAddItems"] = "AddItemsToTrade",
        ["TradeClose"] = "CloseTrading",
        ["TradeConfirmAccept"] = "ConfirmAcceptTrading",
        ["TradeRemoveItem"] = "RemoveItemFromTrade",
        ["TradeOpen"] = "OpenTrading",
        ["TradeUnaccept"] = "UnacceptTrading",
        ["UpdateAvatar"] = "UpdateFigureData",
        ["UpdateForumReadMarker"] = "UpdateForumReadMarkers",
        ["UpdateNavigatorSettings"] = "UpdateHomeRoom",
        ["UpdateThread"] = "UpdateForumThread",
        ["UseAvatarEffect"] = "AvatarEffectSelected",
        ["UseFurniture"] = "UseStuff",
        ["UserCancelTyping"] = "CancelTyping",
        ["UserStartTyping"] = "StartTyping"
    };

    private static readonly Dictionary<string, int[]> IdPositions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AddSpamWallPostIt"] = [0],
        ["ApproveMembershipRequest"] = [0, 1],
        ["AssignRights"] = [0],
        ["BanUserWithDuration"] = [0, 1],
        ["BuyMarketplaceOffer"] = [0],
        ["CancelMarketplaceOffer"] = [0],
        ["CloseChest"] = [0],
        ["DeleteRoom"] = [0],
        ["DeselectFavouriteHabboGroup"] = [0],
        ["DiceOff"] = [0],
        ["FollowFriend"] = [0],
        ["GetExtendedProfile"] = [0],
        ["GetHabboGroupDetails"] = [0],
        ["GetItemData"] = [0],
        ["GetPetInfo"] = [0],
        ["GetRelationshipStatusInfo"] = [0],
        ["GetRoomSettings"] = [0],
        ["GetSelectedBadges"] = [0],
        ["IgnoreUser"] = [0],
        ["JoinHabboGroup"] = [0],
        ["KickMember"] = [0, 1],
        ["KickUser"] = [0],
        ["MountPet"] = [0],
        ["MoveObject"] = [0],
        ["MoveWallItem"] = [0],
        ["MuteUser"] = [0, 1],
        ["OpenChestAndGetContents"] = [0],
        ["OpenFlatConnection"] = [0],
        ["PickupObject"] = [1],
        ["PlacePostIt"] = [0],
        ["PlaceRoomItem"] = [0],
        ["PlaceWallItem"] = [0],
        ["RejectMembershipRequest"] = [0, 1],
        ["RemoveBotFromFlat"] = [0],
        ["RemoveItemFromTrade"] = [0],
        ["RemovePetFromFlat"] = [0],
        ["RespectPet"] = [0],
        ["SelectFavouriteHabboGroup"] = [0],
        ["SendMsg"] = [0],
        ["SetChestOptions"] = [0],
        ["SetChestPreferences"] = [0],
        ["StartAddingToChest"] = [0],
        ["ThrowDice"] = [0],
        ["ToggleStaffPick"] = [0],
        ["UnignoreUser"] = [0],
        ["UpdateHomeRoom"] = [0],
        ["UseStuff"] = [0],
        ["UseWallItem"] = [0],
        ["WithdrawAllFromChest"] = [0],
        ["WithdrawCoinsFromChest"] = [0],
        ["WithdrawItemsFromChest"] = [0]
    };

    private static readonly HashSet<string> IdLists = new(StringComparer.OrdinalIgnoreCase)
    {
        "AcceptFriend",
        "AddItemsToTrade",
        "RemoveFriend",
        "RemoveRights"
    };

    private static readonly HashSet<string> ManualComplexSchemas = new(StringComparer.OrdinalIgnoreCase)
    {
        "MoveWallItem",
        "PlaceWallItem",
        "WithdrawItemsFromChest"
    };

    public static UnityOutgoingMessage Translate(string name, object[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);

        string schema_name = SchemaName(name);
        return schema_name switch
        {
            "BanUserWithDuration" => TranslateBan(name, schema_name, values),
            "GetMarketplaceItemStats" when values.Length == 2 => Append(name, schema_name, values, ""),
            "GetMarketplaceOffers" => TranslateMarketplaceSearch(name, schema_name, values),
            "GetMarketplaceOwnOffers" => TranslateMarketplaceOwnOffers(name, schema_name, values),
            "GetRelationshipStatusInfo" when values.Length == 1 => Append(name, schema_name, values, ""),
            "MoveWallItem" => TranslateWallLocation(name, schema_name, values),
            "PlaceObject" => TranslatePlaceObject(values),
            "PurchaseFromCatalogAsGift" when values.Length == 9 => Append(name, schema_name, values, 1),
            "ReportForumMessage" => TranslateForumReport(name, schema_name, values, 5),
            "ReportForumThread" => TranslateForumReport(name, schema_name, values, 4),
            "SaveRoomSettings" => TranslateRoomSettings(name, schema_name, values),
            "SendMsg" => TranslateConsoleMessage(name, schema_name, values),
            "UpdateForumReadMarkers" => TranslateForumReadMarkers(name, schema_name, values),
            "UseWallItem" => TranslateUseWallItem(name, schema_name, values),
            "Whisper" => TranslateWhisper(name, schema_name, values),
            _ => new UnityOutgoingMessage(name, schema_name, values)
        };
    }

    private static UnityOutgoingMessage TranslateMarketplaceSearch(
        string header_name,
        string schema_name,
        object[] values)
    {
        if (values.Length == 4)
            return new UnityOutgoingMessage(header_name, schema_name, values);
        if (values.Length != 5 ||
            values[4] is not bool combine_unique_offers)
        {
            throw new ArgumentException(
                "Marketplace search requires four Unity fields or five Flash fields.",
                nameof(values));
        }
        if (!combine_unique_offers)
        {
            throw new NotSupportedException(
                "Unity marketplace search cannot disable unique-offer grouping.");
        }
        return new UnityOutgoingMessage(
            header_name,
            schema_name,
            values[..4]);
    }

    private static UnityOutgoingMessage TranslateMarketplaceOwnOffers(
        string header_name,
        string schema_name,
        object[] values)
    {
        if (values.Length == 0)
            return new UnityOutgoingMessage(header_name, schema_name, values);
        if (values.Length != 1 ||
            !TryCount(values[0], out int category) ||
            category != 1)
        {
            throw new NotSupportedException(
                "Unity marketplace own offers only support the open-offers category.");
        }
        return new UnityOutgoingMessage(
            header_name,
            schema_name,
            []);
    }

    public static string SchemaName(string name) => CanonicalNames.GetValueOrDefault(name, name);

    public static void Write(
        in PacketWriter writer,
        UnityOutgoingMessage message,
        IReadOnlyList<OutgoingMessageSchema>? schemas = null)
    {
        if (schemas is not { Count: > 0 })
            throw new NotSupportedException($"Unity message '{message.SchemaName}' requires a verified wire schema.");

        if (IdLists.Contains(message.SchemaName))
        {
            if (!schemas!.Any(IsIdListSchema))
                throw new NotSupportedException($"Unity message '{message.SchemaName}' no longer matches the verified ID-list wire layout.");
            WriteIdList(in writer, message.Values);
            return;
        }

        if (message.SchemaName.Equals("UpdateForumReadMarkers", StringComparison.OrdinalIgnoreCase))
        {
            if (!schemas!.Any(IsForumReadMarkerSchema))
                throw new NotSupportedException($"Unity message '{message.SchemaName}' no longer matches the verified forum read-marker wire layout.");
            WriteForumReadMarkers(in writer, message.Values);
            return;
        }

        if (message.SchemaName.Equals("SaveRoomSettings", StringComparison.OrdinalIgnoreCase))
        {
            WriteRoomSettings(in writer, SelectRoomSettingsLayout(message.Values, schemas));
            return;
        }

        object[] schema_values = message.SchemaName.Equals("SendMsg", StringComparison.OrdinalIgnoreCase)
            ? SelectConsoleLayout(message.Values, schemas)
            : message.Values;
        if (OutgoingSchemaWriter.TryWrite(in writer, schemas, schema_values))
            return;
        if (!ManualComplexSchemas.Contains(message.SchemaName))
            throw new NotSupportedException($"Unity message '{message.SchemaName}' has no supported verified wire schema.");

        object[] values = message.SchemaName.Equals("SendMsg", StringComparison.OrdinalIgnoreCase)
            ? SelectConsoleLayout(message.Values, schemas)
            : [.. message.Values];
        if (IdPositions.TryGetValue(message.SchemaName, out int[]? positions))
        {
            foreach (int position in positions)
                if (position < values.Length)
                    values[position] = ToId(values[position], message.SchemaName, position);
        }

        if (message.SchemaName.Equals("OpenFlatConnection", StringComparison.OrdinalIgnoreCase) && values.Length > 2)
            values[2] = ToLong(values[2], message.SchemaName, 2);

        writer.WriteValues(values);
    }

    private static UnityOutgoingMessage TranslateBan(string header_name, string schema_name, object[] values)
    {
        if (values.Length == 3 && values[1] is string duration)
            return new UnityOutgoingMessage(header_name, schema_name, [values[0], values[2], duration]);
        return new UnityOutgoingMessage(header_name, schema_name, values);
    }

    private static UnityOutgoingMessage TranslateWhisper(string header_name, string schema_name, object[] values)
    {
        if (values.Length != 2 || values[0] is not string combined)
            return new UnityOutgoingMessage(header_name, schema_name, values);

        int separator = combined.IndexOf(' ');
        if (separator <= 0 || separator == combined.Length - 1)
            throw new ArgumentException("Flash Whisper payload must contain a recipient followed by a message.", nameof(values));

        return new UnityOutgoingMessage(
            header_name,
            schema_name,
            [combined[..separator], combined[(separator + 1)..], values[1]]);
    }

    /// <remarks>
    /// Two or three, and neither is padded to the other. The catalogue of the build in hand decides
    /// how many the composer takes — 2415 declares three, older builds two — and the runtime codec
    /// writes to whichever it finds. Filling in a third here would send a field the build may not
    /// have, which is worse than the sender being told the count is wrong.
    /// </remarks>
    private static UnityOutgoingMessage TranslateConsoleMessage(string header_name, string schema_name, object[] values)
    {
        if (values.Length is 2 or 3)
            return new UnityOutgoingMessage(header_name, schema_name, values);
        throw new ArgumentException("SendMsg requires a recipient, message and optional confirmation identifier.", nameof(values));
    }

    private static UnityOutgoingMessage TranslateForumReport(
        string header_name,
        string schema_name,
        object[] values,
        int unity_count)
    {
        if (values.Length == unity_count)
            return new UnityOutgoingMessage(header_name, schema_name, values);
        if (values.Length != unity_count + 2 ||
            values[^2] is not string first_context ||
            values[^1] is not string second_context)
        {
            throw new ArgumentException(
                $"{schema_name} requires {unity_count} Unity fields or {unity_count + 2} Flash fields.",
                nameof(values));
        }
        if (first_context.Length != 0 || second_context.Length != 0)
            throw new NotSupportedException($"Unity {schema_name} cannot represent Flash report context values.");
        return new UnityOutgoingMessage(header_name, schema_name, values[..unity_count]);
    }

    private static UnityOutgoingMessage TranslateForumReadMarkers(
        string header_name,
        string schema_name,
        object[] values)
    {
        UnityForumReadMarker[] markers;
        if (values.Length == 1 && values[0] is UpdateForumReadMarker request)
        {
            markers = request.Markers
                .Select((marker, index) => new UnityForumReadMarker(
                    marker.GroupId,
                    ToInt(marker.LastReadMessageId, schema_name, index * 3 + 1),
                    marker.MarkAsRead))
                .ToArray();
        }
        else if (values.Length == 1 && values[0] is IEnumerable sequence && values[0] is not string)
        {
            object[] entries = sequence.Cast<object>().ToArray();
            markers = entries.All(entry => entry is ForumReadMarker)
                ? entries
                    .Cast<ForumReadMarker>()
                    .Select((marker, index) => new UnityForumReadMarker(
                        marker.GroupId,
                        ToInt(marker.LastReadMessageId, schema_name, index * 3 + 1),
                        marker.MarkAsRead))
                    .ToArray()
                : ReadForumReadMarkers(entries, false, schema_name);
        }
        else
        {
            markers = ReadForumReadMarkers(values, true, schema_name);
        }

        if (markers.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(values), markers.Length, "Unity forum read-marker count exceeds UInt16.");
        return new UnityOutgoingMessage(header_name, schema_name, [markers]);
    }

    private static UnityForumReadMarker[] ReadForumReadMarkers(
        object[] values,
        bool require_count,
        string schema_name)
    {
        int offset = 0;
        int count;
        if (require_count)
        {
            if (values.Length == 0 || !TryCount(values[0], out count))
                throw new ArgumentException($"{schema_name} requires a nonnegative marker count.", nameof(values));
            offset = 1;
        }
        else
        {
            if (values.Length % 3 != 0)
                throw new ArgumentException($"{schema_name} marker entries require three fields.", nameof(values));
            count = values.Length / 3;
        }

        if (values.Length - offset != checked(count * 3))
            throw new ArgumentException($"{schema_name} marker count does not match its entries.", nameof(values));

        var markers = new UnityForumReadMarker[count];
        for (int index = 0; index < count; index++)
        {
            int position = offset + index * 3;
            markers[index] = new UnityForumReadMarker(
                ToId(values[position], schema_name, position),
                ToInt(values[position + 1], schema_name, position + 1),
                ToBool(values[position + 2], schema_name, position + 2));
        }
        return markers;
    }

    private static UnityOutgoingMessage TranslateUseWallItem(string header_name, string schema_name, object[] values)
    {
        if (values.Length == 1)
            return new UnityOutgoingMessage(header_name, schema_name, values);
        if (values.Length == 2 && IsZero(values[1]))
            return new UnityOutgoingMessage(header_name, schema_name, [values[0]]);
        throw new ArgumentException("Unity UseWallItem cannot represent a nonzero Flash state value.", nameof(values));
    }

    private static UnityOutgoingMessage TranslateWallLocation(string header_name, string schema_name, object[] values)
    {
        if (values.Length == 2 && values[1] is string location)
            return new UnityOutgoingMessage(header_name, schema_name, [values[0], WallLocation.ParseString(location)]);
        return new UnityOutgoingMessage(header_name, schema_name, values);
    }

    private static UnityOutgoingMessage TranslatePlaceObject(object[] values)
    {
        if (values.Length == 4)
            return new UnityOutgoingMessage("PlaceRoomItem", "PlaceRoomItem", values);
        if (values.Length != 1 || values[0] is not string packed)
            throw new ArgumentException("Flash PlaceObject payload must be a packed floor or wall placement string.", nameof(values));

        int separator = packed.IndexOf(' ');
        if (separator <= 0 || separator == packed.Length - 1 ||
            !long.TryParse(packed[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out long item_id))
        {
            throw new FormatException($"Invalid Flash PlaceObject payload: '{packed}'.");
        }

        string location = packed[(separator + 1)..];
        if (location[0] == ':')
            return new UnityOutgoingMessage("PlaceWallItem", "PlaceWallItem", [(Id)item_id, WallLocation.ParseString(location)]);

        string[] floor = location.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (floor.Length != 3 ||
            !int.TryParse(floor[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(floor[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) ||
            !int.TryParse(floor[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int direction))
        {
            throw new FormatException($"Invalid Flash PlaceObject payload: '{packed}'.");
        }

        return new UnityOutgoingMessage("PlaceRoomItem", "PlaceRoomItem", [(Id)item_id, x, y, direction]);
    }

    private static UnityOutgoingMessage TranslateRoomSettings(string header_name, string schema_name, object[] values)
    {
        if (values.Length >= 7 && values[6] is bool)
        {
            if (values.Length is not 12 and not 15)
                throw new ArgumentException("Unity SaveRoomSettings requires exactly 12 or 15 values.", nameof(values));
            return new UnityOutgoingMessage(header_name, schema_name, values);
        }
        if (values.Length < 25)
            throw new ArgumentException("Invalid Flash SaveRoomSettings payload.", nameof(values));

        int tag_count = ToInt(values[7], schema_name, 7);
        int settings = 8 + tag_count;
        if (tag_count < 0 || values.Length != settings + 17)
            throw new ArgumentException("Invalid Flash SaveRoomSettings payload.", nameof(values));

        object[] translated =
        [
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[6],
            values[settings + 1],
            values[settings + 7],
            values[settings + 8],
            values[settings + 9],
            values[5],
            values[settings],
            ToBool(values[settings + 2], schema_name, settings + 2) ? 1 : 0,
            ToBool(values[settings + 3], schema_name, settings + 3) ? 1 : 0,
            (Length)0
        ];
        return new UnityOutgoingMessage(header_name, schema_name, translated);
    }

    private static UnityOutgoingMessage Append(string header_name, string schema_name, object[] values, object value) =>
        new(header_name, schema_name, [.. values, value]);

    private static void WriteIdList(in PacketWriter writer, object[] values)
    {
        object[] ids;
        if (values.Length == 1 && values[0] is IEnumerable sequence && values[0] is not string)
        {
            ids = sequence.Cast<object>().ToArray();
        }
        else if (values.Length > 0 && TryCount(values[0], out int count) && count == values.Length - 1)
        {
            ids = values[1..];
        }
        else
        {
            ids = values;
        }

        writer.WriteLength((Length)ids.Length);
        for (int index = 0; index < ids.Length; index++)
            writer.WriteId(ToId(ids[index], "id list", index));
    }

    private static bool IsIdListSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Collection is not OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is OutgoingWireType.Int64 or OutgoingWireType.UInt64;

    private static bool IsForumReadMarkerSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Collection is not OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is OutgoingWireType.Unknown &&
        schema.Parameters[0].ElementWireTypes is { } element_types &&
        element_types.SequenceEqual(
            [
                OutgoingWireType.Int64,
                OutgoingWireType.Int32,
                OutgoingWireType.Boolean
            ]);

    private static void WriteForumReadMarkers(in PacketWriter writer, object[] values)
    {
        if (values.Length != 1 || values[0] is not UnityForumReadMarker[] markers)
            throw new ArgumentException("Unity forum read markers require one normalized marker collection.", nameof(values));

        writer.WriteLength((Length)markers.Length);
        foreach (UnityForumReadMarker marker in markers)
        {
            writer.WriteId(marker.GroupId);
            writer.WriteInt(marker.LastReadMessageId);
            writer.WriteBool(marker.MarkAsRead);
        }
    }

    private static void WriteRoomSettings(in PacketWriter writer, object[] values)
    {
        if (values.Length is not 12 and not 15)
            throw new ArgumentException("Unity SaveRoomSettings payload requires exactly 12 or 15 values.", nameof(values));

        writer.WriteId(ToId(values[0], "SaveRoomSettings", 0));
        writer.WriteString(ToText(values[1], "SaveRoomSettings", 1));
        writer.WriteString(ToText(values[2], "SaveRoomSettings", 2));
        writer.WriteInt(ToInt(values[3], "SaveRoomSettings", 3));
        writer.WriteString(ToText(values[4], "SaveRoomSettings", 4));
        writer.WriteInt(ToInt(values[5], "SaveRoomSettings", 5));
        writer.WriteBool(ToBool(values[6], "SaveRoomSettings", 6));
        writer.WriteInt(ToInt(values[7], "SaveRoomSettings", 7));
        writer.WriteInt(ToInt(values[8], "SaveRoomSettings", 8));
        writer.WriteInt(ToInt(values[9], "SaveRoomSettings", 9));
        writer.WriteInt(ToInt(values[10], "SaveRoomSettings", 10));
        if (values.Length == 15)
        {
            writer.WriteInt(ToInt(values[11], "SaveRoomSettings", 11));
            writer.WriteInt(ToInt(values[12], "SaveRoomSettings", 12));
            writer.WriteInt(ToInt(values[13], "SaveRoomSettings", 13));
            WriteIdList(in writer, values[14..]);
            return;
        }
        WriteIdList(in writer, values[11..]);
    }

    private static object[] SelectConsoleLayout(
        object[] values,
        IReadOnlyList<OutgoingMessageSchema>? schemas)
    {
        bool has_two_fields = schemas is not { Count: > 0 } || schemas.Any(schema => schema.Parameters.Count == 2);
        bool has_three_fields = schemas?.Any(schema => schema.Parameters.Count == 3) == true;
        if (values.Length == 2 && has_two_fields || values.Length == 3 && has_three_fields)
            return values;
        if (values.Length == 3 && has_two_fields)
        {
            if (!IsZero(values[2]))
                throw new NotSupportedException("This Unity SendMsg layout cannot represent a nonzero confirmation identifier.");
            return values[..2];
        }
        if (values.Length == 2 && has_three_fields)
            return [.. values, 0];
        return values;
    }

    private static object[] SelectRoomSettingsLayout(
        object[] values,
        IReadOnlyList<OutgoingMessageSchema>? schemas)
    {
        bool has_legacy = schemas?.Any(schema => schema.Parameters.Count == 12) == true;
        bool has_modern = schemas?.Any(schema => schema.Parameters.Count == 15) == true;
        if (!has_legacy && !has_modern || values.Length == 12 && has_legacy || values.Length == 15 && has_modern)
            return values;
        if (values.Length == 15 && has_legacy)
        {
            if (!IsZero(values[11]) || !IsZero(values[12]) || !IsZero(values[13]))
                throw new NotSupportedException("The legacy Unity SaveRoomSettings layout cannot represent trade or consumption settings.");
            return [.. values[..11], values[14]];
        }
        if (values.Length == 12 && has_modern)
            return [.. values[..11], 0, 0, 0, values[11]];
        throw new NotSupportedException("Unity SaveRoomSettings does not match a verified 12-field or 15-field layout.");
    }

    private static Id ToId(object value, string name, int position) => value switch
    {
        Id id => id,
        byte number => (long)number,
        short number => (long)number,
        int number => (long)number,
        long number => number,
        uint number => (long)number,
        ulong number when number <= long.MaxValue => (long)number,
        string text when Id.TryParse(text, out Id id) => id,
        _ => throw InvalidValue(value, name, position, "an ID")
    };

    private static long ToLong(object value, string name, int position) => value switch
    {
        byte number => number,
        short number => number,
        int number => number,
        long number => number,
        uint number => number,
        ulong number when number <= long.MaxValue => (long)number,
        Id id => id,
        _ => throw InvalidValue(value, name, position, "an Int64")
    };

    private static int ToInt(object value, string name, int position) => value switch
    {
        byte number => number,
        short number => number,
        int number => number,
        uint number when number <= int.MaxValue => (int)number,
        long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
        Id id when (long)id is >= int.MinValue and <= int.MaxValue => (int)(long)id,
        _ => throw InvalidValue(value, name, position, "an Int32")
    };

    private static bool ToBool(object value, string name, int position) => value switch
    {
        bool state => state,
        byte number when number <= 1 => number != 0,
        short number when number is 0 or 1 => number != 0,
        int number when number is 0 or 1 => number != 0,
        _ => throw InvalidValue(value, name, position, "a Boolean")
    };

    private static string ToText(object value, string name, int position) => value is string text
        ? text
        : throw InvalidValue(value, name, position, "a String");

    private static bool TryCount(object value, out int count)
    {
        switch (value)
        {
            case Length length:
                count = length;
                return true;
            case byte number:
                count = number;
                return true;
            case short number when number >= 0:
                count = number;
                return true;
            case int number when number >= 0:
                count = number;
                return true;
            default:
                count = 0;
                return false;
        }
    }

    private static bool IsZero(object value) => value switch
    {
        bool state => !state,
        byte number => number == 0,
        short number => number == 0,
        int number => number == 0,
        long number => number == 0,
        _ => false
    };

    private static ArgumentException InvalidValue(object? value, string name, int position, string expected) =>
        new($"Unity message '{name}' value {position} must be {expected}, got {value?.GetType().Name ?? "null"}.");
}
