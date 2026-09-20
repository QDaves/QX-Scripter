namespace Qx;

[Flags]
public enum ClientType
{
    None = 0,
    Flash = 2,
    All = Flash
}

public static class ClientTypes
{
    public static bool IsSupported(ClientType client) =>
        IsFlash(client);
    public static bool IsFlash(ClientType client) => client is ClientType.Flash;
}
