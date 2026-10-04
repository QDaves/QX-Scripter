namespace Qx.Presentation;

/// <summary>Provides the project's GitHub links.</summary>
public static class ProjectLinks
{
    /// <summary>The GitHub repository in <c>owner/name</c> form.</summary>
    public const string Repository = "QDaves/QX-Scripter";
    /// <summary>Gets the URL of the repository's releases page.</summary>
    public static Uri Releases { get; } = new($"https://github.com/{Repository}/releases");
    /// <summary>Gets the GitHub API URL that lists the repository's releases, up to 100 per page.</summary>
    public static Uri ReleaseApi { get; } = new($"https://api.github.com/repos/{Repository}/releases?per_page=100");
    /// <summary>Gets the URL that opens a new issue in the repository.</summary>
    public static Uri NewIssue { get; } = new($"https://github.com/{Repository}/issues/new");

    /// <summary>Gets the URL of the release page for a tag.</summary>
    /// <param name="tag">The release tag, which is URL-escaped.</param>
    /// <returns>The release page URL.</returns>
    public static Uri Release(string tag) => new($"{Releases.AbsoluteUri}/tag/{Uri.EscapeDataString(tag)}");
}
