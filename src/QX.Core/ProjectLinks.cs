namespace Qx;

public static class ProjectLinks
{
    public const string Repository = "QDaves/QX-Scripter";
    public static Uri Releases { get; } = new($"https://github.com/{Repository}/releases");
    public static Uri ReleaseApi { get; } = new($"https://api.github.com/repos/{Repository}/releases?per_page=100");
    public static Uri NewIssue { get; } = new($"https://github.com/{Repository}/issues/new");

    public static Uri Release(string tag) => new($"{Releases.AbsoluteUri}/tag/{Uri.EscapeDataString(tag)}");
}
