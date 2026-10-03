using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the outgoing <c>AcceptQuest</c> message, sent to accept a quest.</summary>
/// <param name="QuestId">The ID of the quest, written as a 32 bit integer.</param>
public sealed record AcceptQuest(Id QuestId) : IParserComposer<AcceptQuest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AcceptQuest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AcceptQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static AcceptQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(AcceptQuest));
        return new AcceptQuest(quest_id);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AcceptQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(AcceptQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

/// <summary>Represents the outgoing <c>ActivateQuest</c> message, sent to activate a quest.</summary>
/// <param name="QuestId">The ID of the quest, written as a 32 bit integer.</param>
public sealed record ActivateQuest(Id QuestId) : IParserComposer<ActivateQuest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ActivateQuest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ActivateQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static ActivateQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(ActivateQuest));
        return new ActivateQuest(quest_id);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ActivateQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(ActivateQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

/// <summary>Represents the outgoing <c>RejectQuest</c> message, sent to reject a quest.</summary>
/// <param name="QuestId">The ID of the quest, written as a 32 bit integer.</param>
public sealed record RejectQuest(Id QuestId) : IParserComposer<RejectQuest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RejectQuest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RejectQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static RejectQuest ParseRoot(in PacketReader p)
    {
        Id quest_id = QuestWire.ReadId(in p, 0, nameof(QuestId));
        QuestWire.RequireEmpty(in p, nameof(RejectQuest));
        return new RejectQuest(quest_id);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RejectQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(RejectQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestWire.RequireId(value.QuestId);
        QuestWire.WriteId(value.QuestId, in p);
    }
}

/// <summary>Represents the outgoing <c>CancelQuest</c> message, sent to cancel the user's active quest.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record CancelQuest : IParserComposer<CancelQuest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CancelQuest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static CancelQuest ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(CancelQuest));
        return new CancelQuest();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CancelQuest value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(CancelQuest value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

/// <summary>Represents the outgoing <c>GetQuests</c> message, sent to request the quests available to the user.</summary>
/// <remarks>The message carries no data. The hotel answers with <see cref="Qx.Model.Messages.Incoming.Quests"/>.</remarks>
public sealed record GetQuests : IParserComposer<GetQuests>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetQuests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetQuests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetQuests ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(GetQuests));
        return new GetQuests();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(GetQuests value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

/// <summary>Represents the outgoing <c>GetDailyQuest</c> message, sent to request a daily quest.</summary>
/// <remarks>The hotel answers with <see cref="QuestDaily"/>.</remarks>
/// <param name="IsEasy">Whether to request a quest from the easy pool instead of the hard pool.</param>
/// <param name="Index">The index of the quest within the selected pool.</param>
public sealed record GetDailyQuest(
    bool IsEasy,
    int Index) : IParserComposer<GetDailyQuest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetDailyQuest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetDailyQuest ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetDailyQuest ParseRoot(in PacketReader p)
    {
        QuestWire.RequireRemaining(
            in p,
            sizeof(byte) + sizeof(int),
            0,
            nameof(GetDailyQuest));
        var value = new GetDailyQuest(p.ReadBool(), p.ReadInt());
        QuestWire.RequireEmpty(in p, nameof(GetDailyQuest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetDailyQuest value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(GetDailyQuest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteBool(value.IsEasy);
        p.WriteInt(value.Index);
    }
}

/// <summary>Represents the outgoing <c>GetSeasonalQuestsOnly</c> message, sent to request the seasonal quests.</summary>
/// <remarks>The message carries no data. The hotel answers with <see cref="QuestsSeasonal"/>.</remarks>
public sealed record GetSeasonalQuests : IParserComposer<GetSeasonalQuests>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetSeasonalQuests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetSeasonalQuests ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static GetSeasonalQuests ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(GetSeasonalQuests));
        return new GetSeasonalQuests();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetSeasonalQuests value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(GetSeasonalQuests value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

/// <summary>Represents the outgoing <c>OpenQuestTracker</c> message, sent to tell the hotel the quest tracker was opened.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record OpenQuestTracker : IParserComposer<OpenQuestTracker>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static OpenQuestTracker Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenQuestTracker ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static OpenQuestTracker ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(OpenQuestTracker));
        return new OpenQuestTracker();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenQuestTracker value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(OpenQuestTracker value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

/// <summary>Represents the outgoing <c>FriendRequestQuestComplete</c> message, sent to report progress on a friend request quest step.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record FriendRequestQuestComplete : IParserComposer<FriendRequestQuestComplete>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FriendRequestQuestComplete Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendRequestQuestComplete ParseFlash(in PacketReader p) =>
        ParseRoot(in p);

    private static FriendRequestQuestComplete ParseRoot(in PacketReader p)
    {
        QuestWire.RequireEmpty(in p, nameof(FriendRequestQuestComplete));
        return new FriendRequestQuestComplete();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendRequestQuestComplete value, in PacketWriter p) =>
        ComposeRoot(value);

    private static void ComposeRoot(FriendRequestQuestComplete value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}
