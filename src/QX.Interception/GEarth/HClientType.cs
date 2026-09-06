namespace Qx.Interception.GEarth;

public static class HClientType
{
    public static ClientType FromName(string? name)
    {
        if (!TryFromName(name, out ClientType client))
            throw new ArgumentException("The G-Earth client type is not supported.", nameof(name));
        return client;
    }

    public static bool TryFromName(string? name, out ClientType client)
    {
        if (string.Equals(name, "FLASH", StringComparison.OrdinalIgnoreCase))
        {
            client = ClientType.Flash;
            return true;
        }
        if (string.Equals(name, "UNITY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "NITRO", StringComparison.OrdinalIgnoreCase))
        {
            client = ClientType.Unity;
            return true;
        }
        client = ClientType.None;
        return false;
    }
}
