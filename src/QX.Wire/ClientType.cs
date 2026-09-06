namespace Qx;

[Flags]
public enum ClientType
{
    None = 0,
    Unity = 1,
    Flash = 2,
    All = 3
}

public static class ClientTypes
{
    public static bool IsSupported(ClientType client) =>
        IsUnity(client) || IsFlash(client);

    public static bool IsUnity(ClientType client) => client is ClientType.Unity;
    public static bool IsFlash(ClientType client) => client is ClientType.Flash;
}
