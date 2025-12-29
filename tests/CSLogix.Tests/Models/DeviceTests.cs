using System;
using System.Text;
using Xunit;
using CSLogix.Models;

namespace CSLogix.Tests.Models
{
    public class DeviceTests
    {
        [Fact]
        public void GetVendor_WithKnownId_ReturnsVendorName()
        {
            string result = Device.GetVendor(0x0001);

            Assert.Equal("Rockwell Automation/Allen-Bradley", result);
        }

        [Fact]
        public void GetVendor_WithUnknownId_ReturnsUnknown()
        {
            string result = Device.GetVendor(0xFFFF);

            Assert.Equal("Unknown", result);
        }

        [Theory]
        [InlineData(0x0001, "Rockwell Automation/Allen-Bradley")]
        [InlineData(0x0003, "Honeywell Inc.")]
        [InlineData(0x0058, "Siemens Energy & Automation")]
        [InlineData(0x01EE, "Omron Corporation")]
        public void GetVendor_WithVariousIds_ReturnsCorrectVendor(ushort vendorId, string expected)
        {
            string result = Device.GetVendor(vendorId);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetDeviceType_WithKnownId_ReturnsDeviceTypeName()
        {
            string result = Device.GetDeviceType(0x0E);

            Assert.Equal("Programmable Logic Controller", result);
        }

        [Fact]
        public void GetDeviceType_WithUnknownId_ReturnsUnknown()
        {
            string result = Device.GetDeviceType(0xFF);

            Assert.Equal("Unknown", result);
        }

        [Theory]
        [InlineData(0x02, "AC Drive")]
        [InlineData(0x0C, "Communications Adapter")]
        [InlineData(0x0E, "Programmable Logic Controller")]
        [InlineData(0x18, "Human-Machine Interface")]
        [InlineData(0x25, "CIP Motion Drive")]
        public void GetDeviceType_WithVariousIds_ReturnsCorrectType(ushort deviceId, string expected)
        {
            string result = Device.GetDeviceType(deviceId);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Parse_WithValidPacket_ParsesAllFields()
        {
            // Build a mock ListIdentity response packet
            var packet = new byte[80];

            // Length at offset 28
            BitConverter.GetBytes((ushort)48).CopyTo(packet, 28);

            // Encapsulation version at offset 30
            BitConverter.GetBytes((ushort)1).CopyTo(packet, 30);

            // IP address at offset 36 (192.168.1.100 = 0xC0A80164)
            BitConverter.GetBytes((uint)0x6401A8C0).CopyTo(packet, 36);

            // Vendor ID at offset 48 (Allen-Bradley)
            BitConverter.GetBytes((ushort)0x0001).CopyTo(packet, 48);

            // Device type ID at offset 50 (PLC)
            BitConverter.GetBytes((ushort)0x0E).CopyTo(packet, 50);

            // Product code at offset 52
            BitConverter.GetBytes((ushort)55).CopyTo(packet, 52);

            // Revision at offsets 54-55
            packet[54] = 32; // Major
            packet[55] = 11; // Minor

            // Status at offset 56
            BitConverter.GetBytes((ushort)0x0030).CopyTo(packet, 56);

            // Serial number at offset 58
            BitConverter.GetBytes((uint)0xABCD1234).CopyTo(packet, 58);

            // Product name length at offset 62
            packet[62] = 12;

            // Product name starting at offset 63
            Encoding.UTF8.GetBytes("1756-L75/B K").CopyTo(packet, 63);

            // State at last byte
            packet[79] = 0xFF;

            var device = Device.Parse(packet);

            Assert.Equal(48, device.Length);
            Assert.Equal(1, device.EncapsulationVersion);
            Assert.Equal("192.168.1.100", device.IPAddress);
            Assert.Equal(0x0001, device.VendorID);
            Assert.Equal("Rockwell Automation/Allen-Bradley", device.Vendor);
            Assert.Equal(0x0E, device.DeviceID);
            Assert.Equal("Programmable Logic Controller", device.DeviceType);
            Assert.Equal(55, device.ProductCode);
            Assert.Equal("32.11", device.Revision);
            Assert.Equal(0x0030, device.Status);
            Assert.Equal("0xABCD1234", device.SerialNumber);
            Assert.Equal(12, device.ProductNameLength);
            Assert.Equal("1756-L75/B K", device.ProductName);
            Assert.Equal(0xFF, device.State);
        }

        [Fact]
        public void Parse_WithIpAddressOverride_UsesProvidedAddress()
        {
            var packet = new byte[80];
            // Set minimal required fields
            BitConverter.GetBytes((ushort)48).CopyTo(packet, 28);
            packet[62] = 0; // No product name

            var device = Device.Parse(packet, "10.0.0.1");

            Assert.Equal("10.0.0.1", device.IPAddress);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var device = new Device
            {
                IPAddress = "192.168.1.100",
                ProductName = "1756-L75/B",
                Vendor = "Rockwell Automation/Allen-Bradley"
            };

            string result = device.ToString();

            Assert.Equal("192.168.1.100: 1756-L75/B (Rockwell Automation/Allen-Bradley)", result);
        }

        [Fact]
        public void Vendors_Dictionary_ContainsExpectedEntries()
        {
            Assert.True(Device.Vendors.Count > 30);
            Assert.True(Device.Vendors.ContainsKey(0x0001));
            Assert.True(Device.Vendors.ContainsKey(0x0058));
        }

        [Fact]
        public void DeviceTypes_Dictionary_ContainsExpectedEntries()
        {
            Assert.True(Device.DeviceTypes.Count > 20);
            Assert.True(Device.DeviceTypes.ContainsKey(0x0E));
            Assert.True(Device.DeviceTypes.ContainsKey(0x18));
        }

        [Fact]
        public void Parse_WithShortPacket_HandlesGracefully()
        {
            // Create packet that is too short for product name
            var packet = new byte[64];
            BitConverter.GetBytes((ushort)20).CopyTo(packet, 28);
            packet[62] = 50; // Product name length longer than available data

            // Should not throw, just won't have product name
            var device = Device.Parse(packet);

            Assert.Null(device.ProductName);
        }
    }
}
