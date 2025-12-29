# CSLogix

A C# library for communicating with Allen Bradley PLCs over Ethernet/IP. CSLogix is a .NET port of the popular Python [pylogix](https://github.com/dmroeder/pylogix) library.

## Features

- **Read/Write Operations** - Support for all common data types (BOOL, SINT, INT, DINT, LINT, REAL, LREAL, STRING)
- **Array Support** - Read and write 1D, 2D, and 3D arrays
- **UDT Access** - Read and write User Defined Type members
- **Bit-Level Addressing** - Access individual bits within integers
- **Program-Scoped Tags** - Access tags within specific programs
- **Batch Operations** - Efficiently read/write multiple tags in a single request
- **Tag Discovery** - Retrieve the complete tag list from the PLC
- **Device Discovery** - Scan the network for EtherNet/IP devices
- **PLC Time** - Get and set the PLC wall clock time
- **Routing** - Support for multi-chassis configurations via CIP routing
- **Multi-Target** - Supports .NET Standard 2.0, .NET 6.0, and .NET 8.0

## Supported PLCs

- ControlLogix (L6x, L7x, L8x series)
- CompactLogix (L1x, L2x, L3x series)
- Micro800 series (with `Micro800 = true`)

## Installation

### NuGet Package (Coming Soon)

```bash
dotnet add package CSLogix
```

### Build from Source

```bash
git clone https://github.com/yourusername/cslogix.git
cd cslogix
dotnet build
```

## Quick Start

```csharp
using CSLogix;

// Create a connection to the PLC
using var plc = new PLC("192.168.1.10");

// Read a tag
var result = plc.Read("MyDINT");
if (result.Status == "Success")
{
    Console.WriteLine($"Value: {result.Value}");
}

// Write a tag
var writeResult = plc.Write("MyDINT", 12345);
Console.WriteLine($"Write status: {writeResult.Status}");
```

## Usage Examples

### Reading Tags

```csharp
using var plc = new PLC("192.168.1.10");

// Read different data types
var dintValue = plc.Read("MyDINT");
var realValue = plc.Read("MyREAL");
var boolValue = plc.Read("MyBOOL");
var stringValue = plc.Read("MyString");

// Read array elements
var element = plc.Read("MyArray[5]");
var multipleElements = plc.Read("MyArray[0]", 10);  // Read 10 elements

// Read UDT members
var udtMember = plc.Read("MyUDT.Counter");
var nestedMember = plc.Read("MyUDT.SubStruct.Value");

// Read program-scoped tags
var programTag = plc.Read("Program:MainProgram.LocalVar");

// Read bit from integer
var bit5 = plc.Read("MyDINT.5");
```

### Writing Tags

```csharp
using var plc = new PLC("192.168.1.10");

// Write different data types
plc.Write("MyDINT", 12345);
plc.Write("MyREAL", 3.14159f);
plc.Write("MyBOOL", true);
plc.Write("MyString", "Hello PLC!");

// Write array elements
plc.Write("MyArray[0]", 100);
plc.Write("MyArray[0]", new int[] { 10, 20, 30, 40, 50 });

// Write UDT members
plc.Write("MyUDT.Counter", 999);
```

### Batch Operations

```csharp
using var plc = new PLC("192.168.1.10");

// Read multiple tags in one request (more efficient)
var tags = new List<string> { "Tag1", "Tag2", "Tag3", "Tag4" };
var results = plc.Read(tags);

foreach (var result in results as List<Response>)
{
    Console.WriteLine($"{result.TagName}: {result.Value}");
}
```

### Tag Discovery

```csharp
using var plc = new PLC("192.168.1.10");

// Get all controller-scoped tags
var tagList = plc.GetTagList();
if (tagList.Status == "Success")
{
    foreach (var tag in tagList.Value as List<Tag>)
    {
        Console.WriteLine($"{tag.TagName} - {tag.DataType}");
    }
}

// Get program list
var programs = plc.GetProgramsList();

// Get tags for a specific program
var programTags = plc.GetProgramTagList("MainProgram");
```

### Device Discovery

```csharp
using var plc = new PLC("0.0.0.0");

// Discover all EtherNet/IP devices on the network
var devices = plc.Discover();
if (devices.Status == "Success")
{
    foreach (var device in devices.Value as List<Device>)
    {
        Console.WriteLine($"{device.IPAddress}: {device.ProductName}");
    }
}
```

### Configuration Options

```csharp
using var plc = new PLC("192.168.1.10")
{
    ProcessorSlot = 0,        // Slot number (default: 0)
    SocketTimeout = 5.0,      // Timeout in seconds (default: 5.0)
    Micro800 = false,         // Set true for Micro800 series
    ConnectionSize = 4002,    // Packet size (null for auto-negotiate)
};

// Routing through a gateway to another chassis
plc.Route = new object[]
{
    (1, 2),                   // Backplane port 1, slot 2
    (2, "192.168.2.100")      // Ethernet port 2, IP address
};
```

### PLC Time Operations

```csharp
using var plc = new PLC("192.168.1.10");

// Read PLC time
var time = plc.GetPLCTime();
Console.WriteLine($"PLC Time: {time.Value}");

// Set PLC time to current time
plc.SetPLCTime();

// Set PLC time to specific value
plc.SetPLCTime(new DateTime(2025, 1, 1, 12, 0, 0));
```

## API Reference

### PLC Class

| Property | Type | Description |
|----------|------|-------------|
| `IPAddress` | string | IP address of the PLC |
| `Port` | int | TCP port (default: 44818) |
| `ProcessorSlot` | int | Processor slot number (default: 0) |
| `SocketTimeout` | double | Socket timeout in seconds (default: 5.0) |
| `Micro800` | bool | Enable Micro800 mode (default: false) |
| `ConnectionSize` | int? | Connection size in bytes (null for auto) |
| `Route` | object[]? | CIP routing path |

| Method | Returns | Description |
|--------|---------|-------------|
| `Read(tag)` | Response | Read a single tag |
| `Read(tag, count)` | Response | Read multiple array elements |
| `Read(tags)` | Response | Batch read multiple tags |
| `Write(tag, value)` | Response | Write a single tag |
| `Write(tag, values)` | Response | Write multiple array elements |
| `GetTagList()` | Response | Get controller-scoped tags |
| `GetProgramTagList(program)` | Response | Get program-scoped tags |
| `GetProgramsList()` | Response | Get list of programs |
| `Discover()` | Response | Discover network devices |
| `GetDeviceProperties()` | Response | Get device identity |
| `GetPLCTime()` | Response | Read PLC wall clock |
| `SetPLCTime(time?)` | Response | Set PLC wall clock |
| `Close()` | void | Close the connection |

### Response Class

| Property | Type | Description |
|----------|------|-------------|
| `TagName` | string? | Name of the tag |
| `Value` | object? | Value read or written |
| `Status` | string | Status message ("Success" or error) |

## Building and Testing

### Build

```bash
dotnet build CSLogix.sln
```

### Run Tests

```bash
dotnet test CSLogix.sln
```

### Build Examples

```bash
dotnet build examples/BasicExample/BasicExample.csproj
dotnet build examples/AdvancedExample/AdvancedExample.csproj
```

## Project Structure

```
cslogix/
├── src/CSLogix/           # Main library
│   ├── PLC.cs             # Primary API class
│   ├── Connection.cs      # EtherNet/IP protocol handling
│   ├── Constants/         # CIP service and type codes
│   ├── Helpers/           # Tag parsing utilities
│   └── Models/            # Data models (Response, Tag, Device, UDT)
├── tests/CSLogix.Tests/   # Unit and integration tests
└── examples/              # Example applications
    ├── BasicExample/      # Basic read/write operations
    └── AdvancedExample/   # Advanced features demo
```

## Error Handling

All operations return a `Response` object with a `Status` property:

```csharp
var result = plc.Read("MyTag");

if (result.Status == "Success")
{
    // Operation succeeded
    Console.WriteLine(result.Value);
}
else
{
    // Operation failed
    Console.WriteLine($"Error: {result.Status}");
}
```

Common status messages:
- `"Success"` - Operation completed successfully
- `"Connection failure"` - Could not connect to PLC
- `"Path segment error"` - Invalid tag path
- `"Object does not exist"` - Tag not found
- `"Service not supported"` - Operation not supported

## Acknowledgments

This library is a C# port of [pylogix](https://github.com/dmroeder/pylogix) by Dustin Roeder. Special thanks to the pylogix contributors for their excellent work on the original Python implementation.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request
