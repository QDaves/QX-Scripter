using System.Reflection;
using Qx;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;
using Qx.Scripting;
using Xunit;

namespace QX.Tests;

public sealed class RoomChatSettingsWireTests
{
    [Fact]
    public void flash_standalone_chat_settings_use_the_compact_wire()
    {
        RoomChatSettings settings = Roundtrip<RoomChatSettings>("00000002");

        Assert.Equal(RoomChatFlowMode.FreeFlow, settings.Flow);
        Assert.Equal(RoomChatBubbleWidth.Normal, settings.BubbleWidth);
        Assert.Equal(RoomChatScrollSpeed.Normal, settings.ScrollSpeed);
        Assert.Equal(14, settings.TalkHearingDistance);
        Assert.Equal(RoomChatFloodSensitivity.Loose, settings.FloodProtection);
    }

    [Fact]
    public void flash_standalone_chat_settings_keep_the_full_wire()
    {
        RoomChatSettings settings = Roundtrip<RoomChatSettings>(
            "0000000100000002000000020000000F00000000");

        Assert.Equal(RoomChatFlowMode.LineByLine, settings.Flow);
        Assert.Equal(RoomChatBubbleWidth.Thin, settings.BubbleWidth);
        Assert.Equal(RoomChatScrollSpeed.Slow, settings.ScrollSpeed);
        Assert.Equal(15, settings.TalkHearingDistance);
        Assert.Equal(RoomChatFloodSensitivity.Strict, settings.FloodProtection);
    }

    [Fact]
    public void flash_standalone_chat_settings_reject_unknown_lengths()
    {
        byte[] wire = Convert.FromHexString("0000000200");
        using var packet = new Packet(
            new Header(Direction.In, 1),
            ClientType.Flash,
            new PacketBuffer(wire));

        Assert.Throws<NotSupportedException>(() =>
            packet.Reader().Parse<RoomChatSettings>());
    }

    [Fact]
    public void unity_standalone_chat_settings_keep_the_full_wire()
    {
        RoomChatSettings settings = Roundtrip<RoomChatSettings>(
            "0000000100000002000000020000000F00000000",
            ClientType.Unity);

        Assert.Equal(RoomChatFlowMode.LineByLine, settings.Flow);
        Assert.Equal(RoomChatBubbleWidth.Thin, settings.BubbleWidth);
        Assert.Equal(RoomChatScrollSpeed.Slow, settings.ScrollSpeed);
        Assert.Equal(15, settings.TalkHearingDistance);
        Assert.Equal(RoomChatFloodSensitivity.Strict, settings.FloodProtection);
    }

    [Theory]
    [InlineData(
        "01000100000000000000000100000002010000000201",
        true,
        true)]
    [InlineData(
        "01000100000000000000000100000002010000000100000002000000000000000F00000001",
        null,
        false)]
    [InlineData(
        "01000100000000000000000100000002010000000100000002000000000000000F0000000101",
        true,
        false)]
    public void flash_guest_room_details_keep_their_embedded_chat_layout(
        string hex,
        bool? opening_connection,
        bool compact)
    {
        RoomResultDetails details = Roundtrip<RoomResultDetails>(hex);

        Assert.True(details.Forward);
        Assert.False(details.IsStaffPick);
        Assert.True(details.IsGroupMember);
        Assert.False(details.IsRoomMuted);
        Assert.True(details.CanMute);
        Assert.Equal(opening_connection, details.OpeningConnection);
        Assert.Equal(
            compact ? RoomChatFlowMode.FreeFlow : RoomChatFlowMode.LineByLine,
            details.Chat.Flow);
        Assert.Equal(
            compact ? RoomChatBubbleWidth.Normal : RoomChatBubbleWidth.Thin,
            details.Chat.BubbleWidth);
        Assert.Equal(
            compact ? RoomChatScrollSpeed.Normal : RoomChatScrollSpeed.Fast,
            details.Chat.ScrollSpeed);
        Assert.Equal(compact ? 14 : 15, details.Chat.TalkHearingDistance);
        Assert.Equal(
            compact ? RoomChatFloodSensitivity.Loose : RoomChatFloodSensitivity.Normal,
            details.Chat.FloodProtection);
    }

    [Fact]
    public void flash_embedded_compact_chat_requires_its_opening_flag()
    {
        byte[] wire = Convert.FromHexString(
            "010001000000000000000001000000020100000002");
        using var packet = new Packet(
            new Header(Direction.In, 1),
            ClientType.Flash,
            new PacketBuffer(wire));

        Assert.Throws<NotSupportedException>(() =>
            packet.Reader().Parse<RoomResultDetails>());
    }

    [Fact]
    public void unity_standalone_chat_projection_does_not_require_a_guest_room_profile()
    {
        byte[] wire = Convert.FromHexString(
            "0000000100000002000000020000000F00000000");
        var messages = MessageManager.CreateWithEmbeddedMap();
        using var packet = new Packet(
            new Header(Direction.In, 1),
            ClientType.Unity,
            new PacketBuffer(wire))
        {
            Context = new ParserContext(messages, default)
        };
        var intercept = new Intercept { Packet = packet, Sequence = 1 };
        bool invoked = false;
        Action<Intercept> callback = view =>
        {
            invoked = true;
            Assert.Equal(ClientType.Flash, view.Packet.Client);
            Assert.Equal(
                Convert.FromHexString("00000000"),
                view.Packet.Buffer.Span.ToArray());
            RoomChatSettings changed = view.Packet.Reader().Parse<RoomChatSettings>();
            changed.FloodProtection = RoomChatFloodSensitivity.Loose;
            view.Packet.Clear();
            view.Packet.Writer().Compose(changed);
        };
        Type compatibility = typeof(ScriptEngine).Assembly.GetType(
            "Qx.Scripting.UnityIncomingCompatibility",
            throwOnError: true)!;
        MethodInfo invoke = compatibility.GetMethod(
            "Invoke",
            BindingFlags.Public | BindingFlags.Static)!;

        invoke.Invoke(null, ["RoomChatSettings", intercept, callback]);

        Assert.True(invoked);
        packet.Position = 0;
        RoomChatSettings settings = packet.Reader().Parse<RoomChatSettings>();
        Assert.Equal(RoomChatFlowMode.LineByLine, settings.Flow);
        Assert.Equal(RoomChatBubbleWidth.Thin, settings.BubbleWidth);
        Assert.Equal(RoomChatScrollSpeed.Slow, settings.ScrollSpeed);
        Assert.Equal(15, settings.TalkHearingDistance);
        Assert.Equal(RoomChatFloodSensitivity.Loose, settings.FloodProtection);
    }

    static T Roundtrip<T>(string hex, ClientType client = ClientType.Flash)
        where T : IParserComposer<T>
    {
        byte[] wire = Convert.FromHexString(hex);
        using var packet = new Packet(
            new Header(Direction.In, 1),
            client,
            new PacketBuffer(wire));

        T value = packet.Reader().Parse<T>();

        Assert.Equal(0, packet.Available);
        using var composed = new Packet(new Header(Direction.In, 1), client);
        composed.Writer().Compose(value);
        Assert.Equal(wire, composed.Buffer.Span.ToArray());
        return value;
    }
}
