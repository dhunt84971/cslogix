using System;
using Xunit;
using CSLogix.Helpers;
using CSLogix.Constants;

namespace CSLogix.Tests.Helpers
{
    public class TagParserTests
    {
        #region Parse Tests - Simple Tags

        [Fact]
        public void Parse_SimpleTag_ReturnsBaseTag()
        {
            var result = TagParser.Parse("MyTag");

            Assert.Equal("MyTag", result.BaseTag);
            Assert.Empty(result.ArrayIndices);
            Assert.Null(result.BitIndex);
            Assert.Null(result.ProgramName);
            Assert.Empty(result.Members);
        }

        [Fact]
        public void Parse_TagWithDifferentCasing_PreservesCase()
        {
            var result = TagParser.Parse("MyDINT_Counter");

            Assert.Equal("MyDINT_Counter", result.BaseTag);
        }

        #endregion

        #region Parse Tests - Program Scope

        [Fact]
        public void Parse_ProgramScopedTag_ExtractsProgramName()
        {
            var result = TagParser.Parse("Program:MainProgram.Counter");

            Assert.Equal("Program:MainProgram", result.ProgramName);
            Assert.Equal("Counter", result.BaseTag);
        }

        [Fact]
        public void Parse_ProgramScopedTag_CaseInsensitive()
        {
            var result = TagParser.Parse("PROGRAM:TestProgram.MyTag");

            Assert.Equal("PROGRAM:TestProgram", result.ProgramName);
            Assert.Equal("MyTag", result.BaseTag);
        }

        [Fact]
        public void Parse_ProgramScopedTagWithMember_ParsesAllParts()
        {
            var result = TagParser.Parse("Program:MainProgram.MyUDT.Field1");

            Assert.Equal("Program:MainProgram", result.ProgramName);
            Assert.Equal("MyUDT", result.BaseTag);
            Assert.Single(result.Members);
            Assert.Equal("Field1", result.Members[0]);
        }

        #endregion

        #region Parse Tests - Array Indexing

        [Fact]
        public void Parse_OneDimensionalArray_ExtractsIndex()
        {
            var result = TagParser.Parse("MyArray[5]");

            Assert.Equal("MyArray", result.BaseTag);
            Assert.Single(result.ArrayIndices);
            Assert.Equal(5, result.ArrayIndices[0]);
        }

        [Fact]
        public void Parse_TwoDimensionalArray_ExtractsBothIndices()
        {
            var result = TagParser.Parse("MyArray[3,7]");

            Assert.Equal("MyArray", result.BaseTag);
            Assert.Equal(2, result.ArrayIndices.Length);
            Assert.Equal(3, result.ArrayIndices[0]);
            Assert.Equal(7, result.ArrayIndices[1]);
        }

        [Fact]
        public void Parse_ThreeDimensionalArray_ExtractsAllIndices()
        {
            var result = TagParser.Parse("MyArray[1,2,3]");

            Assert.Equal("MyArray", result.BaseTag);
            Assert.Equal(3, result.ArrayIndices.Length);
            Assert.Equal(1, result.ArrayIndices[0]);
            Assert.Equal(2, result.ArrayIndices[1]);
            Assert.Equal(3, result.ArrayIndices[2]);
        }

        [Fact]
        public void Parse_ArrayWithSpaces_TrimsWhitespace()
        {
            var result = TagParser.Parse("MyArray[ 5 , 10 ]");

            Assert.Equal(2, result.ArrayIndices.Length);
            Assert.Equal(5, result.ArrayIndices[0]);
            Assert.Equal(10, result.ArrayIndices[1]);
        }

        [Fact]
        public void Parse_LargeArrayIndex_HandlesCorrectly()
        {
            var result = TagParser.Parse("BigArray[65535]");

            Assert.Single(result.ArrayIndices);
            Assert.Equal(65535, result.ArrayIndices[0]);
        }

        #endregion

        #region Parse Tests - UDT Members

        [Fact]
        public void Parse_UDTWithOneMember_ExtractsMember()
        {
            var result = TagParser.Parse("MyUDT.Field1");

            Assert.Equal("MyUDT", result.BaseTag);
            Assert.Single(result.Members);
            Assert.Equal("Field1", result.Members[0]);
        }

        [Fact]
        public void Parse_UDTWithMultipleMembers_ExtractsAllMembers()
        {
            var result = TagParser.Parse("MyUDT.Level1.Level2.Level3");

            Assert.Equal("MyUDT", result.BaseTag);
            Assert.Equal(3, result.Members.Count);
            Assert.Equal("Level1", result.Members[0]);
            Assert.Equal("Level2", result.Members[1]);
            Assert.Equal("Level3", result.Members[2]);
        }

        [Fact]
        public void Parse_ArrayMemberInUDT_IncludesIndexInMember()
        {
            var result = TagParser.Parse("MyUDT.ArrayField[5]");

            Assert.Equal("MyUDT", result.BaseTag);
            Assert.Single(result.Members);
            Assert.Equal("ArrayField[5]", result.Members[0]);
        }

        [Fact]
        public void Parse_ComplexNestedStructure_ParsesCorrectly()
        {
            var result = TagParser.Parse("Root[0].Child.GrandChild[2,3].Value");

            Assert.Equal("Root", result.BaseTag);
            Assert.Single(result.ArrayIndices);
            Assert.Equal(0, result.ArrayIndices[0]);
            Assert.Equal(3, result.Members.Count);
            Assert.Equal("Child", result.Members[0]);
            Assert.Equal("GrandChild[2,3]", result.Members[1]);
            Assert.Equal("Value", result.Members[2]);
        }

        #endregion

        #region Parse Tests - Bit Addressing

        [Fact]
        public void Parse_BitAddressing_ExtractsBitIndex()
        {
            var result = TagParser.Parse("MyDINT.5");

            Assert.Equal("MyDINT", result.BaseTag);
            Assert.Equal(5, result.BitIndex);
            Assert.Empty(result.Members);
        }

        [Fact]
        public void Parse_BitAddressingOnArray_ExtractsBitIndex()
        {
            var result = TagParser.Parse("MyDINTArray[10].7");

            Assert.Equal("MyDINTArray", result.BaseTag);
            Assert.Single(result.ArrayIndices);
            Assert.Equal(10, result.ArrayIndices[0]);
            Assert.Equal(7, result.BitIndex);
        }

        [Fact]
        public void Parse_BitAddressingMaxBit_ExtractsBitIndex()
        {
            var result = TagParser.Parse("MyDINT.31");

            Assert.Equal(31, result.BitIndex);
        }

        #endregion

        #region BuildIOI Tests - Simple Tags

        [Fact]
        public void BuildIOI_SimpleTag_ReturnsCorrectFormat()
        {
            var ioi = TagParser.BuildIOI("Test");

            // Expected: 0x91 (symbolic), length, "Test", padding
            Assert.Equal(0x91, ioi[0]);     // Symbolic segment
            Assert.Equal(4, ioi[1]);        // Length of "Test"
            Assert.Equal((byte)'T', ioi[2]);
            Assert.Equal((byte)'e', ioi[3]);
            Assert.Equal((byte)'s', ioi[4]);
            Assert.Equal((byte)'t', ioi[5]);
            // No padding needed (4 bytes = even)
            Assert.Equal(6, ioi.Length);
        }

        [Fact]
        public void BuildIOI_OddLengthTag_AddsPadding()
        {
            var ioi = TagParser.BuildIOI("Tag");

            // Expected: 0x91, 3, "Tag", 0x00 (padding)
            Assert.Equal(0x91, ioi[0]);
            Assert.Equal(3, ioi[1]);
            Assert.Equal((byte)'T', ioi[2]);
            Assert.Equal((byte)'a', ioi[3]);
            Assert.Equal((byte)'g', ioi[4]);
            Assert.Equal(0x00, ioi[5]); // Padding
            Assert.Equal(6, ioi.Length);
        }

        #endregion

        #region BuildIOI Tests - Array Indexing

        [Fact]
        public void BuildIOI_ArrayWithSmallIndex_Uses8BitSegment()
        {
            var ioi = TagParser.BuildIOI("Arr[5]");

            // Find element segment (0x28 for 8-bit)
            int nameEnd = 2 + 3 + 1; // 0x91, len, "Arr", padding
            Assert.Equal(0x28, ioi[nameEnd]);
            Assert.Equal(5, ioi[nameEnd + 1]);
        }

        [Fact]
        public void BuildIOI_ArrayWith8BitMaxIndex_Uses8BitSegment()
        {
            var ioi = TagParser.BuildIOI("Arr[255]");

            int nameEnd = 2 + 3 + 1;
            Assert.Equal(0x28, ioi[nameEnd]);
            Assert.Equal(255, ioi[nameEnd + 1]);
        }

        [Fact]
        public void BuildIOI_ArrayWith16BitIndex_Uses16BitSegment()
        {
            var ioi = TagParser.BuildIOI("Arr[1000]");

            int nameEnd = 2 + 3 + 1;
            // 0x29, 0x00, low byte, high byte
            Assert.Equal(0x29, ioi[nameEnd]);
            Assert.Equal(0x00, ioi[nameEnd + 1]);
            Assert.Equal(1000 & 0xFF, ioi[nameEnd + 2]);
            Assert.Equal((1000 >> 8) & 0xFF, ioi[nameEnd + 3]);
        }

        [Fact]
        public void BuildIOI_ArrayWithLargeIndex_Uses32BitSegment()
        {
            var ioi = TagParser.BuildIOI("Arr[100000]");

            int nameEnd = 2 + 3 + 1;
            // 0x2A, 0x00, 4 bytes index
            Assert.Equal(0x2A, ioi[nameEnd]);
            Assert.Equal(0x00, ioi[nameEnd + 1]);
        }

        #endregion

        #region BuildIOI Tests - Program Scope

        [Fact]
        public void BuildIOI_ProgramScoped_IncludesProgramSegment()
        {
            var ioi = TagParser.BuildIOI("Program:Main.Counter");

            // Should start with program name segment
            Assert.Equal(0x91, ioi[0]);
            // Program:Main is 12 chars
            Assert.Equal(12, ioi[1]);
        }

        #endregion

        #region BuildIOI Tests - UDT Members

        [Fact]
        public void BuildIOI_UDTMember_IncludesMemberSegment()
        {
            var ioi = TagParser.BuildIOI("MyUDT.Field");

            // Should have two symbolic segments
            int firstSymbol = 0;
            Assert.Equal(0x91, ioi[firstSymbol]);

            // Find second symbolic segment (after first tag name)
            int nameLen = ioi[1];
            int secondSymbol = 2 + nameLen;
            if (nameLen % 2 != 0) secondSymbol++; // padding

            Assert.Equal(0x91, ioi[secondSymbol]);
        }

        #endregion

        #region BuildIOI Tests - BOOL Array (DWORD storage)

        [Fact]
        public void BuildIOI_BoolArrayWithDWORDType_AdjustsIndex()
        {
            // BOOL arrays are stored as DWORD, index 32 should become DWORD index 1
            var ioi = TagParser.BuildIOI("BoolArray[32]", CIPTypes.DWORD);

            // Find element segment
            int nameEnd = 2 + "BoolArray".Length;
            if ("BoolArray".Length % 2 != 0) nameEnd++;

            Assert.Equal(0x28, ioi[nameEnd]);
            Assert.Equal(1, ioi[nameEnd + 1]); // 32 / 32 = 1
        }

        [Fact]
        public void BuildIOI_BoolArrayIndex0_MapsToZero()
        {
            var ioi = TagParser.BuildIOI("BoolArray[0]", CIPTypes.DWORD);

            int nameEnd = 2 + "BoolArray".Length;
            if ("BoolArray".Length % 2 != 0) nameEnd++;

            Assert.Equal(0x28, ioi[nameEnd]);
            Assert.Equal(0, ioi[nameEnd + 1]); // 0 / 32 = 0
        }

        [Fact]
        public void BuildIOI_BoolArrayIndex63_MapsToOne()
        {
            var ioi = TagParser.BuildIOI("BoolArray[63]", CIPTypes.DWORD);

            int nameEnd = 2 + "BoolArray".Length;
            if ("BoolArray".Length % 2 != 0) nameEnd++;

            Assert.Equal(0x28, ioi[nameEnd]);
            Assert.Equal(1, ioi[nameEnd + 1]); // 63 / 32 = 1
        }

        #endregion

        #region GetBitIndex Tests

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(31, 31)]
        [InlineData(32, 0)]
        [InlineData(33, 1)]
        [InlineData(63, 31)]
        [InlineData(64, 0)]
        [InlineData(100, 4)]
        public void GetBitIndex_ReturnsCorrectBit(int arrayIndex, int expectedBit)
        {
            int result = TagParser.GetBitIndex(arrayIndex);

            Assert.Equal(expectedBit, result);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Parse_EmptyString_ReturnsEmptyBaseTag()
        {
            var result = TagParser.Parse("");

            Assert.Equal("", result.BaseTag);
        }

        [Fact]
        public void Parse_OnlyProgramPrefix_HandlesGracefully()
        {
            // This is an invalid tag but shouldn't crash
            var result = TagParser.Parse("Program:Test");

            // When there's no dot, the full string stays as base tag
            Assert.Equal("Program:Test", result.BaseTag);
            Assert.Null(result.ProgramName);
        }

        [Fact]
        public void Parse_MultipleDots_ParsesAllMembers()
        {
            var result = TagParser.Parse("A.B.C.D.E");

            Assert.Equal("A", result.BaseTag);
            Assert.Equal(4, result.Members.Count);
        }

        [Fact]
        public void BuildIOI_EmptyTag_ReturnsMinimalIOI()
        {
            var ioi = TagParser.BuildIOI("");

            // Should still have symbolic segment header
            Assert.Equal(0x91, ioi[0]);
            Assert.Equal(0, ioi[1]); // Zero length name
        }

        #endregion
    }
}
