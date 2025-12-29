using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CSLogix.Models
{
    /// <summary>
    /// Represents an EtherNet/IP device discovered on the network or
    /// a module in a ControlLogix backplane.
    /// </summary>
    public class Device
    {
        /// <summary>
        /// Gets or sets the length of the device identity response.
        /// </summary>
        public ushort Length { get; set; }

        /// <summary>
        /// Gets or sets the encapsulation protocol version.
        /// </summary>
        public ushort EncapsulationVersion { get; set; }

        /// <summary>
        /// Gets or sets the IP address of the device.
        /// </summary>
        public string? IPAddress { get; set; }

        /// <summary>
        /// Gets or sets the vendor ID.
        /// </summary>
        public ushort VendorID { get; set; }

        /// <summary>
        /// Gets or sets the vendor name (looked up from VendorID).
        /// </summary>
        public string? Vendor { get; set; }

        /// <summary>
        /// Gets or sets the device type ID.
        /// </summary>
        public ushort DeviceID { get; set; }

        /// <summary>
        /// Gets or sets the device type name (looked up from DeviceID).
        /// </summary>
        public string? DeviceType { get; set; }

        /// <summary>
        /// Gets or sets the product code.
        /// </summary>
        public ushort ProductCode { get; set; }

        /// <summary>
        /// Gets or sets the firmware revision (e.g., "32.11").
        /// </summary>
        public string? Revision { get; set; }

        /// <summary>
        /// Gets or sets the device status word.
        /// </summary>
        public ushort Status { get; set; }

        /// <summary>
        /// Gets or sets the device serial number (as hex string).
        /// </summary>
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the length of the product name.
        /// </summary>
        public byte ProductNameLength { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Gets or sets the device state.
        /// </summary>
        public byte State { get; set; }

        /// <summary>
        /// Parses a device from a raw ListIdentity response packet.
        /// </summary>
        /// <param name="data">The raw packet data.</param>
        /// <param name="ipAddress">Optional IP address override.</param>
        /// <returns>A new Device instance.</returns>
        public static Device Parse(byte[] data, string? ipAddress = null)
        {
            var device = new Device();

            // Length at offset 28
            device.Length = BitConverter.ToUInt16(data, 28);

            // Encapsulation version at offset 30
            device.EncapsulationVersion = BitConverter.ToUInt16(data, 30);

            // IP address at offset 36 (as 4-byte little-endian)
            if (!string.IsNullOrEmpty(ipAddress))
            {
                device.IPAddress = ipAddress;
            }
            else
            {
                uint longIp = BitConverter.ToUInt32(data, 36);
                byte[] ipBytes = BitConverter.GetBytes(longIp);
                device.IPAddress = new IPAddress(ipBytes).ToString();
            }

            // Vendor ID at offset 48
            device.VendorID = BitConverter.ToUInt16(data, 48);
            device.Vendor = GetVendor(device.VendorID);

            // Device type ID at offset 50
            device.DeviceID = BitConverter.ToUInt16(data, 50);
            device.DeviceType = GetDeviceType(device.DeviceID);

            // Product code at offset 52
            device.ProductCode = BitConverter.ToUInt16(data, 52);

            // Revision at offsets 54 (major) and 55 (minor)
            byte major = data[54];
            byte minor = data[55];
            device.Revision = $"{major}.{minor}";

            // Status at offset 56
            device.Status = BitConverter.ToUInt16(data, 56);

            // Serial number at offset 58
            uint serial = BitConverter.ToUInt32(data, 58);
            device.SerialNumber = $"0x{serial:X8}";

            // Product name length at offset 62
            device.ProductNameLength = data[62];

            // Product name starting at offset 63
            if (device.ProductNameLength > 0 && data.Length >= 63 + device.ProductNameLength)
            {
                device.ProductName = Encoding.UTF8.GetString(data, 63, device.ProductNameLength);
            }

            // State at the last byte
            if (data.Length > 0)
            {
                device.State = data[data.Length - 1];
            }

            return device;
        }

        /// <summary>
        /// Gets the vendor name from a vendor ID.
        /// </summary>
        public static string GetVendor(ushort vendorId)
        {
            if (Vendors.TryGetValue(vendorId, out var vendor))
            {
                return vendor;
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the device type name from a device type ID.
        /// </summary>
        public static string GetDeviceType(ushort deviceId)
        {
            if (DeviceTypes.TryGetValue(deviceId, out var deviceType))
            {
                return deviceType;
            }
            return "Unknown";
        }

        /// <summary>
        /// Returns a string representation of the device.
        /// </summary>
        public override string ToString()
        {
            return $"{IPAddress}: {ProductName} ({Vendor})";
        }

        /// <summary>
        /// Device type ID mappings (from CIP specification).
        /// </summary>
        public static readonly Dictionary<ushort, string> DeviceTypes = new Dictionary<ushort, string>
        {
            { 0x00, "Generic Device (deprecated)" },
            { 0x02, "AC Drive" },
            { 0x03, "Motor Overload" },
            { 0x04, "Limit Switch" },
            { 0x05, "Inductive Proximity Switch" },
            { 0x06, "Photoelectric Sensor" },
            { 0x07, "General Purpose Discrete I/O" },
            { 0x09, "Resolver" },
            { 0x0C, "Communications Adapter" },
            { 0x0E, "Programmable Logic Controller" },
            { 0x10, "Position Controller" },
            { 0x13, "DC Drive" },
            { 0x15, "Contactor" },
            { 0x16, "Motor Starter" },
            { 0x17, "Soft Start" },
            { 0x18, "Human-Machine Interface" },
            { 0x1A, "Mass Flow Controller" },
            { 0x1B, "Pneumatic Valve" },
            { 0x1C, "Vacuum Pressure Gauge" },
            { 0x1D, "Process Control Value" },
            { 0x1E, "Residual Gas Analyzer" },
            { 0x1F, "DC Power Generator" },
            { 0x20, "RF Power Generator" },
            { 0x21, "Turbomolecular Vacuum Pump" },
            { 0x22, "Encoder" },
            { 0x23, "Safety Discrete I/O Device" },
            { 0x24, "Fluid Flow Controller" },
            { 0x25, "CIP Motion Drive" },
            { 0x26, "CompoNet Repeater" },
            { 0x27, "Mass Flow Controller, Enhanced" },
            { 0x28, "CIP Modbus Device" },
            { 0x29, "CIP Modbus Translator" },
            { 0x2A, "Safety Analog I/O Device" },
            { 0x2B, "Generic Device (keyable)" },
            { 0x2C, "Managed Switch" },
            { 0x32, "ControlNet Physical Layer Component" }
        };

        /// <summary>
        /// Vendor ID mappings (common vendors).
        /// </summary>
        public static readonly Dictionary<ushort, string> Vendors = new Dictionary<ushort, string>
        {
            { 0x0001, "Rockwell Automation/Allen-Bradley" },
            { 0x0002, "Namco Controls Corp." },
            { 0x0003, "Honeywell Inc." },
            { 0x0004, "Parker Hannifin Corp." },
            { 0x0005, "Rockwell Automation/Reliance Electric" },
            { 0x0006, "Reserved" },
            { 0x0007, "SMC Corporation" },
            { 0x0008, "Molex Incorporated" },
            { 0x0009, "Western Reserve Controls" },
            { 0x000A, "Advanced Micro Controls Inc." },
            { 0x000B, "ASCO Pneumatic Controls" },
            { 0x000C, "Banner Engineering Corp." },
            { 0x000D, "Belden Wire & Cable Company" },
            { 0x000E, "Cooper Interconnect" },
            { 0x000F, "Reserved" },
            { 0x0010, "Daniel Woodhead Co." },
            { 0x0011, "Dearborn Group Inc." },
            { 0x0012, "Reserved" },
            { 0x0013, "Helm Instrument Co." },
            { 0x0014, "Huron Net Works" },
            { 0x0015, "Lumberg Inc." },
            { 0x0016, "Online Development Inc." },
            { 0x0017, "Vorne Industries" },
            { 0x0018, "ODVA Special Reserve" },
            { 0x0019, "Reserved" },
            { 0x001A, "Rockwell Automation/FactoryTalk" },
            { 0x001B, "Reserved" },
            { 0x001C, "Reserved" },
            { 0x001D, "Reserved" },
            { 0x001E, "Reserved" },
            { 0x001F, "Reserved" },
            { 0x0020, "Prosoft Technology" },
            { 0x0021, "Reserved" },
            { 0x0022, "Pyramid Solutions" },
            { 0x0058, "Siemens Energy & Automation" },
            { 0x0137, "Schneider Electric" },
            { 0x0168, "ABB Robotics" },
            { 0x01A0, "Beckhoff Automation" },
            { 0x01EE, "Omron Corporation" },
            { 0x022B, "Mitsubishi Electric" },
            { 0x030D, "Eaton Corporation" },
            { 0x0338, "Phoenix Contact" },
            { 0x0339, "B&R Industrial Automation" },
            { 0x034E, "Festo SE & Co. KG" },
            { 0x034F, "Emerson Electric" }
        };
    }
}
