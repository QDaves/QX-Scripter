using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Announces that a user completed an in-client name change.
/// </summary>
/// <param name="WebId">
/// The account id of the renamed user. <c>SessionDataManager.onUserNameChange</c> compares this
/// field against its own user id to decide whether the rename applies to the local user.
/// </param>
/// <param name="Index">
/// The room index of the renamed avatar. <c>RoomUsersHandler.updateNameByIndex</c> keys the in-room
/// name patch off this field, not off <paramref name="WebId"/>.
/// </param>
/// <param name="NewName">The name the user carries from now on.</param>
/// <remarks>
/// Flash reads the first field as an integer; Unity reads it as a 64-bit id, so it is read through
/// <see cref="PacketReader.ReadId"/> (Unity schema 572 is <c>[Id, Int32, String]</c>).
/// </remarks>
public sealed record UserNameChanged(Id WebId, int Index, string NewName)
    : IParserComposer<UserNameChanged>
{
    /// <summary>Reads the message from the packet.</summary>
    public static UserNameChanged Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserNameChanged ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt(), p.ReadString());

    private static UserNameChanged ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt(), p.ReadString());

    /// <summary>Writes the message to the packet.</summary>
    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserNameChanged value, in PacketWriter p)
    {
        p.WriteId(value.WebId);
        p.WriteInt(value.Index);
        p.WriteString(value.NewName);
    }

    private static void ComposeUnity(UserNameChanged value, in PacketWriter p)
    {
        p.WriteId(value.WebId);
        p.WriteInt(value.Index);
        p.WriteString(value.NewName);
    }
}
