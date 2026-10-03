using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a category on the local user's friend list.</summary>
/// <param name="Id">The category identifier, matched by <see cref="Friend.CategoryId"/>.</param>
/// <param name="Name">The category name.</param>
public sealed record FriendCategory(Id Id, string Name) : IParserComposer<FriendCategory>
{
    /// <summary>Reads a friend category from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static FriendCategory Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendCategory ParseFlash(in PacketReader p) => new(p.ReadId(), p.ReadString());

    /// <summary>Writes the friend category to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendCategory value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }
}
