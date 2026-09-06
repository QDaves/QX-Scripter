using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CameraStorageUrl(string Url) : IParserComposer<CameraStorageUrl>
{
    public static CameraStorageUrl Parse(in PacketReader p)
    {
        RequireFlash(p.Client);
        return new CameraStorageUrl(p.ReadString());
    }

    public void Compose(in PacketWriter p)
    {
        RequireFlash(p.Client);
        p.WriteString(Url);
    }

    private static void RequireFlash(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}

public sealed record CameraPublishStatus(bool IsOk, int SecondsToWait, string? ExtraDataId)
    : IParserComposer<CameraPublishStatus>
{
    public static CameraPublishStatus Parse(in PacketReader p)
    {
        RequireFlash(p.Client);
        bool is_ok = p.ReadBool();
        int seconds_to_wait = p.ReadInt();
        string? extra_data_id = is_ok && p.Available > 0 ? p.ReadString() : null;
        return new CameraPublishStatus(is_ok, seconds_to_wait, extra_data_id);
    }

    public void Compose(in PacketWriter p)
    {
        RequireFlash(p.Client);
        if (!IsOk && ExtraDataId is not null)
            throw new InvalidDataException("Failed camera publish status cannot contain an extra data id.");
        p.WriteBool(IsOk);
        p.WriteInt(SecondsToWait);
        if (ExtraDataId is not null)
            p.WriteString(ExtraDataId);
    }

    private static void RequireFlash(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}

public sealed record CameraPurchaseOk : IParserComposer<CameraPurchaseOk>
{
    public static CameraPurchaseOk Parse(in PacketReader p)
    {
        RequireFlash(p.Client);
        return new CameraPurchaseOk();
    }

    public void Compose(in PacketWriter p) => RequireFlash(p.Client);

    private static void RequireFlash(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}

public sealed record InitCamera(int CreditPrice, int DucketPrice, int? PublishDucketPrice)
    : IParserComposer<InitCamera>
{
    public static InitCamera Parse(in PacketReader p)
    {
        RequireFlash(p.Client);
        int credit_price = p.ReadInt();
        int ducket_price = p.ReadInt();
        int? publish_ducket_price = p.Available > 0 ? p.ReadInt() : null;
        return new InitCamera(credit_price, ducket_price, publish_ducket_price);
    }

    public void Compose(in PacketWriter p)
    {
        RequireFlash(p.Client);
        p.WriteInt(CreditPrice);
        p.WriteInt(DucketPrice);
        if (PublishDucketPrice is int publish_ducket_price)
            p.WriteInt(publish_ducket_price);
    }

    private static void RequireFlash(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}

public sealed record RequestCameraConfiguration : IParserComposer<RequestCameraConfiguration>
{
    public static RequestCameraConfiguration Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new RequestCameraConfiguration();
    }

    public void Compose(in PacketWriter p) => RequireSupportedClient(p.Client);

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record PurchasePhoto : IParserComposer<PurchasePhoto>
{
    public static PurchasePhoto Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new PurchasePhoto();
    }

    public void Compose(in PacketWriter p) => RequireSupportedClient(p.Client);

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record PublishPhoto : IParserComposer<PublishPhoto>
{
    public static PublishPhoto Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new PublishPhoto();
    }

    public void Compose(in PacketWriter p) => RequireSupportedClient(p.Client);

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}

public sealed record PhotoCompetition : IParserComposer<PhotoCompetition>
{
    public static PhotoCompetition Parse(in PacketReader p)
    {
        RequireSupportedClient(p.Client);
        return new PhotoCompetition();
    }

    public void Compose(in PacketWriter p) => RequireSupportedClient(p.Client);

    private static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }
}
