using Qx.Messages;

namespace Qx.Model.Forums;

internal static class ForumRequestProtocol
{
    public static T ParseRoot<T>(
        in PacketReader p,
        ModernPacketParser<T> flash,
        ModernPacketParser<T> unity)
    {
        T value = ModernWireClients.Parse(in p, flash, unity);
        ForumProtocol.RequireEmpty(in p, typeof(T).Name);
        return value;
    }

    public static Id ReadFlashGroupId(in PacketReader p) =>
        ForumProtocol.ReadFlashId(in p);

    public static Id ReadUnityGroupId(in PacketReader p) => p.ReadLong();

    public static void WriteFlashGroupId(in PacketWriter p, Id group_id) =>
        ForumProtocol.WriteFlashId(in p, group_id);

    public static void WriteUnityGroupId(in PacketWriter p, Id group_id) =>
        p.WriteLong(group_id);

    public static void PrepareIds(
        ClientType client,
        Id group_id,
        params Id[] int_ids)
    {
        if (client is ClientType.Flash)
            ForumProtocol.RequireFlashId(group_id, "forum group");
        else if (client is not ClientType.Unity)
            throw new UnsupportedClientException(client);
        for (int index = 0; index < int_ids.Length; index++)
            ForumProtocol.RequireFlashId(int_ids[index], "forum field");
    }

    public static void PrepareStrings(in PacketWriter p, params string[] values)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        for (int index = 0; index < values.Length; index++)
            budget.Require(values[index], "forum text", in p);
    }

    public static Id ReadIntId(in PacketReader p) => p.ReadInt();

    public static void WriteIntId(
        in PacketWriter p,
        Id value,
        string field)
    {
        long id = value;
        if (id is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException($"Forum {field} id {id} exceeds the signed 32-bit range.");
        p.WriteInt((int)id);
    }

    public static int ReadFlashMarkerCount(in PacketReader p) =>
        ForumProtocol.ReadFlashCount(in p, 9, 0, "forum read marker");

    public static int ReadUnityMarkerCount(in PacketReader p) =>
        unchecked((ushort)p.ReadShort());

    public static void WriteFlashMarkerCount(in PacketWriter p, int count)
    {
        if (count < 0)
            throw new InvalidDataException($"Invalid forum read marker count {count}.");
        p.WriteInt(count);
    }

    public static void WriteUnityMarkerCount(in PacketWriter p, int count)
    {
        if (count is < 0 or > ushort.MaxValue)
            throw new InvalidDataException($"Unity forum read marker count {count} exceeds {ushort.MaxValue}.");
        p.WriteShort(unchecked((short)(ushort)count));
    }
}
