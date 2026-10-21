using Qx.Messages;

namespace Qx.Model.Wired;

/// <summary>Identifies the WIRED Link operation reported by <see cref="WiredLinkOperationResult"/>.</summary>
public enum WiredLinkOperation
{
    /// <summary>Saves the Link's network connection and own-room receiving setting.</summary>
    SaveConfiguration = 1,
    /// <summary>Creates a network owned by the account.</summary>
    CreateNetwork = 2,
    /// <summary>Renames a network owned by the account.</summary>
    RenameNetwork = 3,
    /// <summary>Deletes a network and disconnects its Links in every room.</summary>
    DeleteNetwork = 4
}

/// <summary>The result code of a WIRED Link operation, matching the client texts wiredlink.error.1..8.</summary>
public enum WiredLinkResultCode
{
    /// <summary>The operation succeeded.</summary>
    Success = 0,
    /// <summary>Only the furniture owner can configure this Link.</summary>
    NotFurnitureOwner = 1,
    /// <summary>The network no longer exists or does not belong to the account.</summary>
    NetworkNotFound = 2,
    /// <summary>The network name is empty, whitespace or longer than 255 characters.</summary>
    InvalidName = 3,
    /// <summary>The account already has a network with that exact name.</summary>
    DuplicateName = 4,
    /// <summary>Another Link in this room is already connected to the network.</summary>
    NetworkUsedInRoom = 5,
    /// <summary>The network identifier is invalid.</summary>
    InvalidNetworkId = 6,
    /// <summary>The Link is no longer available in the room.</summary>
    LinkUnavailable = 7,
    /// <summary>The account reached its network limit.</summary>
    NetworkLimitReached = 8
}

/// <summary>Constants of the WIRED Link network protocol.</summary>
public static class WiredLinkNetworks
{
    /// <summary>The network identifier of a disconnected Link.</summary>
    public const string Disconnected = "0";

    /// <summary>The maximum network name length accepted by the hotel.</summary>
    public const int MaximumNameLength = 255;

    internal static void RequireName(string name, string parameter, in PacketWriter p)
    {
        WiredWire.RequireString(name, parameter, in p);
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength)
            throw new ArgumentException($"A network name needs visible text and at most {MaximumNameLength} characters.", parameter);
    }

    internal static void RequireNetworkId(string network_id, string parameter, in PacketWriter p)
    {
        WiredWire.RequireString(network_id, parameter, in p);
        if (network_id.Length == 0)
            throw new ArgumentException("A network identifier is required; use WiredLinkNetworks.Disconnected to disconnect.", parameter);
    }
}

/// <summary>A network owned by the account, as listed for one Link.</summary>
/// <param name="Id">The network identifier, a decimal string assigned by the hotel.</param>
/// <param name="Name">The exact network name.</param>
/// <param name="ConnectionCount">The number of Links connected to the network, including unloaded rooms.</param>
/// <param name="LinkedInCurrentRoom">Whether a Link in the current room is connected to the network.</param>
public sealed record WiredLinkNetwork(string Id, string Name, int ConnectionCount, bool LinkedInCurrentRoom)
{
    internal static WiredLinkNetwork Parse(in PacketReader p) =>
        new(p.ReadString(), p.ReadString(), p.ReadInt(), p.ReadBool());

    internal static IReadOnlyList<WiredLinkNetwork> ParseList(in PacketReader p, string name)
    {
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, 9, name);
        var networks = new WiredLinkNetwork[count];
        for (int i = 0; i < count; i++)
            networks[i] = Parse(in p);
        return Array.AsReadOnly(networks);
    }

    internal static void ComposeList(IReadOnlyList<WiredLinkNetwork> networks, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(networks, name);
        p.WriteInt(networks.Count);
        foreach (WiredLinkNetwork network in networks)
        {
            ArgumentNullException.ThrowIfNull(network, name);
            WiredWire.RequireString(network.Id, name, in p);
            WiredWire.RequireString(network.Name, name, in p);
            p.WriteString(network.Id);
            p.WriteString(network.Name);
            p.WriteInt(network.ConnectionCount);
            p.WriteBool(network.LinkedInCurrentRoom);
        }
    }
}

/// <summary>A Link connected to a network, possibly in an unloaded room.</summary>
/// <param name="RoomId">The room containing the Link.</param>
/// <param name="RoomName">The room name.</param>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="IsBuildersClub">Whether the Link is Builders Club furniture.</param>
/// <param name="ReceiveLocal">Whether the Link also receives messages and dataframes sent from its own room.</param>
public sealed record WiredLinkConnection(Id RoomId, string RoomName, Id LinkId, bool IsBuildersClub, bool ReceiveLocal);

/// <summary>Requests the network configuration of a WIRED Link.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
public sealed record WiredLinkGetConfiguration(Id LinkId) : IParserComposer<WiredLinkGetConfiguration>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkGetConfiguration Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkGetConfiguration read(in PacketReader p) => new(p.ReadInt());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkGetConfiguration value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.LinkId));
}

/// <summary>Connects a WIRED Link to a network, or disconnects it, and sets own-room receiving.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier, or <see cref="WiredLinkNetworks.Disconnected"/>.</param>
/// <param name="ReceiveLocal">Whether the Link receives messages and dataframes sent from its own room.</param>
public sealed record WiredLinkSaveConfiguration(Id LinkId, string NetworkId, bool ReceiveLocal) : IParserComposer<WiredLinkSaveConfiguration>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkSaveConfiguration Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkSaveConfiguration read(in PacketReader p) => new(p.ReadInt(), p.ReadString(), p.ReadBool());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkSaveConfiguration value, in PacketWriter p)
    {
        WiredLinkNetworks.RequireNetworkId(value.NetworkId, nameof(NetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.NetworkId);
        p.WriteBool(value.ReceiveLocal);
    }
}

/// <summary>Creates a network owned by the account through one of its Links.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="Name">The exact network name.</param>
public sealed record WiredLinkCreateNetwork(Id LinkId, string Name) : IParserComposer<WiredLinkCreateNetwork>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkCreateNetwork Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkCreateNetwork read(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkCreateNetwork value, in PacketWriter p)
    {
        WiredLinkNetworks.RequireName(value.Name, nameof(Name), in p);
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.Name);
    }
}

/// <summary>Renames a network owned by the account. Existing connections are unchanged.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
/// <param name="Name">The new exact network name.</param>
public sealed record WiredLinkRenameNetwork(Id LinkId, string NetworkId, string Name) : IParserComposer<WiredLinkRenameNetwork>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkRenameNetwork Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkRenameNetwork read(in PacketReader p) => new(p.ReadInt(), p.ReadString(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkRenameNetwork value, in PacketWriter p)
    {
        WiredLinkNetworks.RequireNetworkId(value.NetworkId, nameof(NetworkId), in p);
        WiredLinkNetworks.RequireName(value.Name, nameof(Name), in p);
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.NetworkId);
        p.WriteString(value.Name);
    }
}

/// <summary>Deletes a network owned by the account and disconnects its Links in every room.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
public sealed record WiredLinkDeleteNetwork(Id LinkId, string NetworkId) : IParserComposer<WiredLinkDeleteNetwork>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkDeleteNetwork Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkDeleteNetwork read(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkDeleteNetwork value, in PacketWriter p)
    {
        WiredLinkNetworks.RequireNetworkId(value.NetworkId, nameof(NetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.NetworkId);
    }
}

/// <summary>Requests the Links connected to a network, including unloaded rooms.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
public sealed record WiredLinkGetConnections(Id LinkId, string NetworkId) : IParserComposer<WiredLinkGetConnections>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkGetConnections Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkGetConnections read(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkGetConnections value, in PacketWriter p)
    {
        WiredLinkNetworks.RequireNetworkId(value.NetworkId, nameof(NetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.NetworkId);
    }
}

/// <summary>Received when the hotel asks the client to open the configuration of a WIRED Link.</summary>
/// <param name="LinkId">The Link furniture ID.</param>
public sealed record WiredLinkOpen(Id LinkId) : IParserComposer<WiredLinkOpen>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkOpen Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkOpen read(in PacketReader p) => new(p.ReadInt());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkOpen value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.LinkId));
}

/// <summary>The network configuration of a WIRED Link and every network of the account.</summary>
/// <param name="RoomId">The room containing the Link.</param>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="SelectedNetworkId">The connected network, or <see cref="WiredLinkNetworks.Disconnected"/>.</param>
/// <param name="ReceiveLocal">Whether the Link receives messages and dataframes sent from its own room.</param>
/// <param name="Networks">Every network owned by the account.</param>
public sealed record WiredLinkConfiguration(Id RoomId, Id LinkId, string SelectedNetworkId, bool ReceiveLocal,
    IReadOnlyList<WiredLinkNetwork> Networks) : IParserComposer<WiredLinkConfiguration>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkConfiguration Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkConfiguration read(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadBool(), WiredLinkNetwork.ParseList(in p, nameof(Networks)));

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkConfiguration value, in PacketWriter p)
    {
        WiredWire.RequireString(value.SelectedNetworkId, nameof(SelectedNetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.RoomId));
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.SelectedNetworkId);
        p.WriteBool(value.ReceiveLocal);
        WiredLinkNetwork.ComposeList(value.Networks, nameof(Networks), in p);
    }
}

/// <summary>The result of a WIRED Link operation.</summary>
/// <remarks>Networks holds only the networks changed by this operation, not the full account list. A failed result carries no configuration; its selected network is "0" and own-room receiving is false.</remarks>
/// <param name="RoomId">The room containing the Link.</param>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="Operation">The operation that produced the result.</param>
/// <param name="ResultCode">The result code.</param>
/// <param name="HasConfiguration">Whether the result carries the Link's current configuration.</param>
/// <param name="SelectedNetworkId">The connected network, or "0" without configuration.</param>
/// <param name="ReceiveLocal">Whether the Link receives own-room payloads, or false without configuration.</param>
/// <param name="RemovedNetworkId">The deleted network, or "0".</param>
/// <param name="Networks">The networks created, renamed or connected by this operation.</param>
public sealed record WiredLinkOperationResult(Id RoomId, Id LinkId, WiredLinkOperation Operation, WiredLinkResultCode ResultCode,
    bool HasConfiguration, string SelectedNetworkId, bool ReceiveLocal, string RemovedNetworkId,
    IReadOnlyList<WiredLinkNetwork> Networks) : IParserComposer<WiredLinkOperationResult>
{
    /// <summary>Gets whether the operation succeeded.</summary>
    public bool Succeeded => ResultCode == WiredLinkResultCode.Success;

    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkOperationResult Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkOperationResult read(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        Id link_id = p.ReadInt();
        var operation = (WiredLinkOperation)p.ReadInt();
        var result = (WiredLinkResultCode)p.ReadInt();
        bool has_configuration = p.ReadBool();
        string selected = has_configuration ? p.ReadString() : WiredLinkNetworks.Disconnected;
        bool receive_local = has_configuration && p.ReadBool();
        string removed = p.ReadString();
        return new(room_id, link_id, operation, result, has_configuration, selected, receive_local, removed,
            WiredLinkNetwork.ParseList(in p, nameof(Networks)));
    }

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkOperationResult value, in PacketWriter p)
    {
        WiredWire.RequireString(value.SelectedNetworkId, nameof(SelectedNetworkId), in p);
        WiredWire.RequireString(value.RemovedNetworkId, nameof(RemovedNetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.RoomId));
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteInt((int)value.Operation);
        p.WriteInt((int)value.ResultCode);
        p.WriteBool(value.HasConfiguration);
        if (value.HasConfiguration)
        {
            p.WriteString(value.SelectedNetworkId);
            p.WriteBool(value.ReceiveLocal);
        }
        p.WriteString(value.RemovedNetworkId);
        WiredLinkNetwork.ComposeList(value.Networks, nameof(Networks), in p);
    }
}

/// <summary>The Links connected to a network, including saved connections in unloaded rooms.</summary>
/// <param name="RoomId">The room containing the requesting Link.</param>
/// <param name="LinkId">The requesting Link furniture ID.</param>
/// <param name="NetworkId">The network identifier.</param>
/// <param name="Connections">The connected Links.</param>
public sealed record WiredLinkConnections(Id RoomId, Id LinkId, string NetworkId,
    IReadOnlyList<WiredLinkConnection> Connections) : IParserComposer<WiredLinkConnections>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkConnections Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkConnections read(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        Id link_id = p.ReadInt();
        string network_id = p.ReadString();
        int count = p.ReadInt();
        WiredWire.RequireBoundedCount(count, p.Available, 12, nameof(Connections));
        var connections = new WiredLinkConnection[count];
        for (int i = 0; i < count; i++)
            connections[i] = new(p.ReadInt(), p.ReadString(), p.ReadInt(), p.ReadBool(), p.ReadBool());
        return new(room_id, link_id, network_id, Array.AsReadOnly(connections));
    }

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkConnections value, in PacketWriter p)
    {
        WiredWire.RequireString(value.NetworkId, nameof(NetworkId), in p);
        ArgumentNullException.ThrowIfNull(value.Connections, nameof(Connections));
        p.WriteInt(WiredWire.FlashId(value.RoomId));
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.NetworkId);
        p.WriteInt(value.Connections.Count);
        foreach (WiredLinkConnection connection in value.Connections)
        {
            ArgumentNullException.ThrowIfNull(connection, nameof(Connections));
            WiredWire.RequireString(connection.RoomName, nameof(Connections), in p);
            p.WriteInt(WiredWire.FlashId(connection.RoomId));
            p.WriteString(connection.RoomName);
            p.WriteInt(WiredWire.FlashId(connection.LinkId));
            p.WriteBool(connection.IsBuildersClub);
            p.WriteBool(connection.ReceiveLocal);
        }
    }
}

/// <summary>Received when a network used by a Link was deleted; the Link is disconnected.</summary>
/// <param name="RoomId">The room containing the Link.</param>
/// <param name="LinkId">The Link furniture ID.</param>
/// <param name="DeletedNetworkId">The deleted network identifier.</param>
public sealed record WiredLinkConfigurationInvalidated(Id RoomId, Id LinkId, string DeletedNetworkId)
    : IParserComposer<WiredLinkConfigurationInvalidated>
{
    /// <summary>Parses the message.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLinkConfigurationInvalidated Parse(in PacketReader p) => FlashWire.Parse(in p, read);

    private static WiredLinkConfigurationInvalidated read(in PacketReader p) => new(p.ReadInt(), p.ReadInt(), p.ReadString());

    /// <summary>Writes the message.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(WiredLinkConfigurationInvalidated value, in PacketWriter p)
    {
        WiredWire.RequireString(value.DeletedNetworkId, nameof(DeletedNetworkId), in p);
        p.WriteInt(WiredWire.FlashId(value.RoomId));
        p.WriteInt(WiredWire.FlashId(value.LinkId));
        p.WriteString(value.DeletedNetworkId);
    }
}
