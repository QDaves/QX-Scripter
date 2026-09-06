using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game;

public static class FriendPrivateMessageSchema
{
    private const string OutgoingCapabilityName = "unity_private_message_schema";
    private const string IncomingCapabilityName = "unity_console_message_layout";

    public static bool UsesMessageIndex(MessageManager messages, ClientType client)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (client is ClientType.Flash)
            return true;
        if (client is not ClientType.Unity)
            throw new UnsupportedClientException(client);
        if (!messages.TryGetHeader(client, MessageKeys.Friends.PrivateMessageSend, out Header header))
        {
            throw new NotSupportedException(
                "The active Unity build has no resolved private-message request header.");
        }

        MessageDialectCapability capability = ClassifyOutgoing(messages, header, out bool uses_message_index);
        if (!capability.Available)
            throw new NotSupportedException(capability.Reason);
        return uses_message_index;
    }

    public static MessageDialectCapability OutgoingCapability(
        MessageManager messages,
        Header header)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return ClassifyOutgoing(messages, header, out _);
    }

    public static MessageDialectCapability IncomingCapability(
        MessageManager messages,
        Header _)
    {
        ArgumentNullException.ThrowIfNull(messages);
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                IncomingCapabilityName,
                "The active Unity session has no compatible console-message wire layout.");
        }
        if (profile.UnityConsoleMessageLayout is ConsoleMessageWireLayout.Unknown)
        {
            return MessageDialectCapability.Missing(
                IncomingCapabilityName,
                "The active Unity session has no compatible console-message wire layout.");
        }
        return MessageDialectCapability.Ready(IncomingCapabilityName);
    }

    private static MessageDialectCapability ClassifyOutgoing(
        MessageManager messages,
        Header header,
        out bool uses_message_index)
    {
        uses_message_index = false;
        if (!messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas))
        {
            return MessageDialectCapability.Missing(
                OutgoingCapabilityName,
                "The active Unity build has no verified private-message wire schema.");
        }

        bool without_index = schemas.Any(schema => IsPrivateMessageSchema(schema, false));
        bool with_index = schemas.Any(schema => IsPrivateMessageSchema(schema, true));
        if (without_index == with_index)
        {
            return MessageDialectCapability.Missing(
                OutgoingCapabilityName,
                "The active Unity build has an ambiguous private-message wire schema.");
        }
        uses_message_index = with_index;
        return MessageDialectCapability.Ready(OutgoingCapabilityName);
    }

    private static bool IsPrivateMessageSchema(
        OutgoingMessageSchema schema,
        bool with_index)
    {
        int expected_count = with_index ? 3 : 2;
        if (schema.Parameters.Count != expected_count)
            return false;
        if (!IsScalar(schema.Parameters[0], 0, OutgoingWireType.Int64) ||
            !IsScalar(schema.Parameters[1], 1, OutgoingWireType.String))
        {
            return false;
        }
        return !with_index || IsScalar(schema.Parameters[2], 2, OutgoingWireType.Int32);
    }

    private static bool IsScalar(
        OutgoingParameterSchema parameter,
        int position,
        OutgoingWireType wire_type) =>
        parameter.Position == position &&
        parameter.WireType == wire_type &&
        parameter.Collection is OutgoingCollectionKind.None;
}
