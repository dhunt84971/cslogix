using System;
using Xunit;
using CSLogix.Models;

namespace CSLogix.Tests.Models
{
    public class ResponseTests
    {
        [Fact]
        public void Constructor_WithStringStatus_SetsStatusDirectly()
        {
            var response = new Response("TestTag", 123, "Success");

            Assert.Equal("TestTag", response.TagName);
            Assert.Equal(123, response.Value);
            Assert.Equal("Success", response.Status);
        }

        [Fact]
        public void Constructor_WithByteStatus_LooksUpErrorCode()
        {
            var response = new Response("TestTag", 456, (byte)0x00);

            Assert.Equal("Success", response.Status);
        }

        [Fact]
        public void Constructor_WithIntStatus_LooksUpErrorCode()
        {
            var response = new Response("TestTag", 789, 0x08);

            Assert.Equal("Service not supported", response.Status);
        }

        [Fact]
        public void Constructor_WithNullTagName_AcceptsNull()
        {
            var response = new Response(null, "value", "Success");

            Assert.Null(response.TagName);
            Assert.Equal("value", response.Value);
        }

        [Fact]
        public void Constructor_WithNullValue_AcceptsNull()
        {
            var response = new Response("Tag", null, "Success");

            Assert.Null(response.Value);
        }

        [Theory]
        [InlineData(0x00, "Success")]
        [InlineData(0x01, "Connection failure")]
        [InlineData(0x04, "Path segment error")]
        [InlineData(0x08, "Service not supported")]
        [InlineData(0x16, "Object does not exist")]
        [InlineData(0x20, "Invalid Parameter")]
        [InlineData(0x26, "Path size invalid")]
        public void GetErrorCode_WithKnownByte_ReturnsCorrectMessage(byte code, string expected)
        {
            string result = Response.GetErrorCode(code);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetErrorCode_WithUnknownByte_ReturnsUnknownErrorMessage()
        {
            string result = Response.GetErrorCode((byte)0xFF);

            Assert.Equal("Unknown error 255", result);
        }

        [Fact]
        public void GetErrorCode_WithString_ReturnsStringDirectly()
        {
            string result = Response.GetErrorCode("Custom error message");

            Assert.Equal("Custom error message", result);
        }

        [Fact]
        public void GetErrorCode_WithUnknownType_ReturnsUnknownError()
        {
            string result = Response.GetErrorCode(12.5);

            Assert.Equal("Unknown error 12.5", result);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var response = new Response("MyTag", 100, "Success");

            string result = response.ToString();

            Assert.Equal("MyTag 100 Success", result);
        }

        [Fact]
        public void CipErrorCodes_ContainsExpectedCodes()
        {
            Assert.True(Response.CipErrorCodes.ContainsKey(0x00));
            Assert.True(Response.CipErrorCodes.ContainsKey(0x01));
            Assert.True(Response.CipErrorCodes.ContainsKey(0x2C));
            Assert.Equal("Success", Response.CipErrorCodes[0x00]);
            Assert.Equal("Attribute not gettable", Response.CipErrorCodes[0x2C]);
        }

        [Fact]
        public void Constructor_WithArrayValue_AcceptsArray()
        {
            int[] values = { 1, 2, 3, 4, 5 };
            var response = new Response("ArrayTag", values, "Success");

            Assert.Equal(values, response.Value);
        }
    }
}
