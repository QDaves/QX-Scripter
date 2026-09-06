using Qx.Messages;

namespace Qx.Model.Wired;

internal static class WiredIo
{
    public static int[] IntArray(in PacketReader p) => p.ReadIntArray();

    public static string[] StringArray(in PacketReader p) => p.ReadStringArray();

    public static void WriteIntArray(in PacketWriter p, IReadOnlyList<int> a) => p.WriteIntArray(a);

    public static void WriteStringArray(in PacketWriter p, IReadOnlyList<string> a) => p.WriteStringArray(a);
}

internal static class WiredWire
{
    public static int FlashId(Id value) => checked((int)(long)value);

    public static MessageWireProfile RequireUnityConfigurationProfile(in PacketReader p) =>
        RequireUnityConfigurationProfile(p.Context?.WireProfile);

    public static MessageWireProfile RequireUnityConfigurationProfile(in PacketWriter p) =>
        RequireUnityConfigurationProfile(p.Context?.WireProfile);

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    public static void RequireFlashCount(int count, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
    }

    public static void RequireBoundedCount(
        int count,
        int available,
        int minimum_bytes,
        string name)
    {
        RequireFlashCount(count, name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_bytes);
        if (count > available / minimum_bytes)
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
    }

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return Array.AsReadOnly(copy);
    }

    private static MessageWireProfile RequireUnityConfigurationProfile(MessageWireProfile? profile)
    {
        if (profile is not MessageWireProfile value)
            throw new NotSupportedException("The Unity wired configuration has no wire-profile context.");
        if (!value.IsAnalyzed)
            throw new WireProfilePendingException("Unity wired configuration");
        if (!value.IsExact)
            throw new NotSupportedException("The active Unity session has no compatible wired configuration layout.");
        return value;
    }
}
