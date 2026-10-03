namespace Qx.Interception.GEarth;

/// <summary>Contains the headers of the G-Earth extension control protocol.</summary>
/// <remarks>The nested class names follow G-Earth's point of view.</remarks>
internal static class GControl
{
    /// <summary>Contains the headers of control frames an extension sends to G-Earth.</summary>
    public static class Incoming
    {
        /// <summary>The reply to an info request, with the extension's title, author, version, description and flags.</summary>
        public const short ExtensionInfo = 1;
        /// <summary>The reply to an intercepted packet, with the packet as it should be forwarded or blocked.</summary>
        public const short ManipulatedPacket = 2;
        /// <summary>A request for the flags G-Earth was started with.</summary>
        public const short RequestFlags = 3;
        /// <summary>A packet to send to the client or the server.</summary>
        public const short SendMessage = 4;
        /// <summary>A request to convert a packet to its text expression.</summary>
        public const short PacketToStringRequest = 20;
        /// <summary>A request to convert a text expression to a packet.</summary>
        public const short StringToPacketRequest = 21;
        /// <summary>A line for G-Earth's extension console.</summary>
        public const short ExtensionConsoleLog = 98;
    }

    /// <summary>Contains the headers of control frames G-Earth sends to an extension.</summary>
    public static class Outgoing
    {
        /// <summary>The user activated the extension in G-Earth.</summary>
        public const short OnDoubleClick = 1;
        /// <summary>A request for the extension's info.</summary>
        public const short InfoRequest = 2;
        /// <summary>An intercepted packet that G-Earth holds until the extension replies.</summary>
        public const short PacketIntercept = 3;
        /// <summary>The reply to a flags request.</summary>
        public const short FlagsCheck = 4;
        /// <summary>A hotel connection started, with its host, port, versions, client type and message catalog.</summary>
        public const short ConnectionStart = 5;
        /// <summary>The hotel connection ended.</summary>
        public const short ConnectionEnd = 6;
        /// <summary>The extension was initialized.</summary>
        public const short Init = 7;
        /// <summary>Updated information about the G-Earth host.</summary>
        public const short UpdateHostInfo = 10;
        /// <summary>The reply to a packet to text request.</summary>
        public const short PacketToStringResponse = 20;
        /// <summary>The reply to a text to packet request.</summary>
        public const short StringToPacketResponse = 21;
        /// <summary>A free flow frame, which is accepted as a control frame but not handled.</summary>
        public const short FreeFlow = 99;
    }
}
