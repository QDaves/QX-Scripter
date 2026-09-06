using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Xunit;

namespace QX.Tests;

public sealed class GEarthLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task connection_reset_after_handshake_completes_and_clears_transport(bool search_ports)
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var extension = new GEarthExtension(new GEarthOptions
        {
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            SearchPorts = search_ports,
            PortSearchCount = 1
        });
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int disconnected = 0;
        extension.InterceptorConnected += () => connected.TrySetResult();
        extension.InterceptorDisconnected += () => Interlocked.Increment(ref disconnected);
        Task run = extension.RunAsync(cancellation.Token);
        using TcpClient peer = await listener.AcceptTcpClientAsync(cancellation.Token);
        await peer.GetStream().WriteAsync(new GControlWriter(GControl.Outgoing.InfoRequest).ToFrame(), cancellation.Token);
        await connected.Task.WaitAsync(cancellation.Token);
        await peer.GetStream().ReadExactlyAsync(new byte[1], cancellation.Token);
        peer.LingerState = new LingerOption(true, 0);
        peer.Client.Close(0);

        await run.WaitAsync(cancellation.Token);

        Assert.Equal(1, disconnected);
        Assert.False(extension.IsInterceptorConnected);
        Assert.False(extension.IsConnected);
        Assert.Equal(0, extension.ConnectedPort);
    }

    [Fact]
    public async Task malformed_frame_after_handshake_remains_an_error()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var extension = new GEarthExtension(new GEarthOptions
        {
            Port = ((IPEndPoint)listener.LocalEndpoint).Port
        });
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        extension.InterceptorConnected += () => connected.TrySetResult();
        Task run = extension.RunAsync(cancellation.Token);
        using TcpClient peer = await listener.AcceptTcpClientAsync(cancellation.Token);
        await peer.GetStream().WriteAsync(new GControlWriter(GControl.Outgoing.Init).ToFrame(), cancellation.Token);
        await connected.Task.WaitAsync(cancellation.Token);
        await peer.GetStream().WriteAsync(new byte[4], cancellation.Token);

        await Assert.ThrowsAsync<InvalidDataException>(() => run.WaitAsync(cancellation.Token));
        Assert.False(extension.IsInterceptorConnected);
        Assert.Equal(0, extension.ConnectedPort);
    }

    [Fact]
    public void launch_arguments_distinguish_gearth_and_standalone_lifecycles()
    {
        GEarthOptions hosted = GEarthOptions.Parse(
            ["-c", "cookie", "-p", "9097", "-f", "QX_1.0"]);
        GEarthOptions standalone = GEarthOptions.Parse([]);

        Assert.True(hosted.IsLaunchedByGEarth);
        Assert.Equal(9097, hosted.Port);
        Assert.False(standalone.IsLaunchedByGEarth);
        Assert.Equal(9092, standalone.Port);
    }

    [Fact]
    public async Task extension_info_reports_the_product_version()
    {
        string expected_version = Qx.ProductVersion.Current;
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var extension = new GEarthExtension(new GEarthOptions
        {
            Port = port,
            HandshakeTimeout = TimeSpan.FromSeconds(1),
            Title = "QX Scripter",
            Author = "QDave",
            Description = "C# scripting console for Habbo"
        });

        Task<string> reported_version = ReadExtensionVersionAsync(listener, cancellation.Token);
        Task run = extension.RunAsync(cancellation.Token);

        Assert.Equal(expected_version, await reported_version.WaitAsync(cancellation.Token));
        await run.WaitAsync(cancellation.Token);
    }

    [Fact]
    public async Task port_search_skips_non_gearth_listener_and_activates_on_next_instance()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        (TcpListener unrelated, TcpListener gearth) = ConsecutiveListeners();
        using (unrelated)
        using (gearth)
        using (var extension = new GEarthExtension(new GEarthOptions
        {
            Port = ((IPEndPoint)unrelated.LocalEndpoint).Port,
            SearchPorts = true,
            PortSearchCount = 2,
            HandshakeTimeout = TimeSpan.FromMilliseconds(250),
            Title = "QX",
            OnClickUsed = true
        }))
        {
            var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int connected_port = 0;
            extension.InterceptorConnected += () => connected_port = extension.ConnectedPort;
            extension.Activated += () => activated.TrySetResult();

            Task unrelated_server = RejectAfterClientLeavesAsync(unrelated, cancellation.Token);
            Task gearth_server = ActivateAsync(gearth, cancellation.Token);
            Task run = extension.RunAsync(cancellation.Token);

            await activated.Task.WaitAsync(cancellation.Token);
            await run.WaitAsync(cancellation.Token);
            await Task.WhenAll(unrelated_server, gearth_server).WaitAsync(cancellation.Token);
            Assert.Equal(((IPEndPoint)gearth.LocalEndpoint).Port, connected_port);
            Assert.Equal(0, extension.ConnectedPort);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task reconnecting_runtime_survives_gearth_restart_on_the_same_port(bool reset)
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string temporary = Path.Combine(Path.GetTempPath(), $"qx-gearth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);

        await using var runtime = new RuntimeHost(new RuntimeHostOptions
        {
            GEarth = new GEarthOptions
            {
                Port = port,
                HandshakeTimeout = TimeSpan.FromSeconds(1),
                Title = "QX"
            },
            ScriptsDirectory = Path.Combine(temporary, "scripts"),
            SessionRulesPath = Path.Combine(temporary, "rules.json"),
            HeaderCatalogCachePath = Path.Combine(temporary, "headers"),
            EnableFallbackCatalogs = false,
            EnableClientMonitoring = false,
            EnableMcp = false,
            ReconnectTransport = true,
            TransportRetryDelay = TimeSpan.FromMilliseconds(20)
        });
        int connected = 0;
        var second_connection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Extension.InterceptorConnected += () =>
        {
            if (Interlocked.Increment(ref connected) == 2)
                second_connection.TrySetResult();
        };

        try
        {
            await runtime.StartAsync(cancellation.Token);
            await AcceptHandshakeAndCloseAsync(listener, cancellation.Token, reset);
            await AcceptHandshakeAndCloseAsync(listener, cancellation.Token, reset);
            await second_connection.Task.WaitAsync(cancellation.Token);
            Assert.Equal(2, Volatile.Read(ref connected));
        }
        finally
        {
            await runtime.DisposeAsync();
            Directory.Delete(temporary, true);
        }
    }

    private static (TcpListener First, TcpListener Second) ConsecutiveListeners()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var first = new TcpListener(IPAddress.Loopback, 0);
            first.Start();
            int port = ((IPEndPoint)first.LocalEndpoint).Port;
            if (port == ushort.MaxValue)
            {
                first.Stop();
                continue;
            }

            var second = new TcpListener(IPAddress.Loopback, port + 1);
            try
            {
                second.Start();
                return (first, second);
            }
            catch (SocketException)
            {
                first.Stop();
                second.Stop();
            }
        }

        throw new InvalidOperationException("Unable to reserve consecutive loopback ports.");
    }

    private static async Task RejectAfterClientLeavesAsync(
        TcpListener listener,
        CancellationToken cancellation_token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellation_token);
        byte[] buffer = new byte[1];
        int read = await client.GetStream().ReadAsync(buffer, cancellation_token);
        Assert.Equal(0, read);
    }

    private static async Task ActivateAsync(
        TcpListener listener,
        CancellationToken cancellation_token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellation_token);
        NetworkStream stream = client.GetStream();
        byte[] info = new GControlWriter(GControl.Outgoing.InfoRequest).ToFrame();
        await stream.WriteAsync(info, cancellation_token);
        byte[] length = new byte[4];
        await stream.ReadExactlyAsync(length, cancellation_token);
        int remaining = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(length);
        byte[] response = new byte[remaining];
        await stream.ReadExactlyAsync(response, cancellation_token);
        byte[] activated = new GControlWriter(GControl.Outgoing.OnDoubleClick).ToFrame();
        await stream.WriteAsync(activated, cancellation_token);
        await Task.Delay(50, cancellation_token);
    }

    private static async Task AcceptHandshakeAndCloseAsync(
        TcpListener listener,
        CancellationToken cancellation_token,
        bool reset)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellation_token);
        byte[] frame = new GControlWriter(GControl.Outgoing.InfoRequest).ToFrame();
        await client.GetStream().WriteAsync(frame, cancellation_token);
        if (reset)
        {
            await client.GetStream().ReadExactlyAsync(new byte[1], cancellation_token);
            client.LingerState = new LingerOption(true, 0);
            client.Client.Close(0);
        }
        else
        {
            byte[] length = new byte[4];
            await client.GetStream().ReadExactlyAsync(length, cancellation_token);
            byte[] response = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
            await client.GetStream().ReadExactlyAsync(response, cancellation_token);
        }
    }

    private static async Task<string> ReadExtensionVersionAsync(
        TcpListener listener,
        CancellationToken cancellation_token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellation_token);
        NetworkStream stream = client.GetStream();
        byte[] request = new GControlWriter(GControl.Outgoing.InfoRequest).ToFrame();
        await stream.WriteAsync(request, cancellation_token);

        byte[] length_bytes = new byte[4];
        await stream.ReadExactlyAsync(length_bytes, cancellation_token);
        int length = BinaryPrimitives.ReadInt32BigEndian(length_bytes);
        Assert.True(length >= 2);
        byte[] response = new byte[length];
        await stream.ReadExactlyAsync(response, cancellation_token);
        Assert.Equal(
            GControl.Incoming.ExtensionInfo,
            BinaryPrimitives.ReadInt16BigEndian(response));

        var reader = new GControlReader(response.AsSpan(2));
        Assert.Equal("QX Scripter", reader.ReadString());
        Assert.Equal("QDave", reader.ReadString());
        return reader.ReadString();
    }
}
