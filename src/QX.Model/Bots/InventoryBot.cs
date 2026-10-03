using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a bot in the local user's inventory.</summary>
/// <param name="Id">The bot identifier.</param>
/// <param name="Name">The bot's name.</param>
/// <param name="Motto">The bot's motto.</param>
/// <param name="Gender">The bot's gender code as sent by the hotel.</param>
/// <param name="Figure">The bot's figure string.</param>
public sealed record InventoryBot(
    int Id,
    string Name,
    string Motto,
    string Gender,
    string Figure) : IParserComposer<InventoryBot>
{
    /// <summary>Reads an inventory bot from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static InventoryBot Parse(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());

    /// <summary>Writes the inventory bot to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Id);
        p.WriteString(Name);
        p.WriteString(Motto);
        p.WriteString(Gender);
        p.WriteString(Figure);
    }
}

/// <summary>Represents one skill of a bot with its setting.</summary>
/// <param name="Id">The skill identifier.</param>
/// <param name="Data">The skill's data string as sent by the hotel.</param>
public sealed record BotSkill(int Id, string Data) : IParserComposer<BotSkill>
{
    /// <summary>Reads a bot skill from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static BotSkill Parse(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    /// <summary>Writes the bot skill to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Id);
        p.WriteString(Data);
    }
}
