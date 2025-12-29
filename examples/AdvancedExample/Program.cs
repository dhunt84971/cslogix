using System;
using System.Collections.Generic;
using System.Linq;
using CSLogix;
using CSLogix.Models;

namespace AdvancedExample
{
    /// <summary>
    /// Advanced example demonstrating batch operations, tag discovery, and device management.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string ipAddress = args.Length > 0 ? args[0] : "192.168.1.10";

            Console.WriteLine($"CSLogix Advanced Example");
            Console.WriteLine($"Target PLC: {ipAddress}");
            Console.WriteLine();

            // =====================
            // DEVICE DISCOVERY
            // =====================
            Console.WriteLine("=== Device Discovery ===");
            DiscoverDevices();
            Console.WriteLine();

            using var plc = new PLC(ipAddress);

            // =====================
            // BATCH READ OPERATIONS
            // =====================
            Console.WriteLine("=== Batch Read Operations ===");
            BatchReadExample(plc);
            Console.WriteLine();

            // =====================
            // TAG LIST RETRIEVAL
            // =====================
            Console.WriteLine("=== Tag List Retrieval ===");
            GetTagListExample(plc);
            Console.WriteLine();

            // =====================
            // PROGRAM LIST
            // =====================
            Console.WriteLine("=== Program List ===");
            GetProgramsExample(plc);
            Console.WriteLine();

            // =====================
            // PLC TIME OPERATIONS
            // =====================
            Console.WriteLine("=== PLC Time Operations ===");
            TimeOperationsExample(plc);
            Console.WriteLine();

            // =====================
            // MODULE DISCOVERY
            // =====================
            Console.WriteLine("=== Module Discovery ===");
            GetModulesExample(plc);
            Console.WriteLine();

            // =====================
            // ROUTING EXAMPLE
            // =====================
            Console.WriteLine("=== Routing Example ===");
            RoutingExample(ipAddress);
            Console.WriteLine();

            Console.WriteLine("Advanced example complete!");
        }

        static void DiscoverDevices()
        {
            Console.WriteLine("Scanning for EtherNet/IP devices on the network...");

            // Create a temporary PLC instance for discovery
            using var plc = new PLC("0.0.0.0");

            var devices = plc.Discover();
            if (devices.Status == "Success" && devices.Value is List<Device> deviceList)
            {
                Console.WriteLine($"Found {deviceList.Count} device(s):");
                foreach (var device in deviceList)
                {
                    Console.WriteLine($"  - {device.IPAddress}: {device.ProductName}");
                    Console.WriteLine($"    Vendor: {device.Vendor}");
                    Console.WriteLine($"    Type: {device.DeviceType}");
                    Console.WriteLine($"    Revision: {device.Revision}");
                    Console.WriteLine($"    Serial: {device.SerialNumber}");
                }
            }
            else
            {
                Console.WriteLine($"Discovery failed: {devices.Status}");
            }
        }

        static void BatchReadExample(PLC plc)
        {
            // Read multiple tags in a single operation (more efficient than individual reads)
            var tagNames = new List<string>
            {
                "Counter",
                "Temperature",
                "Pressure",
                "Speed",
                "RunStatus"
            };

            Console.WriteLine($"Reading {tagNames.Count} tags in batch...");
            var results = plc.Read(tagNames);

            if (results is List<Response> responses)
            {
                foreach (var response in responses)
                {
                    if (response.Status == "Success")
                    {
                        Console.WriteLine($"  {response.TagName}: {FormatValue(response.Value)}");
                    }
                    else
                    {
                        Console.WriteLine($"  {response.TagName}: ERROR - {response.Status}");
                    }
                }
            }
        }

        static string FormatValue(object? value)
        {
            if (value == null)
                return "null";

            if (value is Array array)
            {
                var elements = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    elements[i] = array.GetValue(i)?.ToString() ?? "null";
                }
                return $"[{string.Join(", ", elements)}]";
            }

            return value.ToString() ?? "null";
        }

        static void GetTagListExample(PLC plc)
        {
            Console.WriteLine("Retrieving controller-scoped tag list...");

            var result = plc.GetTagList();
            if (result.Status == "Success" && result.Value is List<Tag> tags)
            {
                Console.WriteLine($"Found {tags.Count} controller-scoped tags:");

                // Show first 10 tags
                foreach (var tag in tags.Take(10))
                {
                    string arrayInfo = tag.Array > 0 ? $"[{tag.Size}]" : "";
                    string structInfo = tag.Struct > 0 ? " (UDT)" : "";
                    Console.WriteLine($"  {tag.TagName}{arrayInfo}{structInfo}");
                }

                if (tags.Count > 10)
                {
                    Console.WriteLine($"  ... and {tags.Count - 10} more tags");
                }
            }
            else
            {
                Console.WriteLine($"Failed to get tag list: {result.Status}");
            }
        }

        static void GetProgramsExample(PLC plc)
        {
            Console.WriteLine("Retrieving program list...");

            var result = plc.GetProgramsList();
            if (result.Status == "Success" && result.Value is List<string> programs)
            {
                Console.WriteLine($"Found {programs.Count} program(s):");
                foreach (var program in programs)
                {
                    Console.WriteLine($"  - {program}");

                    // Get tags for this program
                    var programTags = plc.GetProgramTagList(program);
                    if (programTags.Status == "Success" && programTags.Value is List<Tag> tags)
                    {
                        Console.WriteLine($"    ({tags.Count} program-scoped tags)");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to get program list: {result.Status}");
            }
        }

        static void TimeOperationsExample(PLC plc)
        {
            // Read PLC time
            Console.WriteLine("Reading PLC time...");
            var timeResult = plc.GetPLCTime();
            if (timeResult.Status == "Success")
            {
                Console.WriteLine($"  PLC Time: {timeResult.Value}");
            }
            else
            {
                Console.WriteLine($"  Failed to read PLC time: {timeResult.Status}");
            }

            // Set PLC time (uncomment to actually set)
            // Console.WriteLine("Setting PLC time to current time...");
            // var setResult = plc.SetPLCTime();
            // Console.WriteLine($"  Result: {setResult.Status}");
        }

        static void GetModulesExample(PLC plc)
        {
            Console.WriteLine("Getting module information...");

            // Get properties of the device at IP address
            var deviceProps = plc.GetDeviceProperties();
            if (deviceProps.Status == "Success" && deviceProps.Value is Device device)
            {
                Console.WriteLine("Device Properties:");
                Console.WriteLine($"  Vendor: {device.Vendor}");
                Console.WriteLine($"  Product: {device.ProductName}");
                Console.WriteLine($"  Type: {device.DeviceType}");
                Console.WriteLine($"  Revision: {device.Revision}");
                Console.WriteLine($"  Serial: {device.SerialNumber}");
            }
            else
            {
                Console.WriteLine($"Failed to get device properties: {deviceProps.Status}");
            }
        }

        static void RoutingExample(string ipAddress)
        {
            Console.WriteLine("Demonstrating routing to a remote PLC...");

            // Example: Route through a ControlLogix gateway to reach another PLC
            // This routes from the gateway (ipAddress) through backplane port 1
            // to a module in slot 2
            using var plc = new PLC(ipAddress)
            {
                // Route: Port 1 (backplane), Slot 2
                Route = new object[] { (1, 2) }
            };

            // Now reads/writes will be routed to the module in slot 2
            var result = plc.Read("RemoteTag");
            Console.WriteLine($"  Routed read result: {result.Status}");

            // Example of more complex routing (through Ethernet bridge)
            // Route: Backplane to slot 1, then Ethernet to 192.168.2.100
            /*
            plc.Route = new object[]
            {
                (1, 1),                    // Port 1, Slot 1 (Ethernet module)
                (2, "192.168.2.100")       // Port 2, IP address
            };
            */
        }
    }
}
