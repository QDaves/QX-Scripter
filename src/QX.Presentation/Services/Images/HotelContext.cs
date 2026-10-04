namespace Qx.Presentation.Services.Images;

public sealed class HotelContext
{
    public const string DefaultWebHost = "www.habbo.com";

    string _web_host = DefaultWebHost;

    public string WebHost => Volatile.Read(ref _web_host);

    public event Action<string>? WebHostChanged;

    public void Use(string? webHost)
    {
        string next = string.IsNullOrWhiteSpace(webHost) ? DefaultWebHost : webHost.Trim();
        if (string.Equals(Interlocked.Exchange(ref _web_host, next), next, StringComparison.OrdinalIgnoreCase))
            return;
        WebHostChanged?.Invoke(next);
    }
}
