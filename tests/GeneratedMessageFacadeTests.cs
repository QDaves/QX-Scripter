using System.Diagnostics;
using Xunit;

namespace QX.Tests;

public sealed class GeneratedMessageFacadeTests
{
    private static readonly string Repository = FindRepository();

    [Fact]
    public void generated_facade_and_manifest_have_canonical_encodings()
    {
        byte[] manifest = File.ReadAllBytes(Path.Combine(
            Repository,
            "src",
            "QX.Protocol",
            "Resources",
            "messages.ini"));
        byte[] facade = File.ReadAllBytes(Path.Combine(
            Repository,
            "src",
            "QX.Protocol",
            "Msg.cs"));

        AssertBom(manifest);
        Assert.DoesNotContain((byte)'\r', manifest);
        Assert.Equal((byte)'\n', manifest[^1]);
        AssertBom(facade);
        Assert.Equal((byte)'\r', facade[^2]);
        Assert.Equal((byte)'\n', facade[^1]);
        for (int index = 3; index < facade.Length; index++)
        {
            if (facade[index] == (byte)'\n')
                Assert.Equal((byte)'\r', facade[index - 1]);
            if (facade[index] == (byte)'\r')
            {
                Assert.True(index + 1 < facade.Length);
                Assert.Equal((byte)'\n', facade[index + 1]);
            }
        }
    }

    [Fact]
    public async Task generated_facade_matches_the_manifest()
    {
        string script = Path.Combine(Repository, "tools", "generate-message-facade.ps1");
        var start = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-Check");

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The message facade check could not be started.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(true);
            throw new TimeoutException("The message facade check did not finish within 30 seconds.");
        }

        Assert.True(process.ExitCode == 0, await output + await error);
    }

    private static void AssertBom(byte[] content)
    {
        Assert.True(content.Length >= 3);
        Assert.Equal((byte)0xEF, content[0]);
        Assert.Equal((byte)0xBB, content[1]);
        Assert.Equal((byte)0xBF, content[2]);
    }

    private static string FindRepository()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QX.slnx")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("The repository root could not be found.");
    }
}
