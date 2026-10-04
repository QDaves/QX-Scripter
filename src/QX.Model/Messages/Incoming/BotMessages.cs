using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>BotInventory</c> message, received with the bots in the user's inventory.</summary>
/// <param name="Bots">The bots in the inventory.</param>
public sealed record BotInventory(IReadOnlyList<InventoryBot> Bots) : IParserComposer<BotInventory>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotInventory Parse(in PacketReader p)
    {
        int count = p.ReadLength();
        var bots = new InventoryBot[count];
        for (int index = 0; index < count; index++)
            bots[index] = p.Parse<InventoryBot>();
        return new BotInventory(bots);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteLength((Length)Bots.Count);
        foreach (InventoryBot bot in Bots)
            p.Compose(bot);
    }
}

/// <summary>
/// Represents the <c>BotAddedToInventory</c> message, received when a bot is added to the user's inventory.
/// </summary>
/// <param name="Bot">The added bot.</param>
/// <param name="BoughtAsGift">Whether the bot was bought as a gift, read before the bot on the wire.</param>
public sealed record BotAddedToInventory(InventoryBot Bot, bool BoughtAsGift)
    : IParserComposer<BotAddedToInventory>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotAddedToInventory Parse(in PacketReader p)
    {
        bool bought_as_gift = p.ReadBool();
        return new BotAddedToInventory(p.Parse<InventoryBot>(), bought_as_gift);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteBool(BoughtAsGift);
        p.Compose(Bot);
    }
}

/// <summary>
/// Represents the <c>BotRemovedFromInventory</c> message, received when a bot is removed from the user's inventory.
/// </summary>
/// <param name="BotId">The identifier of the removed bot.</param>
public sealed record BotRemovedFromInventory(int BotId) : IParserComposer<BotRemovedFromInventory>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotRemovedFromInventory Parse(in PacketReader p)
    {
        return new BotRemovedFromInventory(p.ReadInt());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(BotId);
    }
}

/// <summary>Represents the <c>BotReceived</c> message, received when the user receives a bot.</summary>
/// <param name="Bot">The received bot.</param>
/// <param name="OpenInventory">Whether the client should open the inventory.</param>
public sealed record BotReceived(InventoryBot Bot, bool OpenInventory) : IParserComposer<BotReceived>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotReceived Parse(in PacketReader p)
    {
        return new BotReceived(p.Parse<InventoryBot>(), p.ReadBool());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.Compose(Bot);
        p.WriteBool(OpenInventory);
    }
}

/// <summary>
/// Represents the bot command configuration message, received with the stored data of a bot command.
/// </summary>
/// <param name="BotId">The identifier of the bot.</param>
/// <param name="CommandId">The identifier of the command.</param>
/// <param name="Data">The configuration data of the command.</param>
public sealed record BotCommandConfigurationData(int BotId, int CommandId, string Data)
    : IParserComposer<BotCommandConfigurationData>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotCommandConfigurationData Parse(in PacketReader p)
    {
        return new BotCommandConfigurationData(p.ReadInt(), p.ReadInt(), p.ReadString());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(BotId);
        p.WriteInt(CommandId);
        p.WriteString(Data);
    }
}

/// <summary>Represents the <c>BotSkillListUpdate</c> message, received with the skills of a bot.</summary>
/// <param name="BotId">The identifier of the bot.</param>
/// <param name="Skills">The skills of the bot.</param>
public sealed record BotSkillListUpdate(int BotId, IReadOnlyList<BotSkill> Skills)
    : IParserComposer<BotSkillListUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BotSkillListUpdate Parse(in PacketReader p)
    {
        int bot_id = p.ReadInt();
        int count = p.ReadInt();
        if (count < 0)
            throw new InvalidDataException($"Invalid bot skill count {count}.");
        var skills = new BotSkill[count];
        for (int index = 0; index < count; index++)
            skills[index] = p.Parse<BotSkill>();
        return new BotSkillListUpdate(bot_id, skills);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(BotId);
        p.WriteInt(Skills.Count);
        foreach (BotSkill skill in Skills)
            p.Compose(skill);
    }
}
