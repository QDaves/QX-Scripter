using Qx;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

internal enum OutgoingField
{
    Id,
    Int,
    String,
    Bool,
    Long,
    WallLocation
}

internal abstract class UnityOutgoingCodec
{
    public virtual bool RequiresVerifiedUnitySchema => false;
    public virtual bool MatchesSchema(OutgoingMessageSchema schema) => true;
    public virtual bool MatchesFlashSchema(OutgoingMessageSchema schema) => true;

    public void Invoke(string name, Intercept intercept, Action<Intercept> handler)
    {
        Packet unity_packet = intercept.Packet;
        object native_value;
        Packet? flash_packet = null;

        try
        {
            native_value = ReadUnityPacket(unity_packet);
            flash_packet = UnityCompatibilityPacket.CreateFlashProjection(
                unity_packet.Header);
            WriteFlashPacket(flash_packet, ToFlash(native_value));
        }
        catch (Exception exception)
        {
            flash_packet?.Dispose();
            unity_packet.Position = 0;
            throw new InvalidOperationException($"Unity outgoing message '{name}' does not match its verified wire schema.", exception);
        }

        try
        {
            using (flash_packet)
            {
                byte[] original_bytes = flash_packet.Buffer.Span.ToArray();
                flash_packet.Position = 0;
                var view = new Intercept
                {
                    Packet = flash_packet,
                    Sequence = intercept.Sequence
                };
                Packet? normalized = null;
                try
                {
                    handler(view);

                    if (view.IsBlocked)
                        intercept.Block();

                    Packet edited = view.Packet;
                    if (edited.Client is ClientType.None)
                    {
                        normalized = UnityCompatibilityPacket.CopyAs(edited, ClientType.Flash);
                        edited = normalized;
                    }

                    bool packet_replaced = !ReferenceEquals(view.Packet, flash_packet);
                    bool header_changed = edited.Header != unity_packet.Header;
                    bool body_changed = !edited.Buffer.Span.SequenceEqual(original_bytes);
                    if (packet_replaced || header_changed || body_changed)
                    {
                        if (edited.Client is not ClientType.Flash)
                            throw new InvalidOperationException($"Unity outgoing message '{name}' must be edited through its Flash wire view.");
                        if (header_changed)
                            throw new InvalidOperationException($"Unity outgoing message '{name}' cannot change headers through a Flash wire view.");
                        if (!IsLosslessRoundtrip(unity_packet, native_value, flash_packet.Header, original_bytes))
                            throw new InvalidOperationException($"Unity outgoing message '{name}' cannot be edited through a Flash wire view without losing native fields.");

                        object changed_flash = ReadFlashPacket(edited);
                        object changed_native = ToUnity(native_value, changed_flash);
                        using var translated = new Packet(unity_packet.Header, ClientType.Unity);
                        WriteUnityPacket(translated, changed_native);
                        unity_packet.Clear();
                        unity_packet.WriteSpan(translated.Buffer.Span);
                    }
                }
                finally
                {
                    normalized?.Dispose();
                    if (!ReferenceEquals(view.Packet, flash_packet) && !ReferenceEquals(view.Packet, unity_packet))
                    {
                        view.Packet.Position = 0;
                        view.Packet.Dispose();
                    }
                }
            }
        }
        finally
        {
            unity_packet.Position = 0;
        }
    }

    public bool MatchesUnity(IPacket packet)
    {
        try
        {
            ReadUnityPacket(packet);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            packet.Position = 0;
        }
    }

    public bool MatchesFlash(IPacket packet)
    {
        try
        {
            ReadFlashPacket(packet);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            packet.Position = 0;
        }
    }

    public object[] ReadFlashValues(IPacket packet)
    {
        try
        {
            return ExportFlashValues(ReadFlashPacket(packet));
        }
        finally
        {
            packet.Position = 0;
        }
    }

    protected abstract object ReadUnity(in PacketReader reader);
    protected abstract object ReadFlash(in PacketReader reader);
    protected abstract void WriteUnity(in PacketWriter writer, object value);
    protected abstract void WriteFlash(in PacketWriter writer, object value);
    protected abstract object ToFlash(object native_value);
    protected abstract object ToUnity(object native_value, object flash_value);
    protected virtual object[] ExportFlashValues(object flash_value) => (object[])flash_value;

    private object ReadUnityPacket(IPacket packet)
    {
        packet.Position = 0;
        PacketReader reader = packet.Reader();
        object value = ReadUnity(in reader);
        if (reader.Available != 0)
            throw new InvalidDataException($"Unity payload has {reader.Available} trailing bytes.");
        return value;
    }

    private object ReadFlashPacket(IPacket packet)
    {
        packet.Position = 0;
        PacketReader reader = packet.Reader();
        object value = ReadFlash(in reader);
        if (reader.Available != 0)
            throw new InvalidDataException($"Flash payload has {reader.Available} trailing bytes.");
        return value;
    }

    private void WriteUnityPacket(IPacket packet, object value)
    {
        packet.Position = 0;
        PacketWriter writer = packet.Writer();
        WriteUnity(in writer, value);
        packet.Position = 0;
    }

    private void WriteFlashPacket(IPacket packet, object value)
    {
        packet.Position = 0;
        PacketWriter writer = packet.Writer();
        WriteFlash(in writer, value);
        packet.Position = 0;
    }

    private bool IsLosslessRoundtrip(
        Packet unity_packet,
        object native_value,
        Header flash_header,
        ReadOnlySpan<byte> flash_bytes)
    {
        using var flash_packet = UnityCompatibilityPacket.CreateFlashProjection(
            flash_header);
        flash_packet.WriteSpan(flash_bytes);
        flash_packet.Position = 0;
        object parsed_flash = ReadFlashPacket(flash_packet);
        object restored = ToUnity(native_value, parsed_flash);
        using var roundtrip = new Packet(unity_packet.Header, ClientType.Unity);
        WriteUnityPacket(roundtrip, restored);
        return roundtrip.Buffer.Span.SequenceEqual(unity_packet.Buffer.Span);
    }
}

internal sealed class UnityOutgoingSchemaCodec : UnityOutgoingCodec
{
    private readonly OutgoingField[] _unity_fields;
    private readonly OutgoingField[] _flash_fields;
    private readonly Func<object[], object[]> _to_flash;
    private readonly Func<object[], object[], object[]> _to_unity;

    public UnityOutgoingSchemaCodec(params OutgoingField[] fields)
        : this(fields, fields, null, null)
    {
    }

    public UnityOutgoingSchemaCodec(
        OutgoingField[] unity_fields,
        OutgoingField[] flash_fields,
        Func<object[], object[]>? to_flash,
        Func<object[], object[], object[]>? to_unity)
    {
        _unity_fields = unity_fields;
        _flash_fields = flash_fields;
        _to_flash = to_flash ?? (values => values);
        _to_unity = to_unity ?? MergeProjectedIds;
    }

    protected override object ReadUnity(in PacketReader reader) => ReadFields(in reader, _unity_fields);
    protected override object ReadFlash(in PacketReader reader) => ReadFields(in reader, _flash_fields);
    protected override void WriteUnity(in PacketWriter writer, object value) => WriteFields(in writer, _unity_fields, (object[])value);
    protected override void WriteFlash(in PacketWriter writer, object value) => WriteFields(in writer, _flash_fields, (object[])value);
    protected override object ToFlash(object native_value) => _to_flash((object[])native_value);
    protected override object ToUnity(object native_value, object flash_value) =>
        _to_unity((object[])native_value, (object[])flash_value);

    public override bool MatchesSchema(OutgoingMessageSchema schema)
        => MatchesFields(schema, _unity_fields, false);

    public override bool MatchesFlashSchema(OutgoingMessageSchema schema)
        => MatchesFields(schema, _flash_fields, true);

    private static bool MatchesFields(
        OutgoingMessageSchema schema,
        OutgoingField[] fields,
        bool flash)
    {
        if (schema.Parameters.Count != fields.Length)
            return false;

        for (int index = 0; index < fields.Length; index++)
        {
            OutgoingParameterSchema parameter = schema.Parameters[index];
            bool matches = flash
                ? MatchesFlashField(fields[index], parameter.WireType)
                : MatchesField(fields[index], parameter.WireType);
            if (parameter.Collection is not OutgoingCollectionKind.None || !matches)
                return false;
        }
        return true;
    }

    private object[] MergeProjectedIds(object[] original, object[] changed)
    {
        if (_unity_fields.Length != _flash_fields.Length || changed.Length != _unity_fields.Length)
            throw new InvalidOperationException("Outgoing wire fields require an explicit Unity projection.");

        object[] merged = [.. changed];
        for (int index = 0; index < merged.Length; index++)
        {
            if (_unity_fields[index] is OutgoingField.Id && _flash_fields[index] is OutgoingField.Id)
                merged[index] = UnityOutgoingInterception.PreserveId((Id)original[index], (Id)changed[index]);
        }
        return merged;
    }

    private static object[] ReadFields(in PacketReader reader, OutgoingField[] fields)
    {
        var values = new object[fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            values[index] = fields[index] switch
            {
                OutgoingField.Id => reader.ReadId(),
                OutgoingField.Int => reader.ReadInt(),
                OutgoingField.String => reader.ReadString(),
                OutgoingField.Bool => reader.ReadBool(),
                OutgoingField.Long => reader.ReadLong(),
                OutgoingField.WallLocation => ReadWallLocation(in reader),
                _ => throw new ArgumentOutOfRangeException(nameof(fields))
            };
        }
        return values;
    }

    private static void WriteFields(in PacketWriter writer, OutgoingField[] fields, object[] values)
    {
        if (values.Length != fields.Length)
            throw new InvalidDataException($"Expected {fields.Length} outgoing values, got {values.Length}.");

        for (int index = 0; index < fields.Length; index++)
        {
            switch (fields[index])
            {
                case OutgoingField.Id:
                    writer.WriteId((Id)values[index]);
                    break;
                case OutgoingField.Int:
                    writer.WriteInt((int)values[index]);
                    break;
                case OutgoingField.String:
                    writer.WriteString((string)values[index]);
                    break;
                case OutgoingField.Bool:
                    writer.WriteBool((bool)values[index]);
                    break;
                case OutgoingField.Long:
                    writer.WriteLong((long)values[index]);
                    break;
                case OutgoingField.WallLocation:
                    writer.Compose((WallLocation)values[index]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fields));
            }
        }
    }

    private static WallLocation ReadWallLocation(in PacketReader reader)
    {
        if (reader.Client is not ClientType.Unity)
            return WallLocation.ParseString(reader.ReadString());

        int wall_x = reader.ReadInt();
        int wall_y = reader.ReadInt();
        int offset_x = reader.ReadInt();
        int offset_y = reader.ReadInt();
        string orientation = reader.ReadString();
        if (orientation.Length != 1)
            throw new InvalidDataException("Unity wall orientation must contain one character.");
        return new WallLocation(wall_x, wall_y, offset_x, offset_y, WallOrientation.FromChar(orientation[0]));
    }

    private static bool MatchesField(OutgoingField field, OutgoingWireType wire_type) => field switch
    {
        OutgoingField.Id => wire_type is OutgoingWireType.Int64 or OutgoingWireType.UInt64,
        OutgoingField.Int => wire_type is OutgoingWireType.Int32 or OutgoingWireType.UInt32,
        OutgoingField.String => wire_type is OutgoingWireType.String,
        OutgoingField.Bool => wire_type is OutgoingWireType.Boolean,
        OutgoingField.Long => wire_type is OutgoingWireType.Int64 or OutgoingWireType.UInt64,
        OutgoingField.WallLocation => wire_type is OutgoingWireType.Unknown,
        _ => false
    };

    private static bool MatchesFlashField(OutgoingField field, OutgoingWireType wire_type) => field switch
    {
        OutgoingField.Id => wire_type is OutgoingWireType.Int32 or OutgoingWireType.UInt32,
        OutgoingField.Int => wire_type is OutgoingWireType.Int32 or OutgoingWireType.UInt32,
        OutgoingField.String => wire_type is OutgoingWireType.String,
        OutgoingField.Bool => wire_type is OutgoingWireType.Boolean,
        OutgoingField.Long => wire_type is OutgoingWireType.Int32 or OutgoingWireType.UInt32,
        OutgoingField.WallLocation => wire_type is OutgoingWireType.String,
        _ => false
    };
}

internal sealed class UnityOutgoingIdListCodec : UnityOutgoingCodec
{
    protected override object ReadUnity(in PacketReader reader) => ReadIds(in reader);
    protected override object ReadFlash(in PacketReader reader) => ReadIds(in reader);
    protected override void WriteUnity(in PacketWriter writer, object value) => WriteIds(in writer, (Id[])value);
    protected override void WriteFlash(in PacketWriter writer, object value) => WriteIds(in writer, (Id[])value);
    protected override object ToFlash(object native_value) => native_value;

    protected override object ToUnity(object native_value, object flash_value)
    {
        Id[] original = (Id[])native_value;
        Id[] changed = (Id[])flash_value;
        var originals_by_projection = original
            .GroupBy(id => unchecked((int)(long)id))
            .ToDictionary(group => group.Key, group => group.Distinct().ToArray());
        var original_counts = original
            .GroupBy(id => unchecked((int)(long)id))
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (IGrouping<int, Id> group in changed.GroupBy(id => unchecked((int)(long)id)))
        {
            if (!originals_by_projection.TryGetValue(group.Key, out Id[]? candidates) ||
                candidates.All(id => (long)id == group.Key))
                continue;
            if (group.Count() > original_counts[group.Key])
                throw new InvalidOperationException($"Flash ID projection {group.Key} has an ambiguous changed multiplicity.");
        }
        var restored = new Id[changed.Length];
        for (int index = 0; index < changed.Length; index++)
        {
            Id changed_id = changed[index];
            int projection = unchecked((int)(long)changed_id);
            if (!originals_by_projection.TryGetValue(projection, out Id[]? candidates))
            {
                restored[index] = changed_id;
                continue;
            }
            if (candidates.Length != 1)
                throw new InvalidOperationException($"Flash ID projection {projection} matches multiple Unity IDs.");
            restored[index] = UnityOutgoingInterception.PreserveId(candidates[0], changed_id);
        }
        return restored;
    }

    protected override object[] ExportFlashValues(object flash_value) => [(Id[])flash_value];

    public override bool MatchesSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Collection is not OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is OutgoingWireType.Int64 or OutgoingWireType.UInt64;

    public override bool MatchesFlashSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Collection is not OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is OutgoingWireType.Int32 or OutgoingWireType.UInt32;

    private static Id[] ReadIds(in PacketReader reader)
    {
        int count = reader.ReadLength();
        var ids = new Id[count];
        for (int index = 0; index < count; index++)
            ids[index] = reader.ReadId();
        return ids;
    }

    private static void WriteIds(in PacketWriter writer, Id[] ids)
    {
        writer.WriteLength((Length)ids.Length);
        foreach (Id id in ids)
            writer.WriteId(id);
    }
}

internal readonly record struct UnityMarketplaceOfferValue(
    int Price,
    int FurniCategory,
    Id[] ItemIds);

internal sealed class UnityMarketplaceMakeOfferCodec : UnityOutgoingCodec
{
    public override bool RequiresVerifiedUnitySchema => true;

    protected override object ReadUnity(in PacketReader reader) =>
        ReadOffer(in reader);

    protected override object ReadFlash(in PacketReader reader) =>
        ReadOffer(in reader);

    protected override void WriteUnity(
        in PacketWriter writer,
        object value) =>
        WriteOffer(in writer, (UnityMarketplaceOfferValue)value);

    protected override void WriteFlash(
        in PacketWriter writer,
        object value) =>
        WriteOffer(in writer, (UnityMarketplaceOfferValue)value);

    protected override object ToFlash(object native_value) =>
        native_value;

    protected override object ToUnity(
        object native_value,
        object flash_value)
    {
        UnityMarketplaceOfferValue original =
            (UnityMarketplaceOfferValue)native_value;
        UnityMarketplaceOfferValue changed =
            (UnityMarketplaceOfferValue)flash_value;
        return changed with
        {
            ItemIds = RestoreIds(
                original.ItemIds,
                changed.ItemIds)
        };
    }

    protected override object[] ExportFlashValues(
        object flash_value)
    {
        UnityMarketplaceOfferValue offer =
            (UnityMarketplaceOfferValue)flash_value;
        return
        [
            offer.Price,
            offer.FurniCategory,
            offer.ItemIds
        ];
    }

    public override bool MatchesSchema(
        OutgoingMessageSchema schema) =>
        MatchesSchema(
            schema,
            OutgoingWireType.Int64,
            OutgoingWireType.UInt64);

    public override bool MatchesFlashSchema(
        OutgoingMessageSchema schema) =>
        MatchesSchema(
            schema,
            OutgoingWireType.Int32,
            OutgoingWireType.UInt32);

    private static bool MatchesSchema(
        OutgoingMessageSchema schema,
        OutgoingWireType signed_id,
        OutgoingWireType unsigned_id) =>
        schema.Parameters.Count == 3 &&
        schema.Parameters[0].Collection is
            OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is
            OutgoingWireType.Int32 or
            OutgoingWireType.UInt32 &&
        schema.Parameters[1].Collection is
            OutgoingCollectionKind.None &&
        schema.Parameters[1].WireType is
            OutgoingWireType.Int32 or
            OutgoingWireType.UInt32 &&
        schema.Parameters[2].Collection is not
            OutgoingCollectionKind.None &&
        (schema.Parameters[2].WireType == signed_id ||
            schema.Parameters[2].WireType == unsigned_id);

    private static UnityMarketplaceOfferValue ReadOffer(
        in PacketReader reader)
    {
        int price = reader.ReadInt();
        int category = reader.ReadInt();
        int count = reader.ReadLength();
        var item_ids = new Id[count];
        for (int index = 0; index < count; index++)
            item_ids[index] = reader.ReadId();
        return new UnityMarketplaceOfferValue(
            price,
            category,
            item_ids);
    }

    private static void WriteOffer(
        in PacketWriter writer,
        UnityMarketplaceOfferValue offer)
    {
        writer.WriteInt(offer.Price);
        writer.WriteInt(offer.FurniCategory);
        writer.WriteLength((Length)offer.ItemIds.Length);
        foreach (Id item_id in offer.ItemIds)
            writer.WriteId(item_id);
    }

    private static Id[] RestoreIds(
        IReadOnlyList<Id> original,
        IReadOnlyList<Id> changed)
    {
        Dictionary<int, Id[]> originals = original
            .GroupBy(Project)
            .ToDictionary(
                group => group.Key,
                group => group.Distinct().ToArray());
        foreach (IGrouping<int, Id> group in
                 changed.GroupBy(Project))
        {
            if (originals.TryGetValue(
                    group.Key,
                    out Id[]? candidates) &&
                candidates.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Flash marketplace item ID projection {group.Key} matches multiple Unity IDs.");
            }
        }

        var restored = new Id[changed.Count];
        for (int index = 0; index < changed.Count; index++)
        {
            Id item_id = changed[index];
            int projection = Project(item_id);
            restored[index] = originals.TryGetValue(
                    projection,
                    out Id[]? candidates)
                ? UnityOutgoingInterception.PreserveId(
                    candidates[0],
                    item_id)
                : item_id;
        }
        return restored;
    }

    private static int Project(Id id) =>
        unchecked((int)(long)id);
}

internal readonly record struct UnityForumReadMarkerValue(
    Id GroupId,
    int LastReadMessageId,
    bool MarkAsRead);

internal sealed class UnityForumReadMarkersOutgoingCodec : UnityOutgoingCodec
{
    public override bool RequiresVerifiedUnitySchema => true;
    protected override object ReadUnity(in PacketReader reader) => ReadMarkers(in reader);
    protected override object ReadFlash(in PacketReader reader) => ReadMarkers(in reader);
    protected override void WriteUnity(in PacketWriter writer, object value) =>
        WriteMarkers(in writer, (UnityForumReadMarkerValue[])value);
    protected override void WriteFlash(in PacketWriter writer, object value) =>
        WriteMarkers(in writer, (UnityForumReadMarkerValue[])value);
    protected override object ToFlash(object native_value) => native_value;

    protected override object ToUnity(object native_value, object flash_value)
    {
        UnityForumReadMarkerValue[] original = (UnityForumReadMarkerValue[])native_value;
        UnityForumReadMarkerValue[] changed = (UnityForumReadMarkerValue[])flash_value;
        var originals_by_projection = original
            .GroupBy(marker => unchecked((int)(long)marker.GroupId))
            .ToDictionary(
                group => group.Key,
                group => group.Select(marker => marker.GroupId).Distinct().ToArray());
        var original_counts = original
            .GroupBy(marker => unchecked((int)(long)marker.GroupId))
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (IGrouping<int, UnityForumReadMarkerValue> group in
                 changed.GroupBy(marker => unchecked((int)(long)marker.GroupId)))
        {
            if (!originals_by_projection.TryGetValue(group.Key, out Id[]? candidates) ||
                candidates.All(id => (long)id == group.Key))
            {
                continue;
            }

            if (group.Count() > original_counts[group.Key])
                throw new InvalidOperationException($"Flash forum group ID projection {group.Key} has an ambiguous changed multiplicity.");
        }

        var restored = new UnityForumReadMarkerValue[changed.Length];
        for (int index = 0; index < changed.Length; index++)
        {
            UnityForumReadMarkerValue marker = changed[index];
            int projection = unchecked((int)(long)marker.GroupId);
            if (!originals_by_projection.TryGetValue(projection, out Id[]? candidates))
            {
                restored[index] = marker;
                continue;
            }

            if (candidates.Length != 1)
                throw new InvalidOperationException($"Flash forum group ID projection {projection} matches multiple Unity IDs.");

            restored[index] = marker with
            {
                GroupId = UnityOutgoingInterception.PreserveId(candidates[0], marker.GroupId)
            };
        }
        return restored;
    }

    protected override object[] ExportFlashValues(object flash_value)
    {
        UnityForumReadMarkerValue[] markers = (UnityForumReadMarkerValue[])flash_value;
        var values = new object[1 + markers.Length * 3];
        values[0] = markers.Length;
        for (int index = 0; index < markers.Length; index++)
        {
            int offset = 1 + index * 3;
            values[offset] = markers[index].GroupId;
            values[offset + 1] = markers[index].LastReadMessageId;
            values[offset + 2] = markers[index].MarkAsRead;
        }
        return values;
    }

    public override bool MatchesSchema(OutgoingMessageSchema schema) =>
        MatchesCollectionSchema(schema);

    public override bool MatchesFlashSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 0 || MatchesCollectionSchema(schema);

    private static bool MatchesCollectionSchema(OutgoingMessageSchema schema) =>
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

    private static UnityForumReadMarkerValue[] ReadMarkers(in PacketReader reader)
    {
        int count = reader.ReadLength();
        var markers = new UnityForumReadMarkerValue[count];
        for (int index = 0; index < count; index++)
        {
            markers[index] = new UnityForumReadMarkerValue(
                reader.ReadId(),
                reader.ReadInt(),
                reader.ReadBool());
        }
        return markers;
    }

    private static void WriteMarkers(
        in PacketWriter writer,
        UnityForumReadMarkerValue[] markers)
    {
        writer.WriteLength((Length)markers.Length);
        foreach (UnityForumReadMarkerValue marker in markers)
        {
            writer.WriteId(marker.GroupId);
            writer.WriteInt(marker.LastReadMessageId);
            writer.WriteBool(marker.MarkAsRead);
        }
    }
}

internal sealed record UnityRoomSettingsValue(
    Id RoomId,
    string Name,
    string Description,
    int DoorMode,
    string Password,
    int CategoryId,
    bool AllowPets,
    int WhoCanMute,
    int WhoCanKick,
    int WhoCanBan,
    int MaximumVisitors,
    int TradeMode,
    int AllowFoodConsume,
    int AllowWalkThrough,
    Id[] NftGroupIds,
    bool LegacyLayout);

internal sealed record FlashRoomSettingsValue(
    Id RoomId,
    string Name,
    string Description,
    int DoorMode,
    string Password,
    int MaximumVisitors,
    int CategoryId,
    string[] Tags,
    int TradeMode,
    bool AllowPets,
    bool AllowFoodConsume,
    bool AllowWalkThrough,
    bool HideWalls,
    int WallThickness,
    int FloorThickness,
    int WhoCanMute,
    int WhoCanKick,
    int WhoCanBan,
    int ChatFloodSensitivity,
    bool LeaveOnDoorTile,
    bool IdleSleepEnabled,
    int IdleSleepTimeoutSeconds,
    bool IdleAutokickEnabled,
    int IdleAutokickTimeoutSeconds,
    bool MuteAllPets);

internal sealed class UnityRoomSettingsOutgoingCodec : UnityOutgoingCodec
{
    public override bool MatchesSchema(OutgoingMessageSchema schema)
    {
        int count = schema.Parameters.Count;
        if (count is not 12 and not 15)
            return false;

        for (int index = 0; index < count; index++)
        {
            OutgoingParameterSchema parameter = schema.Parameters[index];
            if (index == count - 1)
            {
                if (parameter.Collection is OutgoingCollectionKind.None ||
                    parameter.WireType is not OutgoingWireType.Int64 and not OutgoingWireType.UInt64)
                    return false;
                continue;
            }

            if (parameter.Collection is not OutgoingCollectionKind.None ||
                !MatchesUnityRoomField(index, parameter.WireType))
                return false;
        }
        return true;
    }

    public override bool MatchesFlashSchema(OutgoingMessageSchema schema)
    {
        if (schema.Parameters.Count != 25)
            return false;

        for (int index = 0; index < schema.Parameters.Count; index++)
        {
            OutgoingParameterSchema parameter = schema.Parameters[index];
            if (index == 7)
            {
                if (parameter.Collection is OutgoingCollectionKind.None ||
                    parameter.WireType is not OutgoingWireType.String and not OutgoingWireType.Unknown)
                    return false;
                continue;
            }

            if (parameter.Collection is not OutgoingCollectionKind.None ||
                !MatchesFlashRoomField(index, parameter.WireType))
                return false;
        }
        return true;
    }

    protected override object ReadUnity(in PacketReader reader)
    {
        Id room_id = reader.ReadId();
        string name = reader.ReadString();
        string description = reader.ReadString();
        int door_mode = reader.ReadInt();
        string password = reader.ReadString();
        int category_id = reader.ReadInt();
        bool allow_pets = reader.ReadBool();
        int who_can_mute = reader.ReadInt();
        int who_can_kick = reader.ReadInt();
        int who_can_ban = reader.ReadInt();
        int maximum_visitors = reader.ReadInt();
        if (reader.Available >= 14 && reader.Available % 8 == 6)
        {
            int trade_mode = reader.ReadInt();
            int allow_food = reader.ReadInt();
            int allow_walk = reader.ReadInt();
            Id[] nft_group_ids = reader.ReadIdArray();
            return new UnityRoomSettingsValue(
                room_id, name, description, door_mode, password, category_id, allow_pets,
                who_can_mute, who_can_kick, who_can_ban, maximum_visitors,
                trade_mode, allow_food, allow_walk, nft_group_ids, false);
        }

        if (reader.Available < 2 || reader.Available % 8 != 2)
            throw new InvalidDataException("Unity SaveRoomSettings has an invalid trailing layout.");
        Id[] legacy_nft_groups = reader.ReadIdArray();
        return new UnityRoomSettingsValue(
            room_id, name, description, door_mode, password, category_id, allow_pets,
            who_can_mute, who_can_kick, who_can_ban, maximum_visitors,
            0, 0, 0, legacy_nft_groups, true);
    }

    protected override object ReadFlash(in PacketReader reader)
    {
        Id room_id = reader.ReadId();
        string name = reader.ReadString();
        string description = reader.ReadString();
        int door_mode = reader.ReadInt();
        string password = reader.ReadString();
        int maximum_visitors = reader.ReadInt();
        int category_id = reader.ReadInt();
        string[] tags = reader.ReadStringArray();
        return new FlashRoomSettingsValue(
            room_id,
            name,
            description,
            door_mode,
            password,
            maximum_visitors,
            category_id,
            tags,
            reader.ReadInt(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadInt(),
            reader.ReadInt(),
            reader.ReadInt(),
            reader.ReadInt(),
            reader.ReadInt(),
            reader.ReadInt(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadInt(),
            reader.ReadBool(),
            reader.ReadInt(),
            reader.ReadBool());
    }

    protected override void WriteUnity(in PacketWriter writer, object value)
    {
        var settings = (UnityRoomSettingsValue)value;
        writer.WriteId(settings.RoomId);
        writer.WriteString(settings.Name);
        writer.WriteString(settings.Description);
        writer.WriteInt(settings.DoorMode);
        writer.WriteString(settings.Password);
        writer.WriteInt(settings.CategoryId);
        writer.WriteBool(settings.AllowPets);
        writer.WriteInt(settings.WhoCanMute);
        writer.WriteInt(settings.WhoCanKick);
        writer.WriteInt(settings.WhoCanBan);
        writer.WriteInt(settings.MaximumVisitors);
        if (!settings.LegacyLayout)
        {
            writer.WriteInt(settings.TradeMode);
            writer.WriteInt(settings.AllowFoodConsume);
            writer.WriteInt(settings.AllowWalkThrough);
        }
        writer.WriteIdArray(settings.NftGroupIds);
    }

    protected override void WriteFlash(in PacketWriter writer, object value)
    {
        var settings = (FlashRoomSettingsValue)value;
        writer.WriteId(settings.RoomId);
        writer.WriteString(settings.Name);
        writer.WriteString(settings.Description);
        writer.WriteInt(settings.DoorMode);
        writer.WriteString(settings.Password);
        writer.WriteInt(settings.MaximumVisitors);
        writer.WriteInt(settings.CategoryId);
        writer.WriteStringArray(settings.Tags);
        writer.WriteInt(settings.TradeMode);
        writer.WriteBool(settings.AllowPets);
        writer.WriteBool(settings.AllowFoodConsume);
        writer.WriteBool(settings.AllowWalkThrough);
        writer.WriteBool(settings.HideWalls);
        writer.WriteInt(settings.WallThickness);
        writer.WriteInt(settings.FloorThickness);
        writer.WriteInt(settings.WhoCanMute);
        writer.WriteInt(settings.WhoCanKick);
        writer.WriteInt(settings.WhoCanBan);
        writer.WriteInt(settings.ChatFloodSensitivity);
        writer.WriteBool(settings.LeaveOnDoorTile);
        writer.WriteBool(settings.IdleSleepEnabled);
        writer.WriteInt(settings.IdleSleepTimeoutSeconds);
        writer.WriteBool(settings.IdleAutokickEnabled);
        writer.WriteInt(settings.IdleAutokickTimeoutSeconds);
        writer.WriteBool(settings.MuteAllPets);
    }

    protected override object ToFlash(object native_value)
    {
        var settings = (UnityRoomSettingsValue)native_value;
        return new FlashRoomSettingsValue(
            settings.RoomId,
            settings.Name,
            settings.Description,
            settings.DoorMode,
            settings.Password,
            settings.MaximumVisitors,
            settings.CategoryId,
            [],
            settings.TradeMode,
            settings.AllowPets,
            settings.AllowFoodConsume != 0,
            settings.AllowWalkThrough != 0,
            false,
            0,
            0,
            settings.WhoCanMute,
            settings.WhoCanKick,
            settings.WhoCanBan,
            0,
            false,
            false,
            0,
            false,
            0,
            false);
    }

    protected override object ToUnity(object native_value, object flash_value)
    {
        var original = (UnityRoomSettingsValue)native_value;
        var changed = (FlashRoomSettingsValue)flash_value;
        EnsureFlashOnlyFieldsUnchanged(changed);
        if (original.LegacyLayout && (changed.TradeMode != 0 || changed.AllowFoodConsume || changed.AllowWalkThrough))
            throw new InvalidOperationException("UNITY11 room settings cannot represent Flash trade or consumption settings.");

        return original with
        {
            RoomId = UnityOutgoingInterception.PreserveId(original.RoomId, changed.RoomId),
            Name = changed.Name,
            Description = changed.Description,
            DoorMode = changed.DoorMode,
            Password = changed.Password,
            CategoryId = changed.CategoryId,
            AllowPets = changed.AllowPets,
            WhoCanMute = changed.WhoCanMute,
            WhoCanKick = changed.WhoCanKick,
            WhoCanBan = changed.WhoCanBan,
            MaximumVisitors = changed.MaximumVisitors,
            TradeMode = changed.TradeMode,
            AllowFoodConsume = changed.AllowFoodConsume ? 1 : 0,
            AllowWalkThrough = changed.AllowWalkThrough ? 1 : 0
        };
    }

    protected override object[] ExportFlashValues(object flash_value)
    {
        var settings = (FlashRoomSettingsValue)flash_value;
        return
        [
            settings.RoomId,
            settings.Name,
            settings.Description,
            settings.DoorMode,
            settings.Password,
            settings.MaximumVisitors,
            settings.CategoryId,
            settings.Tags.Length,
            .. settings.Tags,
            settings.TradeMode,
            settings.AllowPets,
            settings.AllowFoodConsume,
            settings.AllowWalkThrough,
            settings.HideWalls,
            settings.WallThickness,
            settings.FloorThickness,
            settings.WhoCanMute,
            settings.WhoCanKick,
            settings.WhoCanBan,
            settings.ChatFloodSensitivity,
            settings.LeaveOnDoorTile,
            settings.IdleSleepEnabled,
            settings.IdleSleepTimeoutSeconds,
            settings.IdleAutokickEnabled,
            settings.IdleAutokickTimeoutSeconds,
            settings.MuteAllPets
        ];
    }

    private static void EnsureFlashOnlyFieldsUnchanged(FlashRoomSettingsValue settings)
    {
        if (settings.Tags.Length != 0 ||
            settings.HideWalls ||
            settings.WallThickness != 0 ||
            settings.FloorThickness != 0 ||
            settings.ChatFloodSensitivity != 0 ||
            settings.LeaveOnDoorTile ||
            settings.IdleSleepEnabled ||
            settings.IdleSleepTimeoutSeconds != 0 ||
            settings.IdleAutokickEnabled ||
            settings.IdleAutokickTimeoutSeconds != 0 ||
            settings.MuteAllPets)
        {
            throw new InvalidOperationException("Unity room settings cannot represent the edited Flash-only fields.");
        }
    }

    private static bool MatchesUnityRoomField(int index, OutgoingWireType type) => index switch
    {
        0 => type is OutgoingWireType.Int64 or OutgoingWireType.UInt64,
        1 or 2 or 4 => type is OutgoingWireType.String,
        6 => type is OutgoingWireType.Boolean,
        _ => type is OutgoingWireType.Int32 or OutgoingWireType.UInt32
    };

    private static bool MatchesFlashRoomField(int index, OutgoingWireType type) => index switch
    {
        0 => type is OutgoingWireType.Int32 or OutgoingWireType.UInt32,
        1 or 2 or 4 => type is OutgoingWireType.String,
        9 or 10 or 11 or 12 or 19 or 20 or 22 or 24 => type is OutgoingWireType.Boolean,
        _ => type is OutgoingWireType.Int32 or OutgoingWireType.UInt32
    };
}

internal static class UnityOutgoingInterception
{
    private static readonly OutgoingField id = OutgoingField.Id;
    private static readonly OutgoingField integer = OutgoingField.Int;
    private static readonly OutgoingField text = OutgoingField.String;
    private static readonly OutgoingField boolean = OutgoingField.Bool;
    private static readonly OutgoingField long_integer = OutgoingField.Long;
    private static readonly OutgoingField wall_location = OutgoingField.WallLocation;

    private static readonly Dictionary<string, UnityOutgoingCodec> Codecs = Build();

    public static void Invoke(
        string requested_name,
        Intercept intercept,
        Action<Intercept> handler,
        MessageManager messages)
    {
        if (intercept.Packet.Client is not ClientType.Unity)
        {
            handler(intercept);
            return;
        }

        string schema_name = UnityOutgoingCompatibility.SchemaName(requested_name);
        if (schema_name.Equals("PlaceObject", StringComparison.OrdinalIgnoreCase))
            schema_name = ResolvePlacementSchema(intercept.Packet);

        messages.TryGetOutgoingSchemas(
            ClientType.Unity,
            intercept.Packet.Header,
            out IReadOnlyList<OutgoingMessageSchema> schemas);
        IReadOnlyList<OutgoingMessageSchema> flash_schemas = FlashSchemas(messages, requested_name, schema_name);
        bool runtime_schema_match = UnityOutgoingRuntimeCodec.TryCreate(
            intercept.Packet,
            schemas,
            out UnityOutgoingRuntimeCodec runtime_codec);
        bool runtime_match = runtime_schema_match && runtime_codec.MatchesFlashSchemas(flash_schemas);

        if (Codecs.TryGetValue(schema_name, out UnityOutgoingCodec? static_codec) &&
            static_codec.MatchesUnity(intercept.Packet))
        {
            UnityOutgoingCodec codec = static_codec;
            bool static_schema_match = StaticSchemaAllowed(static_codec, schemas, flash_schemas);
            if (!static_schema_match ||
                runtime_schema_match && !static_codec.MatchesSchema(runtime_codec.Schema))
            {
                if (!runtime_match)
                    throw new NotSupportedException($"Unity outgoing message '{requested_name}' has no verified Flash wire projection. Use OnUnityOut for its native payload.");
                codec = runtime_codec;
            }
            codec.Invoke(requested_name, intercept, handler);
            return;
        }

        if (!runtime_match)
            throw new NotSupportedException($"Unity outgoing message '{requested_name}' has no verified Flash wire projection. Use OnUnityOut for its native payload.");

        runtime_codec.Invoke(requested_name, intercept, handler);
    }

    public static IReadOnlyList<string> AdditionalUnityNames(string name)
    {
        string schema_name = UnityOutgoingCompatibility.SchemaName(name);
        return schema_name.Equals("PlaceObject", StringComparison.OrdinalIgnoreCase)
            ? ["PlaceWallItem"]
            : [];
    }

    public static bool HasStaticCodec(string name) => Codecs.ContainsKey(name);

    public static object[] ReadFlashValues(
        string requested_name,
        IPacket packet,
        MessageManager messages)
    {
        if (packet.Client is not ClientType.Flash || packet.Header.Direction is not Direction.Out)
            throw new ArgumentException("Outgoing compatibility requires a Flash outgoing packet.", nameof(packet));

        string schema_name = UnityOutgoingCompatibility.SchemaName(requested_name);
        messages.TryGetOutgoingSchemas(
            ClientType.Unity,
            packet.Header,
            out IReadOnlyList<OutgoingMessageSchema> schemas);
        IReadOnlyList<OutgoingMessageSchema> flash_schemas = FlashSchemas(messages, requested_name, schema_name);
        bool runtime_match = UnityOutgoingRuntimeCodec.TryCreateFlash(
            packet,
            schemas,
            flash_schemas,
            out UnityOutgoingRuntimeCodec runtime_codec);

        if (Codecs.TryGetValue(schema_name, out UnityOutgoingCodec? static_codec) && static_codec.MatchesFlash(packet))
        {
            bool static_schema_match = StaticSchemaAllowed(static_codec, schemas, flash_schemas);
            if (!static_schema_match && !runtime_match)
                throw new NotSupportedException($"Unity outgoing message '{requested_name}' has no verified Flash-to-Unity wire projection.");
            UnityOutgoingCodec codec = !static_schema_match ||
                runtime_match && !static_codec.MatchesSchema(runtime_codec.Schema)
                    ? runtime_codec
                    : static_codec;
            return codec.ReadFlashValues(packet);
        }

        if (!runtime_match)
            throw new NotSupportedException($"Unity outgoing message '{requested_name}' has no verified Flash-to-Unity wire projection.");
        return runtime_codec.ReadFlashValues(packet);
    }

    private static IReadOnlyList<OutgoingMessageSchema> FlashSchemas(
        MessageManager messages,
        string requested_name,
        string schema_name)
    {
        if (messages.TryGetOutgoingSchemas(ClientType.Flash, requested_name, out IReadOnlyList<OutgoingMessageSchema> schemas))
            return schemas;
        if (!schema_name.Equals(requested_name, StringComparison.OrdinalIgnoreCase) &&
            messages.TryGetOutgoingSchemas(ClientType.Flash, schema_name, out schemas))
            return schemas;
        return [];
    }

    private static bool StaticSchemaAllowed(
        UnityOutgoingCodec codec,
        IReadOnlyList<OutgoingMessageSchema> schemas,
        IReadOnlyList<OutgoingMessageSchema> flash_schemas) =>
        (codec.RequiresVerifiedUnitySchema
            ? schemas.Any(codec.MatchesSchema)
            : SchemaAllowed(schemas, codec.MatchesSchema)) &&
        SchemaAllowed(flash_schemas, codec.MatchesFlashSchema);

    private static bool SchemaAllowed(
        IReadOnlyList<OutgoingMessageSchema> schemas,
        Func<OutgoingMessageSchema, bool> matches) =>
        schemas.Count == 0 ||
        schemas.Any(matches) ||
        schemas.All(schema =>
            schema.Parameters.Any(parameter =>
                parameter.WireType is OutgoingWireType.Unknown) &&
            schema.Parameters.All(parameter =>
                parameter.ElementWireTypes is null));

    public static Id PreserveId(Id original, Id changed)
    {
        int projected = unchecked((int)(long)original);
        return ((long)original > int.MaxValue || (long)original < int.MinValue) && (long)changed == projected
            ? original
            : changed;
    }

    private static Dictionary<string, UnityOutgoingCodec> Build()
    {
        var codecs = new Dictionary<string, UnityOutgoingCodec>(StringComparer.OrdinalIgnoreCase);

        Add(codecs, new UnityOutgoingSchemaCodec(text, integer, integer), "Chat");
        Add(codecs, new UnityOutgoingSchemaCodec(text, integer), "Shout");
        Add(codecs, new UnityOutgoingSchemaCodec(text),
            "ChangeMotto", "GetCatalogIndex", "HabboSearch", "RequestFriend");
        Add(codecs, new UnityOutgoingSchemaCodec(text, text), "NewNavigatorSearch", "UpdateFigureData");
        Add(codecs, new UnityOutgoingSchemaCodec(text, boolean), "LetUserIn");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, integer), "Move", "LookTo");
        Add(codecs, new UnityOutgoingSchemaCodec(integer),
            "AvatarEffectActivated", "AvatarEffectSelected", "Dance", "Expression", "OpenTrading", "Posture",
            "RateFlat", "Sign");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, integer, text), "GetCatalogPage");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, integer, text, integer),
            "PurchaseFromCatalog");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, text, text), "SaveWardrobeOutfit");
        Add(codecs, new UnityOutgoingSchemaCodec(text, text, text, integer, integer, integer), "CreateFlat");
        Add(codecs, new UnityOutgoingSchemaCodec(),
            "AcceptTrading", "CancelTyping", "CloseTrading", "ConfirmAcceptTrading", "GetBadges",
            "GetMarketplaceCanMakeOffer", "GetMarketplaceConfiguration", "GetWardrobe",
            "BuyMarketplaceTokens", "CancelAllMarketplaceOffers", "Quit",
            "RedeemMarketplaceOfferCredits", "StartTyping", "UnacceptTrading");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [integer, integer, text, integer],
            [integer, integer, text, integer, boolean],
            values => [.. values, true],
            (_, changed) =>
            {
                if (!(bool)changed[4])
                {
                    throw new InvalidOperationException(
                        "Unity marketplace search cannot disable unique-offer grouping.");
                }
                return changed[..4];
            }),
            "GetMarketplaceOffers");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [],
            [integer],
            _ => [1],
            (_, changed) =>
            {
                if ((int)changed[0] != 1)
                {
                    throw new InvalidOperationException(
                        "Unity marketplace own offers only support the open-offers category.");
                }
                return [];
            }),
            "GetMarketplaceOwnOffers");

        Add(
            codecs,
            new UnityMarketplaceMakeOfferCodec(),
            "MakeOffer");

        Add(codecs, new UnityOutgoingSchemaCodec(id, text, text, text), "AddSpamWallPostIt");
        Add(codecs, new UnityOutgoingSchemaCodec(id, id),
            "ApproveMembershipRequest", "RejectMembershipRequest");
        Add(codecs, new UnityOutgoingSchemaCodec(id),
            "AssignRights", "BuyMarketplaceOffer", "CancelMarketplaceOffer", "CloseChest",
            "DeleteRoom", "DeselectFavouriteHabboGroup", "DiceOff", "FollowFriend", "GetItemData", "GetPetInfo",
            "GetRoomSettings", "GetSelectedBadges", "IgnoreUser", "JoinHabboGroup", "KickUser",
            "OpenChestAndGetContents", "RemoveBotFromFlat", "RemoveItemFromTrade", "RemovePetFromFlat",
            "RespectPet", "SelectFavouriteHabboGroup", "StartAddingToChest", "ThrowDice",
            "UnignoreUser", "UpdateHomeRoom", "WithdrawAllFromChest");
        Add(codecs, new UnityOutgoingSchemaCodec(id, boolean),
            "GetExtendedProfile", "GetHabboGroupDetails", "MountPet", "ToggleStaffPick");
        Add(codecs, new UnityOutgoingSchemaCodec(id, id, boolean), "KickMember");
        Add(codecs, new UnityOutgoingSchemaCodec(id, id, integer), "MuteUser");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer, integer), "MoveObject", "PlaceRoomItem");
        Add(codecs, new UnityOutgoingSchemaCodec(id, wall_location), "MoveWallItem", "PlaceWallItem");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, id), "PickupObject");
        Add(codecs, new UnityOutgoingSchemaCodec(id, text), "PlacePostIt");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer), "UseStuff", "WithdrawCoinsFromChest");
        Add(codecs, new UnityOutgoingSchemaCodec(id, boolean, boolean, integer), "SetChestOptions");
        Add(codecs, new UnityOutgoingSchemaCodec(id, text, text, boolean, boolean, integer, integer, integer, boolean), "SetChestPreferences");
        Add(codecs, new UnityOutgoingSchemaCodec(id, boolean, integer, text, integer), "WithdrawItemsFromChest");

        var id_list = new UnityOutgoingIdListCodec();
        Add(codecs, id_list, "AcceptFriend", "AddItemsToTrade", "RemoveFriend", "RemoveRights");

        Add(codecs, new UnityOutgoingSchemaCodec(id), "GetForumStats");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer),
            "GetForumThreads", "GetThreads");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer, integer),
            "GetForumThreadMessages", "GetMessages");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer),
            "GetForumThread", "GetThread");
        Add(codecs, new UnityOutgoingSchemaCodec(integer, integer, integer), "GetForumsList");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer, integer, integer),
            "UpdateForumSettings");
        Add(codecs, new UnityOutgoingSchemaCodec(), "GetUnreadForumsCount");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, text, text),
            "PostForumMessage", "PostMessage");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer),
            "ModerateForumThread", "ModerateThread");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, integer, integer),
            "ModerateForumMessage", "ModerateMessage");
        Add(codecs, new UnityOutgoingSchemaCodec(id, integer, boolean, boolean),
            "UpdateForumThread", "UpdateThread");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, integer, integer, text],
            [id, integer, integer, text, text, text],
            values => [.. values, "", ""],
            (original, changed) => MergeForumReport(original, changed, "ReportForumThread")),
            "ReportForumThread", "CallForHelpFromForumThread");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, integer, integer, integer, text],
            [id, integer, integer, integer, text, text, text],
            values => [.. values, "", ""],
            (original, changed) => MergeForumReport(original, changed, "ReportForumMessage")),
            "ReportForumMessage", "CallForHelpFromForumMessage");

        var forum_read_markers = new UnityForumReadMarkersOutgoingCodec();
        Add(codecs, forum_read_markers, "UpdateForumReadMarkers", "UpdateForumReadMarker");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, id, text],
            [id, text, id],
            values => [values[0], values[2], values[1]],
            (original, changed) =>
            [
                PreserveId((Id)original[0], (Id)changed[0]),
                PreserveId((Id)original[1], (Id)changed[2]),
                changed[1]
            ]), "BanUserWithDuration");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [text, text, integer],
            [text, integer],
            values => [$"{values[0]} {values[1]}", values[2]],
            (_, changed) => SplitWhisper(changed)), "Whisper");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id],
            [id, integer],
            values => [values[0], 0],
            (original, changed) =>
            {
                if ((int)changed[1] != 0)
                    throw new InvalidOperationException("Unity UseWallItem cannot represent a Flash state value.");
                return [PreserveId((Id)original[0], (Id)changed[0])];
            }), "UseWallItem");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, text],
            [id, text, integer],
            values => [values[0], values[1], 0],
            (original, changed) =>
            {
                if ((int)changed[2] != 0)
                    throw new InvalidOperationException("Unity SendMsg cannot represent a Flash confirmation identifier.");
                return [PreserveId((Id)original[0], (Id)changed[0]), changed[1]];
            }), "SendMsg");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, text, long_integer],
            [id, text, integer],
            values => [values[0], values[1], unchecked((int)(long)values[2])],
            (original, changed) =>
            {
                int projected = unchecked((int)(long)original[2]);
                long timestamp = (int)changed[2] == projected ? (long)original[2] : (int)changed[2];
                return [PreserveId((Id)original[0], (Id)changed[0]), changed[1], timestamp];
            }), "OpenFlatConnection");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, text],
            [id],
            values => [values[0]],
            (original, changed) => [PreserveId((Id)original[0], (Id)changed[0]), original[1]]),
            "GetRelationshipStatusInfo");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [integer, integer, text],
            [integer, integer],
            values => values[..2],
            (original, changed) => [changed[0], changed[1], original[2]]),
            "GetMarketplaceItemStats");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [integer, integer, text, text, text, integer, integer, integer, boolean, integer],
            [integer, integer, text, text, text, integer, integer, integer, boolean],
            values => values[..9],
            (original, changed) => [.. changed, original[9]]),
            "PurchaseFromCatalogAsGift");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, integer, integer, integer],
            [text],
            values => [$"{values[0]} {values[1]} {values[2]} {values[3]}"],
            (original, changed) => MergePlacement(original, changed, "PlaceRoomItem")),
            "PlaceRoomItem");

        Add(codecs, new UnityOutgoingSchemaCodec(
            [id, wall_location],
            [text],
            values => [$"{values[0]} {values[1]}"],
            (original, changed) => MergePlacement(original, changed, "PlaceWallItem")),
            "PlaceWallItem");

        codecs["SaveRoomSettings"] = new UnityRoomSettingsOutgoingCodec();
        return codecs;
    }

    private static object[] MergeForumReport(
        object[] original,
        object[] changed,
        string name)
    {
        if (changed.Length != original.Length + 2 ||
            changed[^2] is not string first_context ||
            changed[^1] is not string second_context)
        {
            throw new InvalidOperationException($"Flash {name} does not match its verified wire schema.");
        }
        if (first_context.Length != 0 || second_context.Length != 0)
            throw new InvalidOperationException($"Unity {name} cannot represent Flash report context values.");

        object[] restored = changed[..original.Length];
        restored[0] = PreserveId((Id)original[0], (Id)changed[0]);
        return restored;
    }

    private static object[] SplitWhisper(object[] changed)
    {
        string combined = (string)changed[0];
        int separator = combined.IndexOf(' ');
        if (separator <= 0 || separator == combined.Length - 1)
            throw new InvalidOperationException("Flash Whisper payload must contain a recipient followed by a message.");
        return [combined[..separator], combined[(separator + 1)..], changed[1]];
    }

    private static object[] MergePlacement(object[] original, object[] changed, string expected_schema)
    {
        UnityOutgoingMessage translated = UnityOutgoingCompatibility.Translate("PlaceObject", changed);
        if (!translated.SchemaName.Equals(expected_schema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"A {expected_schema} packet cannot change placement type through its Flash wire view.");
        object[] values = [.. translated.Values];
        values[0] = PreserveId((Id)original[0], (Id)values[0]);
        return values;
    }

    private static string ResolvePlacementSchema(IPacket packet)
    {
        int position = 0;
        PacketReader reader = packet.ReaderAt(ref position);
        reader.ReadId();
        if (reader.Available == 12)
            return "PlaceRoomItem";
        return "PlaceWallItem";
    }

    private static void Add(
        IDictionary<string, UnityOutgoingCodec> codecs,
        UnityOutgoingCodec codec,
        params string[] names)
    {
        foreach (string name in names)
            codecs[name] = codec;
    }
}
