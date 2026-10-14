using System.Globalization;
using Qx.Messages;
using Qx.Model.Wired;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents how one wired variable is drawn above avatars or furni, as the Fx bar configuration of the room defines it.</summary>
/// <remarks><see cref="Extra"/> carries the renderer settings, among them <c>icon</c>.</remarks>
/// <param name="ConfigId">The ID of the configuration.</param>
/// <param name="IsUserFx">Whether the configuration applies to avatars rather than furni.</param>
/// <param name="ShowMode">The show mode code as sent by the server.</param>
/// <param name="VisibilityMask">The visibility mask as sent by the server.</param>
/// <param name="ShowOnMouseHover">Whether the value is shown when the mouse hovers over the object.</param>
/// <param name="ShowDuration">The show duration as sent by the server.</param>
/// <param name="CategoryId">The category ID as sent by the server.</param>
/// <param name="StyleId">The style ID as sent by the server.</param>
/// <param name="ColorId">The color ID as sent by the server.</param>
/// <param name="WidthId">The width ID as sent by the server.</param>
/// <param name="RendererId">The renderer ID as sent by the server.</param>
/// <param name="DefaultMinValue">The default lower bound of the value, used when a status sends none.</param>
/// <param name="DefaultMaxValue">The default upper bound of the value, used when a status sends none.</param>
/// <param name="Extra">Additional renderer settings as key and value pairs, such as <c>icon</c>.</param>
public sealed record VariableFxConfigEntry(
    int ConfigId,
    bool IsUserFx,
    int ShowMode,
    int VisibilityMask,
    bool ShowOnMouseHover,
    int ShowDuration,
    int CategoryId,
    int StyleId,
    int ColorId,
    int WidthId,
    int RendererId,
    long DefaultMinValue,
    long DefaultMaxValue,
    IReadOnlyDictionary<string, string> Extra)
{
    /// <summary>Gets the icon the room gave this variable, such as <c>gold</c> or <c>ranch.tomato</c>, or <see langword="null"/> when <see cref="Extra"/> has no <c>icon</c> entry.</summary>
    public string? Icon => Extra.GetValueOrDefault("icon");

    public int StackOrder => int.TryParse(Extra.GetValueOrDefault("stack_order"),
        NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) ? order : 0;

    internal static VariableFxConfigEntry Parse(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadLong(),
        p.ReadLong(),
        VariableFxWire.ReadExtra(in p));

    internal void Compose(in PacketWriter p)
    {
        p.WriteInt(ConfigId);
        p.WriteBool(IsUserFx);
        p.WriteInt(ShowMode);
        p.WriteInt(VisibilityMask);
        p.WriteBool(ShowOnMouseHover);
        p.WriteInt(ShowDuration);
        p.WriteInt(CategoryId);
        p.WriteInt(StyleId);
        p.WriteInt(ColorId);
        p.WriteInt(WidthId);
        p.WriteInt(RendererId);
        p.WriteLong(DefaultMinValue);
        p.WriteLong(DefaultMaxValue);
        VariableFxWire.WriteExtra(Extra, in p);
    }
}

/// <summary>Represents the value of one wired variable on one avatar or furni, as shown in the Fx bar.</summary>
/// <remarks>
/// The key reads <c>{configId}|{variableId}</c>. Users are identified by their room index, furni by
/// their item id. When only one of <see cref="MinValue"/> and <see cref="MaxValue"/> is set, composing
/// writes the other as 0.
/// </remarks>
/// <param name="Key">The status key, <c>{configId}|{variableId}</c>.</param>
/// <param name="IsInitialize">Whether the value is restated on entry rather than changed.</param>
/// <param name="IsUserEntity">Whether the value belongs to an avatar rather than a furni.</param>
/// <param name="EntityId">The avatar's room index, or the furni's item id.</param>
/// <param name="Value">The current value.</param>
/// <param name="MinValue">The lower bound the server set for this value, or <see langword="null"/> when none was sent.</param>
/// <param name="MaxValue">The upper bound the server set for this value, or <see langword="null"/> when none was sent.</param>
/// <param name="Extra">Additional renderer data sent with the value, such as <c>current_level</c>.</param>
public sealed record VariableFxStatusEntry(
    string Key,
    bool IsInitialize,
    bool IsUserEntity,
    int EntityId,
    long Value,
    long? MinValue,
    long? MaxValue,
    IReadOnlyDictionary<string, string> Extra)
{
    /// <summary>Gets the ID of the Fx configuration the value is drawn with, read from <see cref="Key"/>.</summary>
    public int ConfigId => VariableFxSlot.ConfigIdOf(Key);

    /// <summary>Gets the ID of the wired variable the value belongs to, read from <see cref="Key"/>.</summary>
    public string VariableId => VariableFxSlot.VariableIdOf(Key);

    /// <summary>Gets the avatar or furni and the variable the value belongs to.</summary>
    public VariableFxSlot Slot => new(IsUserEntity, EntityId, ConfigId, VariableId);

    internal static VariableFxStatusEntry Parse(in PacketReader p)
    {
        string key = p.ReadString();
        bool initialize = p.ReadBool();
        bool is_user = p.ReadBool();
        int entity_id = p.ReadInt();
        long value = p.ReadLong();
        long? min_value = null;
        long? max_value = null;
        if (p.ReadBool())
        {
            min_value = p.ReadLong();
            max_value = p.ReadLong();
        }
        return new(key, initialize, is_user, entity_id, value, min_value, max_value, VariableFxWire.ReadExtra(in p));
    }

    internal void Compose(in PacketWriter p)
    {
        p.WriteString(Key);
        p.WriteBool(IsInitialize);
        p.WriteBool(IsUserEntity);
        p.WriteInt(EntityId);
        p.WriteLong(Value);
        bool has_range = MinValue is not null || MaxValue is not null;
        p.WriteBool(has_range);
        if (has_range)
        {
            p.WriteLong(MinValue ?? 0);
            p.WriteLong(MaxValue ?? 0);
        }
        VariableFxWire.WriteExtra(Extra, in p);
    }
}

/// <summary>Represents the <c>VariableFxConfigs</c> message, received with the Fx bar configurations a room adds or replaces.</summary>
/// <param name="Configs">The added or replaced configurations.</param>
public sealed record VariableFxConfigUpdate(IReadOnlyList<VariableFxConfigEntry> Configs)
    : IParserComposer<VariableFxConfigUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableFxConfigUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableFxConfigUpdate ParseFlash(in PacketReader p)
    {
        var configs = new VariableFxConfigEntry[VariableFxWire.ReadCount(in p, VariableFxWire.ConfigBytes, nameof(Configs))];
        for (int i = 0; i < configs.Length; i++)
            configs[i] = VariableFxConfigEntry.Parse(in p);
        return new(configs);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(VariableFxConfigUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Configs.Count);
        foreach (VariableFxConfigEntry config in value.Configs)
            config.Compose(in p);
    }
}

/// <summary>Represents the <c>VariableFxConfigsRemoved</c> message, received with the Fx bar configurations a room removes.</summary>
/// <param name="ConfigIds">The IDs of the removed configurations.</param>
public sealed record VariableFxConfigRemoval(IReadOnlyList<int> ConfigIds)
    : IParserComposer<VariableFxConfigRemoval>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableFxConfigRemoval Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableFxConfigRemoval ParseFlash(in PacketReader p)
    {
        var ids = new int[VariableFxWire.ReadCount(in p, sizeof(int), nameof(ConfigIds))];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = p.ReadInt();
        return new(ids);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(VariableFxConfigRemoval value, in PacketWriter p)
    {
        p.WriteInt(value.ConfigIds.Count);
        foreach (int id in value.ConfigIds)
            p.WriteInt(id);
    }
}

/// <summary>Represents the <c>VariableFxStatus</c> message, received with Fx bar values that changed.</summary>
/// <remarks>
/// A message marked <see cref="IsInitialize"/> restates values on entry rather than reporting a change,
/// which the client shows without animation.
/// </remarks>
/// <param name="IsInitialize">Whether the values are restated on entry rather than changed.</param>
/// <param name="Statuses">The values.</param>
public sealed record VariableFxStatusUpdate(bool IsInitialize, IReadOnlyList<VariableFxStatusEntry> Statuses)
    : IParserComposer<VariableFxStatusUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableFxStatusUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableFxStatusUpdate ParseFlash(in PacketReader p)
    {
        bool is_initialize = p.ReadBool();
        var statuses = new VariableFxStatusEntry[VariableFxWire.ReadCount(in p, VariableFxWire.StatusBytes, nameof(Statuses))];
        for (int i = 0; i < statuses.Length; i++)
            statuses[i] = VariableFxStatusEntry.Parse(in p);
        return new(is_initialize, statuses);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(VariableFxStatusUpdate value, in PacketWriter p)
    {
        p.WriteBool(value.IsInitialize);
        p.WriteInt(value.Statuses.Count);
        foreach (VariableFxStatusEntry status in value.Statuses)
            status.Compose(in p);
    }
}

/// <summary>Represents the <c>VariableFxStatusRemoved</c> message, received with Fx bar values that were removed.</summary>
/// <remarks>Each key reads <c>{configId}|{variableId}|{u or f}|{entityId}</c>.</remarks>
/// <param name="Keys">The removal keys.</param>
public sealed record VariableFxStatusRemoval(IReadOnlyList<string> Keys)
    : IParserComposer<VariableFxStatusRemoval>
{
    /// <summary>Gets the removed values, read from the keys the way the client reads them.</summary>
    /// <remarks>Keys that do not have four parts are skipped.</remarks>
    public IEnumerable<VariableFxSlot> Slots
    {
        get
        {
            foreach (string key in Keys)
            {
                if (VariableFxSlot.TryFromRemovalKey(key, out VariableFxSlot slot))
                    yield return slot;
            }
        }
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static VariableFxStatusRemoval Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static VariableFxStatusRemoval ParseFlash(in PacketReader p)
    {
        var keys = new string[VariableFxWire.ReadCount(in p, VariableFxWire.StringBytes, nameof(Keys))];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = p.ReadString();
        return new(keys);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(VariableFxStatusRemoval value, in PacketWriter p)
    {
        p.WriteInt(value.Keys.Count);
        foreach (string key in value.Keys)
            p.WriteString(key);
    }
}

/// <summary>Represents which avatar or furni and which wired variable one Fx bar value belongs to.</summary>
/// <remarks>This is how the client files the values of every room object.</remarks>
/// <param name="IsUserEntity">Whether the value belongs to an avatar rather than a furni.</param>
/// <param name="EntityId">The avatar's room index, or the furni's item id.</param>
/// <param name="ConfigId">The ID of the Fx configuration.</param>
/// <param name="VariableId">The ID of the wired variable.</param>
public readonly record struct VariableFxSlot(bool IsUserEntity, int EntityId, int ConfigId, string VariableId)
{
    /// <summary>Gets the config id in a status key, which is everything before the first separator.</summary>
    /// <param name="key">The status key.</param>
    /// <returns>The config id, or 0 when that part is not an integer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public static int ConfigIdOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        int separator = key.IndexOf('|');
        ReadOnlySpan<char> text = separator < 0 ? key : key.AsSpan(0, separator);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : 0;
    }

    /// <summary>Gets the variable id in a status key, which is everything after the first separator.</summary>
    /// <param name="key">The status key.</param>
    /// <returns>The variable id, or an empty string when the key has no separator.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public static string VariableIdOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        int separator = key.IndexOf('|');
        return separator < 0 ? "" : key[(separator + 1)..];
    }

    /// <summary>Attempts to read a removal key into a slot.</summary>
    /// <remarks>
    /// The config id is the part before the first separator, the entity kind and id are the parts after
    /// the last two separators, and the variable id is between them. The kind <c>u</c> marks an avatar,
    /// and an entity id that is not an integer reads as 0.
    /// </remarks>
    /// <param name="key">The removal key.</param>
    /// <param name="slot">The slot read from the key, or the default value when the key is not valid.</param>
    /// <returns><see langword="true"/> when the key has those four parts; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public static bool TryFromRemovalKey(string key, out VariableFxSlot slot)
    {
        ArgumentNullException.ThrowIfNull(key);
        slot = default;
        int first = key.IndexOf('|');
        int last = key.LastIndexOf('|');
        int kind = last > 0 ? key.LastIndexOf('|', last - 1) : -1;
        if (first < 0 || kind <= first)
            return false;
        slot = new(
            key.AsSpan(kind + 1, last - kind - 1).SequenceEqual("u"),
            int.TryParse(key.AsSpan(last + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int entity) ? entity : 0,
            ConfigIdOf(key[..first]),
            key[(first + 1)..kind]);
        return true;
    }
}

static class VariableFxWire
{
    public const int StringBytes = sizeof(short);
    public const int ExtraBytes = StringBytes * 2;
    public const int ConfigBytes = sizeof(int) * 9 + sizeof(bool) * 2 + sizeof(long) * 2 + sizeof(int);
    public const int StatusBytes = StringBytes + sizeof(bool) * 3 + sizeof(int) + sizeof(long) + sizeof(int);

    public static int ReadCount(in PacketReader p, int minimum_bytes, string name)
    {
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, minimum_bytes, name);
        return count;
    }

    public static IReadOnlyDictionary<string, string> ReadExtra(in PacketReader p)
    {
        int count = ReadCount(in p, ExtraBytes, nameof(VariableFxStatusEntry.Extra));
        var extra = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string key = p.ReadString();
            extra[key] = p.ReadString();
        }
        return extra;
    }

    public static void WriteExtra(IReadOnlyDictionary<string, string> extra, in PacketWriter p)
    {
        p.WriteInt(extra.Count);
        foreach ((string key, string value) in extra)
        {
            p.WriteString(key);
            p.WriteString(value);
        }
    }
}
