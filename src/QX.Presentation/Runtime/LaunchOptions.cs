using Qx.Interception.GEarth;

namespace Qx.Presentation.Runtime;

public sealed record LaunchOptions(GEarthOptions GEarth, bool HostedByGEarth)
{
    public static LaunchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        GEarthOptions options = GEarthOptions.Parse(args, new GEarthOptions
        {
            Title = "QX Scripter",
            Author = "QDave",
            Description = "C# scripting console for Habbo",
            OnClickUsed = true,
            Port = 9092
        });
        bool hosted = options.IsLaunchedByGEarth;
        options.SearchPorts = !hosted;
        return new LaunchOptions(options, hosted);
    }
}
