using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the <c>GetCreditsInfo</c> message, sent to request the user's credit balance.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record WalletBalanceRequest : IParserComposer<WalletBalanceRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WalletBalanceRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WalletBalanceRequest ParseFlash(in PacketReader p)
    {
        EconomyWire.RequireEmpty(in p, nameof(WalletBalanceRequest));
        return new WalletBalanceRequest();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WalletBalanceRequest value, in PacketWriter p)
    {
    }
}
