using Qx.Messages;
using Qx.Model.Bots;

namespace Qx.Model.Messages.Incoming;

public sealed record BotInventory(IReadOnlyList<InventoryBot> Bots) : IParserComposer<BotInventory>
{
    public static BotInventory Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        int count = p.ReadLength();
        var bots = new InventoryBot[count];
        for (int index = 0; index < count; index++)
            bots[index] = p.Parse<InventoryBot>();
        return new BotInventory(bots);
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteLength((Length)Bots.Count);
        foreach (InventoryBot bot in Bots)
            p.Compose(bot);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record BotAddedToInventory(InventoryBot Bot, bool BoughtAsGift)
    : IParserComposer<BotAddedToInventory>
{
    public static BotAddedToInventory Parse(in PacketReader p)
    {
        if (p.Client is ClientType.Flash)
        {
            bool bought_as_gift = p.ReadBool();
            return new BotAddedToInventory(p.Parse<InventoryBot>(), bought_as_gift);
        }
        if (p.Client is ClientType.Unity)
            return new BotAddedToInventory(p.Parse<InventoryBot>(), p.ReadBool());
        throw new UnsupportedClientException(p.Client);
    }

    public void Compose(in PacketWriter p)
    {
        if (p.Client is ClientType.Flash)
        {
            p.WriteBool(BoughtAsGift);
            p.Compose(Bot);
        }
        else if (p.Client is ClientType.Unity)
        {
            p.Compose(Bot);
            p.WriteBool(BoughtAsGift);
        }
        else
        {
            throw new UnsupportedClientException(p.Client);
        }
    }
}

public sealed record BotRemovedFromInventory(int BotId) : IParserComposer<BotRemovedFromInventory>
{
    public static BotRemovedFromInventory Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new BotRemovedFromInventory(p.ReadInt());
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteInt(BotId);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record BotReceived(InventoryBot Bot, bool OpenInventory) : IParserComposer<BotReceived>
{
    public static BotReceived Parse(in PacketReader p)
    {
        if (p.Client is ClientType.Flash)
            return new BotReceived(p.Parse<InventoryBot>(), p.ReadBool());
        if (p.Client is ClientType.Unity)
        {
            bool open_inventory = p.ReadBool();
            return new BotReceived(p.Parse<InventoryBot>(), open_inventory);
        }
        throw new UnsupportedClientException(p.Client);
    }

    public void Compose(in PacketWriter p)
    {
        if (p.Client is ClientType.Flash)
        {
            p.Compose(Bot);
            p.WriteBool(OpenInventory);
        }
        else if (p.Client is ClientType.Unity)
        {
            p.WriteBool(OpenInventory);
            p.Compose(Bot);
        }
        else
        {
            throw new UnsupportedClientException(p.Client);
        }
    }
}

public sealed record BotCommandConfigurationData(int BotId, int CommandId, string Data)
    : IParserComposer<BotCommandConfigurationData>
{
    public static BotCommandConfigurationData Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new BotCommandConfigurationData(p.ReadInt(), p.ReadInt(), p.ReadString());
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteInt(BotId);
        p.WriteInt(CommandId);
        p.WriteString(Data);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record BotSkillListUpdate(int BotId, IReadOnlyList<BotSkill> Skills)
    : IParserComposer<BotSkillListUpdate>
{
    public static BotSkillListUpdate Parse(in PacketReader p)
    {
        RequireFlash(p.Client);
        int bot_id = p.ReadInt();
        int count = p.ReadInt();
        if (count < 0)
            throw new InvalidDataException($"Invalid bot skill count {count}.");
        var skills = new BotSkill[count];
        for (int index = 0; index < count; index++)
            skills[index] = p.Parse<BotSkill>();
        return new BotSkillListUpdate(bot_id, skills);
    }

    public void Compose(in PacketWriter p)
    {
        RequireFlash(p.Client);
        p.WriteInt(BotId);
        p.WriteInt(Skills.Count);
        foreach (BotSkill skill in Skills)
            p.Compose(skill);
    }

    private static void RequireFlash(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}

public sealed record PlaceBot(Id BotId, int X, int Y) : IParserComposer<PlaceBot>
{
    public static PlaceBot Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new PlaceBot(p.ReadId(), p.ReadInt(), p.ReadInt());
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteId(BotId);
        p.WriteInt(X);
        p.WriteInt(Y);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record GetBotInventory : IParserComposer<GetBotInventory>
{
    public static GetBotInventory Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new GetBotInventory();
    }

    public void Compose(in PacketWriter p) => RequireSupportedClient(p.Client);

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record CommandBot(Id BotId, int CommandId, string Data) : IParserComposer<CommandBot>
{
    public static CommandBot Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new CommandBot(p.ReadId(), p.ReadInt(), p.ReadString());
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteId(BotId);
        p.WriteInt(CommandId);
        p.WriteString(Data);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record RemoveBotFromFlat(Id BotId) : IParserComposer<RemoveBotFromFlat>
{
    public static RemoveBotFromFlat Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemoveBotFromFlat ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static RemoveBotFromFlat ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemoveBotFromFlat value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.BotId));

    private static void ComposeUnity(RemoveBotFromFlat value, in PacketWriter p) =>
        p.WriteLong(value.BotId);
}

public sealed record GetBotCommandConfigurationData(Id BotId, int CommandId)
    : IParserComposer<GetBotCommandConfigurationData>
{
    public static GetBotCommandConfigurationData Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new GetBotCommandConfigurationData(p.ReadId(), p.ReadInt());
    }

    public void Compose(in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        p.WriteId(BotId);
        p.WriteInt(CommandId);
    }

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}
