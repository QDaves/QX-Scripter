using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the <c>PlaceBot</c> message, sent to place a bot from the inventory in the room.</summary>
/// <param name="BotId">The identifier of the bot.</param>
/// <param name="X">The x coordinate of the target tile.</param>
/// <param name="Y">The y coordinate of the target tile.</param>
public sealed record PlaceBot(Id BotId, int X, int Y) : IParserComposer<PlaceBot>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PlaceBot Parse(in PacketReader p)
    {
        return new PlaceBot(p.ReadId(), p.ReadInt(), p.ReadInt());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(BotId);
        p.WriteInt(X);
        p.WriteInt(Y);
    }
}

/// <summary>Represents the <c>GetBotInventory</c> message, sent to request the user's bot inventory.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record GetBotInventory : IParserComposer<GetBotInventory>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetBotInventory Parse(in PacketReader p) => new GetBotInventory();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}

/// <summary>Represents the <c>CommandBot</c> message, sent to run a command on a bot.</summary>
/// <param name="BotId">The identifier of the bot.</param>
/// <param name="CommandId">The identifier of the command.</param>
/// <param name="Data">The data sent with the command.</param>
public sealed record CommandBot(Id BotId, int CommandId, string Data) : IParserComposer<CommandBot>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CommandBot Parse(in PacketReader p)
    {
        return new CommandBot(p.ReadId(), p.ReadInt(), p.ReadString());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(BotId);
        p.WriteInt(CommandId);
        p.WriteString(Data);
    }
}

/// <summary>Represents the <c>RemoveBotFromFlat</c> message, sent to remove a bot from the room.</summary>
/// <param name="BotId">The identifier of the bot.</param>
public sealed record RemoveBotFromFlat(Id BotId) : IParserComposer<RemoveBotFromFlat>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RemoveBotFromFlat Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RemoveBotFromFlat ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RemoveBotFromFlat value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.BotId));
}

/// <summary>
/// Represents the <c>GetBotCommandConfigurationData</c> message, sent to request the stored data of a bot command.
/// </summary>
/// <param name="BotId">The identifier of the bot.</param>
/// <param name="CommandId">The identifier of the command.</param>
public sealed record GetBotCommandConfigurationData(Id BotId, int CommandId)
    : IParserComposer<GetBotCommandConfigurationData>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetBotCommandConfigurationData Parse(in PacketReader p)
    {
        return new GetBotCommandConfigurationData(p.ReadId(), p.ReadInt());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(BotId);
        p.WriteInt(CommandId);
    }
}
