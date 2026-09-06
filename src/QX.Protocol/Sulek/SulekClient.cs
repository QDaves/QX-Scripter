namespace Qx.Protocol.Sulek;

public static class SulekClient
{
    public const string BaseUrl = "https://api.sulek.dev";

    public static async Task<SulekMessages> FetchMessagesAsync(string variant, string version, HttpClient http, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/releases/{variant}/{version}/messages";
        string json = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return SulekMessages.Parse(json);
    }
}
