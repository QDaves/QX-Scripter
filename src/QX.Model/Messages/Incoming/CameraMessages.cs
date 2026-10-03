using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>CameraStorageUrl</c> message, received with the storage URL of a camera photo.</summary>
/// <param name="Url">The URL of the stored photo.</param>
public sealed record CameraStorageUrl(string Url) : IParserComposer<CameraStorageUrl>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CameraStorageUrl Parse(in PacketReader p)
    {
        return new CameraStorageUrl(p.ReadString());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteString(Url);
    }
}

/// <summary>
/// Represents the <c>CameraPublishStatus</c> message, received with the result of publishing a camera photo.
/// </summary>
/// <param name="IsOk">Whether the photo was published.</param>
/// <param name="SecondsToWait">The number of seconds to wait before publishing again.</param>
/// <param name="ExtraDataId">
/// The extra data identifier of the published photo, or <see langword="null"/> when the publish failed or the
/// message does not carry one.
/// </param>
public sealed record CameraPublishStatus(bool IsOk, int SecondsToWait, string? ExtraDataId)
    : IParserComposer<CameraPublishStatus>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CameraPublishStatus Parse(in PacketReader p)
    {
        bool is_ok = p.ReadBool();
        int seconds_to_wait = p.ReadInt();
        string? extra_data_id = is_ok && p.Available > 0 ? p.ReadString() : null;
        return new CameraPublishStatus(is_ok, seconds_to_wait, extra_data_id);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when <see cref="IsOk"/> is <see langword="false"/> and <see cref="ExtraDataId"/> is set.
    /// </exception>
    public void Compose(in PacketWriter p)
    {
        if (!IsOk && ExtraDataId is not null)
            throw new InvalidDataException("Failed camera publish status cannot contain an extra data id.");
        p.WriteBool(IsOk);
        p.WriteInt(SecondsToWait);
        if (ExtraDataId is not null)
            p.WriteString(ExtraDataId);
    }
}

/// <summary>Represents the <c>CameraPurchaseOk</c> message, received when a camera photo purchase succeeds.</summary>
/// <remarks>The message has no payload.</remarks>
public sealed record CameraPurchaseOk : IParserComposer<CameraPurchaseOk>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CameraPurchaseOk Parse(in PacketReader p) => new CameraPurchaseOk();

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
    }
}

/// <summary>Represents the <c>InitCamera</c> message, received with the prices of the camera.</summary>
/// <param name="CreditPrice">The price of a photo in credits.</param>
/// <param name="DucketPrice">The price of a photo in duckets.</param>
/// <param name="PublishDucketPrice">
/// The price of publishing a photo in duckets, or <see langword="null"/> when the message does not carry it.
/// </param>
public sealed record InitCamera(int CreditPrice, int DucketPrice, int? PublishDucketPrice)
    : IParserComposer<InitCamera>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static InitCamera Parse(in PacketReader p)
    {
        int credit_price = p.ReadInt();
        int ducket_price = p.ReadInt();
        int? publish_ducket_price = p.Available > 0 ? p.ReadInt() : null;
        return new InitCamera(credit_price, ducket_price, publish_ducket_price);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(CreditPrice);
        p.WriteInt(DucketPrice);
        if (PublishDucketPrice is int publish_ducket_price)
            p.WriteInt(publish_ducket_price);
    }
}
