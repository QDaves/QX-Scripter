namespace Qx.Interception.GEarth;

public sealed class GEarthOptions
{
    public int Port { get; set; } = 9092;
    public bool SearchPorts { get; set; }
    public int PortSearchCount { get; set; } = 32;
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = Qx.ProductVersion.Current;
    public string Description { get; set; } = "";
    public bool OnClickUsed { get; set; }
    public bool CanLeave { get; set; } = true;
    public bool CanDelete { get; set; } = true;
    public string File { get; set; } = "";
    public string Cookie { get; set; } = "";
    public bool IsLaunchedByGEarth =>
        !string.IsNullOrWhiteSpace(File) &&
        !string.IsNullOrWhiteSpace(Cookie);

    public static GEarthOptions Parse(string[] args, GEarthOptions? baseOptions = null)
    {
        GEarthOptions options = baseOptions ?? new GEarthOptions();

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "-p" when int.TryParse(args[i + 1], out int port):
                    options.Port = port;
                    break;
                case "-f":
                    options.File = args[i + 1];
                    break;
                case "-c":
                    options.Cookie = args[i + 1];
                    break;
            }
        }

        return options;
    }
}
