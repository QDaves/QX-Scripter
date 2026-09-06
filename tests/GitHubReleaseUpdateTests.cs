using System.Net;
using Qx.Updates;
using Xunit;

namespace QX.Tests;

public sealed class GitHubReleaseUpdateTests
{
    [Fact]
    public async Task requests_and_parses_the_latest_release_from_github()
    {
        HttpMethod? method = null;
        Uri? uri = null;
        string? accept = null;
        string? user_agent = null;
        string? api_version = null;
        using var http = new HttpClient(new ResponseHandler(request =>
        {
            method = request.Method;
            uri = request.RequestUri;
            accept = request.Headers.Accept.Single().MediaType;
            user_agent = request.Headers.UserAgent.ToString();
            api_version = request.Headers.GetValues("X-GitHub-Api-Version").Single();
            return JsonResponse(HttpStatusCode.OK,
                """
                [{
                  "tag_name": " 0.20.0 ",
                  "name": "  QX\n Scripter\t0.20\u001b  ",
                  "html_url": "https://example.invalid/not-trusted",
                  "draft": false,
                  "prerelease": false
                }]
                """);
        }));

        GitHubRelease? release = await GitHubReleaseUpdates.GetLatestAsync(http);

        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal(Qx.ProjectLinks.ReleaseApi, uri);
        Assert.Equal("application/vnd.github+json", accept);
        Assert.Equal($"QXScripter/{Qx.ProductVersion.Current}", user_agent);
        Assert.Equal("2026-03-10", api_version);
        Assert.NotNull(release);
        Assert.Equal("0.20.0", release.Tag);
        Assert.Equal("0.20.0", release.Version);
        Assert.Equal("QX Scripter 0.20", release.Name);
        Assert.Equal(Qx.ProjectLinks.Release("0.20.0"), release.Uri);
    }

    [Theory]
    [InlineData("0.1.0", "v0.2.0", true)]
    [InlineData("0.1.0.0", "v0.2.0", true)]
    [InlineData("v0.1.0", "v0.1.0", false)]
    [InlineData("0.20.0", "v0.19.9", false)]
    [InlineData("not-a-version", "v0.20.0", false)]
    [InlineData("0.1.0", "v0.2", false)]
    [InlineData("0.1.0", "v0.2.0-beta", false)]
    public void compares_release_versions_strictly(
        string installed_version,
        string release_tag,
        bool expected)
    {
        var release = new GitHubRelease(
            release_tag,
            release_tag.TrimStart('v', 'V'),
            release_tag,
            Qx.ProjectLinks.Releases);

        Assert.Equal(expected, GitHubReleaseUpdates.ShouldNotify(installed_version, null, release));
    }

    [Fact]
    public void notifies_only_once_for_each_new_release()
    {
        GitHubRelease current = Release("v0.20.0");
        GitHubRelease next = Release("v0.21.0");

        Assert.True(GitHubReleaseUpdates.ShouldNotify("0.1.0", null, current));
        Assert.False(GitHubReleaseUpdates.ShouldNotify("0.1.0", "V0.20.0", current));
        Assert.True(GitHubReleaseUpdates.ShouldNotify("0.1.0", "v0.1.0", current));
        Assert.True(GitHubReleaseUpdates.ShouldNotify("0.1.0", current.Tag, next));
        Assert.False(GitHubReleaseUpdates.ShouldNotify("0.21.0", current.Tag, next));
    }

    [Theory]
    [InlineData("v0.20")]
    [InlineData("v0.20.0.0")]
    [InlineData("v0.20.0-beta")]
    [InlineData("v00.20.0")]
    [InlineData("release-0.20.0")]
    public async Task rejects_malformed_release_tags(string tag)
    {
        using HttpClient http = JsonClient(HttpStatusCode.OK, $$"""{"tag_name":"{{tag}}"}""");

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(http));
    }

    [Fact]
    public async Task rejects_drafts()
    {
        using HttpClient http = JsonClient(
            HttpStatusCode.OK,
            """{"tag_name":"v0.20.0","draft":true}""");

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(http));
    }

    [Fact]
    public async Task selects_the_highest_published_version_including_preview_releases()
    {
        using var http = new HttpClient(new ResponseHandler(_ => JsonResponse(HttpStatusCode.OK,
            """
            [
              {"tag_name":"v0.1.2"},
              {"tag_name":"v0.1.4","draft":true},
              {"tag_name":"v0.1.3","prerelease":true},
              {"tag_name":"v0.1.1"},
              {"tag_name":"invalid"}
            ]
            """)));

        GitHubRelease? release = await GitHubReleaseUpdates.GetLatestAsync(http);

        Assert.NotNull(release);
        Assert.Equal("v0.1.3", release.Tag);
        Assert.True(GitHubReleaseUpdates.ShouldNotify("0.1.2", null, release));
    }

    [Fact]
    public async Task ignores_missing_releases_and_invalid_json()
    {
        using HttpClient missing = JsonClient(HttpStatusCode.NotFound, "{}");
        using HttpClient invalid = JsonClient(HttpStatusCode.OK, "{not-json");

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(missing));
        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(invalid));
    }

    [Fact]
    public async Task ignores_transport_failures()
    {
        using var http = new HttpClient(new FailingHandler());

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(http));
    }

    [Fact]
    public async Task ignores_io_failures()
    {
        using var http = new HttpClient(new IoFailureHandler());

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(http));
    }

    [Fact]
    public async Task propagates_cancellation()
    {
        using var http = new HttpClient(new CancellationHandler());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GitHubReleaseUpdates.GetLatestAsync(http, cancellation.Token));
    }

    [Fact]
    public async Task rejects_declared_and_streamed_responses_over_one_megabyte()
    {
        byte[] oversized = new byte[1024 * 1024 + 1];
        using var declared = new HttpClient(new ResponseHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(oversized)
            }));
        using var streamed = new HttpClient(new ResponseHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new UnknownLengthContent(oversized)
            }));

        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(declared));
        Assert.Null(await GitHubReleaseUpdates.GetLatestAsync(streamed));
    }

    private static GitHubRelease Release(string tag) => new(
        tag,
        tag.TrimStart('v', 'V'),
        tag,
        Qx.ProjectLinks.Releases);

    private static HttpClient JsonClient(HttpStatusCode status, string json) =>
        new(new ResponseHandler(_ => JsonResponse(status, $"[{json}]")));

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json)
    };

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellation_token) => Task.FromResult(response(request));
    }

    private sealed class UnknownLengthContent(byte[] content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellation_token) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
    }

    private sealed class IoFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellation_token) =>
            Task.FromException<HttpResponseMessage>(new IOException("connection interrupted"));
    }

    private sealed class CancellationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellation_token) =>
            Task.FromCanceled<HttpResponseMessage>(cancellation_token);
    }
}
