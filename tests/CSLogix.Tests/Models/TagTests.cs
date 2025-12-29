using System;
using System.Text;
using Xunit;
using CSLogix.Models;

namespace CSLogix.Tests.Models
{
    public class TagTests
    {
        [Fact]
        public void DefaultConstructor_InitializesProperties()
        {
            var tag = new Tag();

            Assert.Equal(string.Empty, tag.TagName);
            Assert.Equal(0, tag.InstanceID);
            Assert.Equal(0, tag.SymbolType);
            Assert.Equal(0, tag.DataTypeValue);
            Assert.Equal(string.Empty, tag.DataType);
            Assert.Equal(0, tag.Array);
            Assert.Equal(0, tag.Struct);
            Assert.Equal(0, tag.Size);
        }

        [Theory]
        [InlineData("__hidden", true)]
        [InlineData("Routine:MainRoutine", true)]
        [InlineData("Map:Something", true)]
        [InlineData("Task:MainTask", true)]
        [InlineData("UDI:UserDefinedInterface", true)]
        [InlineData("MyTag", false)]
        [InlineData("Program:Main.MyTag", false)]
        [InlineData("ValidTag__WithUnderscores", true)]
        public void InFilter_ReturnsExpectedResult(string tagName, bool expected)
        {
            bool result = Tag.InFilter(tagName);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Parse_WithSimpleTag_ParsesCorrectly()
        {
            // Build a mock packet for a simple DINT tag
            // Offset 0: InstanceID (2 bytes)
            // Offset 4: Name length (2 bytes)
            // Offset 6: Name string
            // After name: Type info (2 bytes)

            var packet = new byte[20];
            BitConverter.GetBytes((ushort)100).CopyTo(packet, 0); // InstanceID = 100
            BitConverter.GetBytes((ushort)7).CopyTo(packet, 4);   // Name length = 7
            Encoding.UTF8.GetBytes("MyDINT1").CopyTo(packet, 6);  // Tag name
            BitConverter.GetBytes((ushort)0x00C4).CopyTo(packet, 13); // DINT type (0xC4), not array, not struct

            var tag = Tag.Parse(packet);

            Assert.Equal("MyDINT1", tag.TagName);
            Assert.Equal(100, tag.InstanceID);
            Assert.Equal(0xC4, tag.DataTypeValue);
            Assert.Equal(0, tag.Array);
            Assert.Equal(0, tag.Struct);
            Assert.Equal(0, tag.Size);
        }

        [Fact]
        public void Parse_WithProgramScope_IncludesProgramName()
        {
            var packet = new byte[20];
            BitConverter.GetBytes((ushort)50).CopyTo(packet, 0);
            BitConverter.GetBytes((ushort)5).CopyTo(packet, 4);
            Encoding.UTF8.GetBytes("Count").CopyTo(packet, 6);
            BitConverter.GetBytes((ushort)0x00C4).CopyTo(packet, 11);

            var tag = Tag.Parse(packet, "Program:MainProgram");

            Assert.Equal("Program:MainProgram.Count", tag.TagName);
        }

        [Fact]
        public void Parse_WithArray_SetsArrayFlagsAndSize()
        {
            // Build packet with array flag set
            var packet = new byte[20];
            BitConverter.GetBytes((ushort)200).CopyTo(packet, 0);  // InstanceID
            BitConverter.GetBytes((ushort)6).CopyTo(packet, 4);    // Name length
            Encoding.UTF8.GetBytes("MyArr1").CopyTo(packet, 6);    // Tag name
            // Type info: 0x6000 sets array dimension bits, 0x00C4 is DINT
            BitConverter.GetBytes((ushort)0x20C4).CopyTo(packet, 12);
            BitConverter.GetBytes((ushort)10).CopyTo(packet, 14);  // Array size = 10

            var tag = Tag.Parse(packet);

            Assert.Equal(1, tag.Array); // 1D array
            Assert.Equal(10, tag.Size);
        }

        [Fact]
        public void Parse_WithStruct_SetsStructFlag()
        {
            var packet = new byte[20];
            BitConverter.GetBytes((ushort)300).CopyTo(packet, 0);
            BitConverter.GetBytes((ushort)5).CopyTo(packet, 4);
            Encoding.UTF8.GetBytes("MyUDT").CopyTo(packet, 6);
            // Type info: 0x8000 sets struct flag
            BitConverter.GetBytes((ushort)0x80A0).CopyTo(packet, 11);

            var tag = Tag.Parse(packet);

            Assert.Equal(1, tag.Struct);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var tag = new Tag
            {
                TagName = "TestTag",
                InstanceID = 123,
                SymbolType = 0xC4,
                DataTypeValue = 0x00C4,
                DataType = "DINT",
                Array = 1,
                Struct = 0,
                Size = 10
            };

            string result = tag.ToString();

            Assert.Contains("TestTag", result);
            Assert.Contains("123", result);
            Assert.Contains("Array=1", result);
            Assert.Contains("Size=10", result);
        }

        [Fact]
        public void UDT_Property_CanBeSet()
        {
            var udt = new UDT { Name = "MyUDT", Type = 0x1234 };
            var tag = new Tag { UDT = udt };

            Assert.NotNull(tag.UDT);
            Assert.Equal("MyUDT", tag.UDT.Name);
        }

        [Fact]
        public void OptionalProperties_CanBeSet()
        {
            var tag = new Tag
            {
                AccessRight = 1,
                Internal = true,
                Meta = 42,
                Scope0 = 0,
                Scope1 = 1,
                Bytes = new byte[] { 0x01, 0x02 }
            };

            Assert.Equal(1, tag.AccessRight);
            Assert.True(tag.Internal);
            Assert.Equal(42, tag.Meta);
            Assert.Equal(0, tag.Scope0);
            Assert.Equal(1, tag.Scope1);
            Assert.Equal(2, tag.Bytes!.Length);
        }
    }
}
