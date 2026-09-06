namespace Qx.Interception.GEarth;

public static class GControl
{
    public static class Incoming
    {
        public const short ExtensionInfo = 1;
        public const short ManipulatedPacket = 2;
        public const short RequestFlags = 3;
        public const short SendMessage = 4;
        public const short PacketToStringRequest = 20;
        public const short StringToPacketRequest = 21;
        public const short ExtensionConsoleLog = 98;
    }

    public static class Outgoing
    {
        public const short OnDoubleClick = 1;
        public const short InfoRequest = 2;
        public const short PacketIntercept = 3;
        public const short FlagsCheck = 4;
        public const short ConnectionStart = 5;
        public const short ConnectionEnd = 6;
        public const short Init = 7;
        public const short UpdateHostInfo = 10;
        public const short PacketToStringResponse = 20;
        public const short StringToPacketResponse = 21;
        public const short FreeFlow = 99;
    }
}
