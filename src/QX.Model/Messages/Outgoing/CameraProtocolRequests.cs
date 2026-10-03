using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>
/// Represents the <c>RequestCameraConfiguration</c> message, sent to request the camera configuration.
/// </summary>
/// <remarks>The message has no payload.</remarks>
public sealed record RequestCameraConfiguration : IParserComposer<RequestCameraConfiguration>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RequestCameraConfiguration Parse(in PacketReader p) => new RequestCameraConfiguration();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}

/// <summary>Represents the <c>PurchasePhoto</c> message, sent to buy the current camera photo.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record PurchasePhoto : IParserComposer<PurchasePhoto>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PurchasePhoto Parse(in PacketReader p) => new PurchasePhoto();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}

/// <summary>Represents the <c>PublishPhoto</c> message, sent to publish the current camera photo.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record PublishPhoto : IParserComposer<PublishPhoto>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PublishPhoto Parse(in PacketReader p) => new PublishPhoto();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}

/// <summary>Represents the <c>PhotoCompetition</c> message, sent to enter the current photo in a competition.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record PhotoCompetition : IParserComposer<PhotoCompetition>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PhotoCompetition Parse(in PacketReader p) => new PhotoCompetition();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}
