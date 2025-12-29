using System;

namespace CSLogix.Constants
{
    /// <summary>
    /// CIP (Common Industrial Protocol) service codes used in EtherNet/IP communication.
    /// </summary>
    public static class CIPServices
    {
        /// <summary>Read Tag service code.</summary>
        public const byte ReadTag = 0x4C;

        /// <summary>Write Tag service code.</summary>
        public const byte WriteTag = 0x4D;

        /// <summary>Read/Write/Modify service code (for bit-level operations).</summary>
        public const byte ReadModifyWrite = 0x4E;

        /// <summary>Read Tag Fragmented service code (for partial reads).</summary>
        public const byte ReadTagFragmented = 0x52;

        /// <summary>Write Tag Fragmented service code (for partial writes).</summary>
        public const byte WriteTagFragmented = 0x53;

        /// <summary>Forward Open service code (for establishing connections).</summary>
        public const byte ForwardOpen = 0x54;

        /// <summary>Get Instance Attribute List service code (for tag list retrieval).</summary>
        public const byte GetInstanceAttributeList = 0x55;

        /// <summary>Large Forward Open service code (for large packet connections).</summary>
        public const byte LargeForwardOpen = 0x5B;

        /// <summary>Forward Close service code.</summary>
        public const byte ForwardClose = 0x4E;

        /// <summary>Multiple Service Packet service code (for batch operations).</summary>
        public const byte MultipleServicePacket = 0x0A;

        /// <summary>Get Attributes All service code.</summary>
        public const byte GetAttributesAll = 0x01;

        /// <summary>Get Attribute Single service code.</summary>
        public const byte GetAttributeSingle = 0x0E;

        /// <summary>Set Attribute Single service code.</summary>
        public const byte SetAttributeSingle = 0x10;

        /// <summary>List Identity service code (for device discovery).</summary>
        public const byte ListIdentity = 0x63;

        /// <summary>Unconnected Send service code.</summary>
        public const byte UnconnectedSend = 0x52;
    }

    /// <summary>
    /// EIP (EtherNet/IP) encapsulation commands.
    /// </summary>
    public static class EIPCommands
    {
        /// <summary>NOP command.</summary>
        public const ushort Nop = 0x0000;

        /// <summary>List Services command.</summary>
        public const ushort ListServices = 0x0004;

        /// <summary>List Identity command.</summary>
        public const ushort ListIdentity = 0x0063;

        /// <summary>List Interfaces command.</summary>
        public const ushort ListInterfaces = 0x0064;

        /// <summary>Register Session command.</summary>
        public const ushort RegisterSession = 0x0065;

        /// <summary>Unregister Session command.</summary>
        public const ushort UnregisterSession = 0x0066;

        /// <summary>Send RR Data command (unconnected messaging).</summary>
        public const ushort SendRRData = 0x006F;

        /// <summary>Send Unit Data command (connected messaging).</summary>
        public const ushort SendUnitData = 0x0070;
    }

    /// <summary>
    /// Common CIP class codes.
    /// </summary>
    public static class CIPClasses
    {
        /// <summary>Identity Object class.</summary>
        public const byte Identity = 0x01;

        /// <summary>Message Router class.</summary>
        public const byte MessageRouter = 0x02;

        /// <summary>Connection Manager class.</summary>
        public const byte ConnectionManager = 0x06;

        /// <summary>Symbol Object class (for tag access).</summary>
        public const byte Symbol = 0x6B;

        /// <summary>Template Object class (for UDT templates).</summary>
        public const byte Template = 0x6C;

        /// <summary>Wall Clock Time class.</summary>
        public const byte WallClockTime = 0x8B;
    }

    /// <summary>
    /// Common Port IDs for routing.
    /// </summary>
    public static class PortIDs
    {
        /// <summary>Backplane port ID.</summary>
        public const byte Backplane = 0x01;

        /// <summary>Ethernet port ID.</summary>
        public const byte Ethernet = 0x02;
    }
}
