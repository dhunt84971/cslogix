using System;
using Xunit;
using CSLogix;
using CSLogix.Constants;

namespace CSLogix.Tests
{
    public class PacketBuildingTests
    {
        #region BuildRRDataHeader Tests

        [Fact]
        public void BuildRRDataHeader_WithZeroLength_ReturnsCorrectHeader()
        {
            using var plc = new PLC("192.168.1.1");
            var connection = new Connection(plc);

            var header = connection.BuildRRDataHeader(0);

            // Should be 40 bytes
            Assert.Equal(40, header.Length);

            // EIP Command should be SendRRData (0x006F)
            ushort command = BitConverter.ToUInt16(header, 0);
            Assert.Equal(EIPCommands.SendRRData, command);

            // Length should be 16 + frameLen = 16
            ushort length = BitConverter.ToUInt16(header, 2);
            Assert.Equal(16, length);

            // Item count should be 2
            ushort itemCount = BitConverter.ToUInt16(header, 30);
            Assert.Equal(2, itemCount);

            // Item 1 type should be Null Address (0x0000)
            ushort item1Type = BitConverter.ToUInt16(header, 32);
            Assert.Equal(0x0000, item1Type);

            // Item 1 length should be 0
            ushort item1Len = BitConverter.ToUInt16(header, 34);
            Assert.Equal(0, item1Len);

            // Item 2 type should be Unconnected Data (0x00B2)
            ushort item2Type = BitConverter.ToUInt16(header, 36);
            Assert.Equal(0x00B2, item2Type);

            // Item 2 length should be frameLen (0)
            ushort item2Len = BitConverter.ToUInt16(header, 38);
            Assert.Equal(0, item2Len);
        }

        [Fact]
        public void BuildRRDataHeader_WithPayload_ReturnsCorrectLength()
        {
            using var plc = new PLC("192.168.1.1");
            var connection = new Connection(plc);

            var header = connection.BuildRRDataHeader(100);

            // Length should be 16 + 100 = 116
            ushort length = BitConverter.ToUInt16(header, 2);
            Assert.Equal(116, length);

            // Item 2 length should be frameLen (100)
            ushort item2Len = BitConverter.ToUInt16(header, 38);
            Assert.Equal(100, item2Len);
        }

        [Fact]
        public void BuildRRDataHeader_HasCorrectStructure()
        {
            using var plc = new PLC("192.168.1.1");
            var connection = new Connection(plc);

            var header = connection.BuildRRDataHeader(50);

            // Session handle at offset 4 (should be 0 - not registered)
            uint sessionHandle = BitConverter.ToUInt32(header, 4);
            Assert.Equal(0u, sessionHandle);

            // Status at offset 8 (should be 0)
            uint status = BitConverter.ToUInt32(header, 8);
            Assert.Equal(0u, status);

            // Interface handle at offset 24 (should be 0)
            uint ifHandle = BitConverter.ToUInt32(header, 24);
            Assert.Equal(0u, ifHandle);

            // Timeout at offset 28 (should be 0)
            ushort timeout = BitConverter.ToUInt16(header, 28);
            Assert.Equal(0, timeout);
        }

        #endregion

        #region CIP Service Constants Tests

        [Fact]
        public void CIPServices_HasCorrectReadTagCode()
        {
            Assert.Equal(0x4C, CIPServices.ReadTag);
        }

        [Fact]
        public void CIPServices_HasCorrectWriteTagCode()
        {
            Assert.Equal(0x4D, CIPServices.WriteTag);
        }

        [Fact]
        public void CIPServices_HasCorrectForwardOpenCode()
        {
            Assert.Equal(0x54, CIPServices.ForwardOpen);
        }

        [Fact]
        public void CIPServices_HasCorrectLargeForwardOpenCode()
        {
            Assert.Equal(0x5B, CIPServices.LargeForwardOpen);
        }

        [Fact]
        public void CIPServices_HasCorrectMultipleServicePacketCode()
        {
            Assert.Equal(0x0A, CIPServices.MultipleServicePacket);
        }

        [Fact]
        public void CIPServices_HasCorrectFragmentedReadCode()
        {
            Assert.Equal(0x52, CIPServices.ReadTagFragmented);
        }

        [Fact]
        public void CIPServices_HasCorrectFragmentedWriteCode()
        {
            Assert.Equal(0x53, CIPServices.WriteTagFragmented);
        }

        [Fact]
        public void CIPServices_HasCorrectGetInstanceAttributeListCode()
        {
            Assert.Equal(0x55, CIPServices.GetInstanceAttributeList);
        }

        #endregion

        #region EIP Commands Tests

        [Fact]
        public void EIPCommands_HasCorrectRegisterSessionCode()
        {
            Assert.Equal(0x0065, EIPCommands.RegisterSession);
        }

        [Fact]
        public void EIPCommands_HasCorrectUnregisterSessionCode()
        {
            Assert.Equal(0x0066, EIPCommands.UnregisterSession);
        }

        [Fact]
        public void EIPCommands_HasCorrectSendRRDataCode()
        {
            Assert.Equal(0x006F, EIPCommands.SendRRData);
        }

        [Fact]
        public void EIPCommands_HasCorrectSendUnitDataCode()
        {
            Assert.Equal(0x0070, EIPCommands.SendUnitData);
        }

        [Fact]
        public void EIPCommands_HasCorrectListIdentityCode()
        {
            Assert.Equal(0x0063, EIPCommands.ListIdentity);
        }

        #endregion

        #region CIP Classes Tests

        [Fact]
        public void CIPClasses_HasCorrectIdentityCode()
        {
            Assert.Equal(0x01, CIPClasses.Identity);
        }

        [Fact]
        public void CIPClasses_HasCorrectMessageRouterCode()
        {
            Assert.Equal(0x02, CIPClasses.MessageRouter);
        }

        [Fact]
        public void CIPClasses_HasCorrectConnectionManagerCode()
        {
            Assert.Equal(0x06, CIPClasses.ConnectionManager);
        }

        [Fact]
        public void CIPClasses_HasCorrectSymbolCode()
        {
            Assert.Equal(0x6B, CIPClasses.Symbol);
        }

        [Fact]
        public void CIPClasses_HasCorrectTemplateCode()
        {
            Assert.Equal(0x6C, CIPClasses.Template);
        }

        #endregion

        #region Port IDs Tests

        [Fact]
        public void PortIDs_HasCorrectBackplaneCode()
        {
            Assert.Equal(0x01, PortIDs.Backplane);
        }

        [Fact]
        public void PortIDs_HasCorrectEthernetCode()
        {
            Assert.Equal(0x02, PortIDs.Ethernet);
        }

        #endregion

        #region Byte Order Tests (Little Endian)

        [Fact]
        public void BuildRRDataHeader_UsesLittleEndianByteOrder()
        {
            using var plc = new PLC("192.168.1.1");
            var connection = new Connection(plc);

            var header = connection.BuildRRDataHeader(0x1234);

            // Check the length field (0x1234 + 16 = 0x1244)
            // In little endian: 0x44 0x12
            Assert.Equal(0x44, header[2]);
            Assert.Equal(0x12, header[3]);
        }

        #endregion

        #region Symbolic Segment Format Tests

        [Fact]
        public void SymbolicSegment_StartsWithCorrectMarker()
        {
            // 0x91 is the symbolic segment marker
            // This is tested via TagParser but verify the constant value
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Test");

            Assert.Equal(0x91, ioi[0]);
        }

        [Fact]
        public void SymbolicSegment_HasCorrectLengthByte()
        {
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Test");

            // Length should be 4 ("Test" has 4 characters)
            Assert.Equal(4, ioi[1]);
        }

        [Fact]
        public void SymbolicSegment_ContainsTagName()
        {
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Test");

            Assert.Equal((byte)'T', ioi[2]);
            Assert.Equal((byte)'e', ioi[3]);
            Assert.Equal((byte)'s', ioi[4]);
            Assert.Equal((byte)'t', ioi[5]);
        }

        #endregion

        #region Element Segment Format Tests

        [Fact]
        public void ElementSegment_8Bit_HasCorrectFormat()
        {
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Tag[5]");

            // After "Tag" (3 chars + padding) we should have element segment
            // 0x91, 0x03, 'T', 'a', 'g', 0x00 (padding), 0x28, 0x05
            int offset = 6; // After tag name and padding
            Assert.Equal(0x28, ioi[offset]); // 8-bit element segment marker
            Assert.Equal(5, ioi[offset + 1]);
        }

        [Fact]
        public void ElementSegment_16Bit_HasCorrectFormat()
        {
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Tag[500]");

            int offset = 6;
            Assert.Equal(0x29, ioi[offset]); // 16-bit element segment marker
            Assert.Equal(0x00, ioi[offset + 1]); // Padding byte
            // 500 = 0x01F4
            Assert.Equal(0xF4, ioi[offset + 2]); // Low byte
            Assert.Equal(0x01, ioi[offset + 3]); // High byte
        }

        [Fact]
        public void ElementSegment_32Bit_HasCorrectFormat()
        {
            byte[] ioi = CSLogix.Helpers.TagParser.BuildIOI("Tag[100000]");

            int offset = 6;
            Assert.Equal(0x2A, ioi[offset]); // 32-bit element segment marker
            Assert.Equal(0x00, ioi[offset + 1]); // Padding byte
            // 100000 = 0x000186A0
            Assert.Equal(0xA0, ioi[offset + 2]); // Lowest byte
            Assert.Equal(0x86, ioi[offset + 3]);
            Assert.Equal(0x01, ioi[offset + 4]);
            Assert.Equal(0x00, ioi[offset + 5]); // Highest byte
        }

        #endregion

        #region CIP Type Constants Tests

        [Fact]
        public void CIPTypes_HasCorrectBoolCode()
        {
            Assert.Equal(0xC1, CIPTypes.BOOL);
        }

        [Fact]
        public void CIPTypes_HasCorrectSintCode()
        {
            Assert.Equal(0xC2, CIPTypes.SINT);
        }

        [Fact]
        public void CIPTypes_HasCorrectIntCode()
        {
            Assert.Equal(0xC3, CIPTypes.INT);
        }

        [Fact]
        public void CIPTypes_HasCorrectDintCode()
        {
            Assert.Equal(0xC4, CIPTypes.DINT);
        }

        [Fact]
        public void CIPTypes_HasCorrectLintCode()
        {
            Assert.Equal(0xC5, CIPTypes.LINT);
        }

        [Fact]
        public void CIPTypes_HasCorrectRealCode()
        {
            Assert.Equal(0xCA, CIPTypes.REAL);
        }

        [Fact]
        public void CIPTypes_HasCorrectLrealCode()
        {
            Assert.Equal(0xCB, CIPTypes.LREAL);
        }

        [Fact]
        public void CIPTypes_HasCorrectDwordCode()
        {
            Assert.Equal(0xD3, CIPTypes.DWORD);
        }

        [Fact]
        public void CIPTypes_HasCorrectStringCode()
        {
            Assert.Equal(0xDA, CIPTypes.STRING);
        }

        [Fact]
        public void CIPTypes_HasCorrectStructCode()
        {
            Assert.Equal(0xA0, CIPTypes.STRUCT);
        }

        #endregion

        #region CIPTypes Helper Methods Tests

        [Fact]
        public void CIPTypes_GetSize_ReturnsCorrectSizeForBool()
        {
            Assert.Equal(1, CIPTypes.GetSize(CIPTypes.BOOL));
        }

        [Fact]
        public void CIPTypes_GetSize_ReturnsCorrectSizeForInt()
        {
            Assert.Equal(2, CIPTypes.GetSize(CIPTypes.INT));
        }

        [Fact]
        public void CIPTypes_GetSize_ReturnsCorrectSizeForDint()
        {
            Assert.Equal(4, CIPTypes.GetSize(CIPTypes.DINT));
        }

        [Fact]
        public void CIPTypes_GetSize_ReturnsCorrectSizeForReal()
        {
            Assert.Equal(4, CIPTypes.GetSize(CIPTypes.REAL));
        }

        [Fact]
        public void CIPTypes_GetSize_ReturnsCorrectSizeForLint()
        {
            Assert.Equal(8, CIPTypes.GetSize(CIPTypes.LINT));
        }

        [Fact]
        public void CIPTypes_GetSize_ReturnsDefaultForUnknown()
        {
            Assert.Equal(1, CIPTypes.GetSize(0xFF));
        }

        [Fact]
        public void CIPTypes_GetName_ReturnsCorrectNames()
        {
            Assert.Equal("BOOL", CIPTypes.GetName(CIPTypes.BOOL));
            Assert.Equal("DINT", CIPTypes.GetName(CIPTypes.DINT));
            Assert.Equal("REAL", CIPTypes.GetName(CIPTypes.REAL));
            Assert.Equal("STRING", CIPTypes.GetName(CIPTypes.STRING));
        }

        [Fact]
        public void CIPTypes_GetName_ReturnsUnknownForInvalidCode()
        {
            Assert.Equal("UNKNOWN", CIPTypes.GetName(0xFF));
        }

        [Fact]
        public void CIPTypes_IsStruct_ReturnsTrueForStructCode()
        {
            Assert.True(CIPTypes.IsStruct(CIPTypes.STRUCT));
        }

        [Fact]
        public void CIPTypes_IsStruct_ReturnsFalseForOtherCodes()
        {
            Assert.False(CIPTypes.IsStruct(CIPTypes.DINT));
            Assert.False(CIPTypes.IsStruct(CIPTypes.REAL));
        }

        [Fact]
        public void CIPTypes_IsFloat_ReturnsTrueForFloatingTypes()
        {
            Assert.True(CIPTypes.IsFloat(CIPTypes.REAL));
            Assert.True(CIPTypes.IsFloat(CIPTypes.LREAL));
        }

        [Fact]
        public void CIPTypes_IsFloat_ReturnsFalseForIntegerTypes()
        {
            Assert.False(CIPTypes.IsFloat(CIPTypes.DINT));
            Assert.False(CIPTypes.IsFloat(CIPTypes.SINT));
        }

        [Fact]
        public void CIPTypes_IsBoolArray_ReturnsTrueForDword()
        {
            Assert.True(CIPTypes.IsBoolArray(CIPTypes.DWORD));
        }

        [Fact]
        public void CIPTypes_IsBoolArray_ReturnsFalseForOtherTypes()
        {
            Assert.False(CIPTypes.IsBoolArray(CIPTypes.BOOL));
            Assert.False(CIPTypes.IsBoolArray(CIPTypes.DINT));
        }

        [Fact]
        public void CIPTypes_GetDotNetType_ReturnsCorrectTypes()
        {
            Assert.Equal(typeof(bool), CIPTypes.GetDotNetType(CIPTypes.BOOL));
            Assert.Equal(typeof(int), CIPTypes.GetDotNetType(CIPTypes.DINT));
            Assert.Equal(typeof(float), CIPTypes.GetDotNetType(CIPTypes.REAL));
            Assert.Equal(typeof(double), CIPTypes.GetDotNetType(CIPTypes.LREAL));
            Assert.Equal(typeof(short), CIPTypes.GetDotNetType(CIPTypes.INT));
            Assert.Equal(typeof(long), CIPTypes.GetDotNetType(CIPTypes.LINT));
        }

        #endregion
    }
}
