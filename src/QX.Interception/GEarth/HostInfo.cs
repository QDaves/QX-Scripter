namespace Qx.Interception.GEarth;

public sealed class HostInfo
{
    public string PacketLogger { get; set; } = "";
    public string Version { get; set; } = "";
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static HostInfo Read(ref GControlReader reader)
    {
        var info = new HostInfo
        {
            PacketLogger = reader.ReadString(),
            Version = reader.ReadString()
        };

        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            info.Attributes[key] = value;
        }

        return info;
    }
}
