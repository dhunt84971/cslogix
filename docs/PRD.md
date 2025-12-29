# CSLogix - Product Requirements Document (PRD)

## 1. Overview

### 1.1 Product Name
**CSLogix** - A C# .NET Core Library for Allen Bradley PLC Communication

### 1.2 Purpose
CSLogix is a C# library that enables Ethernet/IP (EtherNet Industrial Protocol) based communication with Allen Bradley PLCs. The library allows .NET Core applications to read/write tag values, retrieve tag lists, discover devices, and perform other PLC operations.

### 1.3 Reference Implementation
This library is based on the Python library **pylogix** (version 1.1.4), originally created by Burt Peterson and maintained by Dustin Roeder. The C# implementation will maintain the same function names and behavior while following C# conventions and best practices.

### 1.4 Supported PLC Models
- **ControlLogix** (RSLogix5000/Studio5000)
- **CompactLogix** (RSLogix5000/Studio5000)
- **Micro800 Series** (Connected Components Workbench)
- **RSEmulate** (with additional configuration)

**Not Supported:**
- PLC5, SLC, MicroLogix (use different protocol)

---

## 2. Technical Requirements

### 2.1 Platform Requirements
- **.NET Core 6.0+** or **.NET Standard 2.0+**
- Cross-platform support (Windows, Linux, macOS)
- No external dependencies (pure .NET implementation)

### 2.2 Communication Protocol
- **EtherNet/IP (Ethernet Industrial Protocol)**
- Default port: **44818**
- TCP/IP socket-based communication
- Support for both connected and unconnected messaging

### 2.3 Connection Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| IPAddress | string | "" | PLC IP address |
| Port | int | 44818 | EtherNet/IP port |
| ProcessorSlot | int | 0 | Slot number for ControlLogix |
| SocketTimeout | double | 5.0 | Connection timeout in seconds |
| Micro800 | bool | false | Flag for Micro800 series PLCs |
| Route | tuple[] | null | Custom routing path |
| ConnectionSize | int | 508/4002 | Packet size (auto-negotiated) |

---

## 3. Data Types

### 3.1 CIP Data Types
The library must support the following CIP (Common Industrial Protocol) data types:

| Type Code | Size (bytes) | Name | C# Type | Format |
|-----------|--------------|------|---------|--------|
| 0xC1 | 1 | BOOL | bool | Little-endian |
| 0xC2 | 1 | SINT | sbyte | Little-endian |
| 0xC3 | 2 | INT | short | Little-endian |
| 0xC4 | 4 | DINT | int | Little-endian |
| 0xC5 | 8 | LINT | long | Little-endian |
| 0xC6 | 1 | USINT | byte | Little-endian |
| 0xC7 | 2 | UINT | ushort | Little-endian |
| 0xC8 | 4 | UDINT | uint | Little-endian |
| 0xC9 | 8 | LWORD | ulong | Little-endian |
| 0xCA | 4 | REAL | float | Little-endian |
| 0xCB | 8 | LREAL | double | Little-endian |
| 0xD1 | 1 | BYTE | byte | Little-endian |
| 0xD2 | 2 | WORD | ushort | Little-endian |
| 0xD3 | 4 | DWORD | uint | Little-endian (bool array) |
| 0xDA | 1 | STRING | string | Special format |
| 0xA0 | 88 | STRUCT | byte[] | UDT/String structure |
| 0xC0 | 8 | DT | DateTime | Microseconds since epoch |

### 3.2 String Types
- **Standard STRING**: 82-character max, 88-byte structure (4-byte length + 84 bytes data)
- **Short STRING (0xDA)**: Variable length with 1-byte length prefix
- **Special STRING (0xD0)**: Variable length with 2-byte length prefix

---

## 4. Core Classes

### 4.1 PLC Class (Main Entry Point)
The primary class for PLC communication, implementing `IDisposable` for resource cleanup.

```csharp
public class PLC : IDisposable
{
    // Properties
    public string IPAddress { get; set; }
    public int Port { get; set; }
    public int ProcessorSlot { get; set; }
    public double SocketTimeout { get; set; }
    public bool Micro800 { get; set; }
    public object[] Route { get; set; }
    public int ConnectionSize { get; set; }
    public string StringEncoding { get; set; }
}
```

### 4.2 Response Class
Return type for all PLC operations.

```csharp
public class Response
{
    public string TagName { get; }
    public object Value { get; }
    public string Status { get; }
}
```

### 4.3 Tag Class
Represents a tag retrieved from the PLC.

```csharp
public class Tag
{
    public string TagName { get; set; }
    public ushort InstanceID { get; set; }
    public byte SymbolType { get; set; }
    public ushort DataTypeValue { get; set; }
    public string DataType { get; set; }
    public int Array { get; set; }
    public int Struct { get; set; }
    public int Size { get; set; }
    public int? AccessRight { get; set; }
    public bool? Internal { get; set; }
}
```

### 4.4 Device Class
Represents a discovered EtherNet/IP device.

```csharp
public class Device
{
    public ushort Length { get; set; }
    public ushort EncapsulationVersion { get; set; }
    public string IPAddress { get; set; }
    public ushort VendorID { get; set; }
    public string Vendor { get; set; }
    public ushort DeviceID { get; set; }
    public string DeviceType { get; set; }
    public ushort ProductCode { get; set; }
    public string Revision { get; set; }
    public ushort Status { get; set; }
    public string SerialNumber { get; set; }
    public byte ProductNameLength { get; set; }
    public string ProductName { get; set; }
    public byte State { get; set; }
}
```

### 4.5 UDT Class (User Defined Type)
Represents a user-defined type structure.

```csharp
public class UDT
{
    public ushort Type { get; set; }
    public string Name { get; set; }
    public List<Tag> Fields { get; set; }
    public Dictionary<string, Tag> FieldsByName { get; set; }
}
```

---

## 5. Public API Methods

### 5.1 Read Operations

#### Read(string tag, int count = 1, byte? datatype = null)
Read a single tag or array elements from the PLC.

**Parameters:**
- `tag`: Tag name (supports program scope, arrays, UDT members, bit addressing)
- `count`: Number of elements to read (default: 1)
- `datatype`: Optional data type override

**Returns:** `Response` object with value(s)

**Tag Name Formats Supported:**
- Simple: `MyTag`
- Array: `MyTag[0]`, `MyTag[0,1,2]` (multi-dimensional)
- Program scope: `Program:MainProgram.MyTag`
- UDT member: `MyUDT.Member`
- Bit of word: `MyDINT.5`
- Complex: `MyArray[4].Member[2].SubMember`

#### Read(IEnumerable<string> tags)
Read multiple tags using multi-service messaging (batch read).

**Parameters:**
- `tags`: Collection of tag names or tuples (tagName, count, datatype)

**Returns:** `List<Response>` for each tag

### 5.2 Write Operations

#### Write(string tag, object value, byte? datatype = null)
Write a single value or array to a PLC tag.

**Parameters:**
- `tag`: Tag name
- `value`: Value to write (single or array)
- `datatype`: Optional data type override

**Returns:** `Response` object

#### Write(IEnumerable<(string tag, object value, byte? datatype)> tags)
Write multiple tags using multi-service messaging (batch write).

**Parameters:**
- `tags`: Collection of tuples (tagName, value, datatype)

**Returns:** `List<Response>` for each tag

### 5.3 Tag List Operations

#### GetTagList(bool allTags = true)
Retrieve the tag list from the PLC.

**Parameters:**
- `allTags`: If true, includes program-scoped tags; if false, controller tags only

**Returns:** `Response` with `Value` as `List<Tag>`

#### GetProgramTagList(string programName)
Retrieve tags for a specific program.

**Parameters:**
- `programName`: Program name (e.g., "Program:MainProgram")

**Returns:** `Response` with `Value` as `List<Tag>`

#### GetProgramsList()
Retrieve list of program names from the PLC.

**Returns:** `Response` with `Value` as `List<string>`

### 5.4 Time Operations

#### GetPLCTime(bool raw = false)
Get the PLC clock time.

**Parameters:**
- `raw`: If true, returns microseconds since epoch; if false, returns DateTime

**Returns:** `Response` with `Value` as `DateTime` or `long`

#### SetPLCTime(int? dst = null)
Set the PLC clock time to current system time.

**Parameters:**
- `dst`: Daylight saving time flag (null = auto-detect)

**Returns:** `Response`

### 5.5 Discovery Operations

#### Discover()
Discover all EtherNet/IP devices on the network.

**Returns:** `Response` with `Value` as `List<Device>`

#### GetModuleProperties(int slot)
Get properties of a module in a specific slot.

**Parameters:**
- `slot`: Slot number

**Returns:** `Response` with `Value` as `Device`

#### GetDeviceProperties()
Get properties of the device at the configured IP address.

**Returns:** `Response` with `Value` as `Device`

### 5.6 Custom Messaging

#### Message(byte cipService, ushort cipClass, ushort cipInstance, byte? cipAttribute = null, byte[] data = null)
Send a custom CIP message.

**Parameters:**
- `cipService`: CIP service code
- `cipClass`: CIP class
- `cipInstance`: CIP instance
- `cipAttribute`: Optional CIP attribute
- `data`: Optional data payload

**Returns:** `Response` with raw data

### 5.7 Connection Management

#### Close()
Close the connection to the PLC and release resources.

---

## 6. Internal Implementation Requirements

### 6.1 Connection Management (Connection Class)
Internal class handling socket communication.

**Required Methods:**
- `Connect(bool connected = true)` - Establish connection
- `Send(byte[] request, bool connected = true, int? slot = null)` - Send request
- `Close()` - Close connection
- `Discover(Func<byte[], Device> parser)` - Device discovery

**Connection Types:**
- **Connected Messaging**: Uses Forward Open for session
- **Unconnected Messaging**: Direct request/response

**Forward Open:**
- Standard: ConnectionSize <= 511 bytes (service 0x54)
- Large: ConnectionSize > 511 bytes (service 0x5B)
- Auto-negotiate: Try large first, fallback to standard

### 6.2 IOI (Instance/Object Identifier) Building
Build tag path for CIP requests.

**Supported Formats:**
- Symbolic segment (0x91): Tag names
- Element segment (0x28/0x29/0x2A): Array indices
- Multi-dimensional arrays
- Bit addressing for BOOL arrays

### 6.3 Packet Building

**EIP Header Structure:**
| Field | Size | Description |
|-------|------|-------------|
| Command | 2 | EIP command (0x65=Register, 0x66=Unregister, 0x6F=SendRRData, 0x70=SendUnitData) |
| Length | 2 | Data length |
| Session Handle | 4 | Session identifier |
| Status | 4 | Response status |
| Context | 8 | Sender context |
| Options | 4 | Options |

### 6.4 Service Codes
| Code | Service | Description |
|------|---------|-------------|
| 0x4C | Read Tag | Single read |
| 0x4D | Write Tag | Single write |
| 0x4E | Read/Write Modify | Bit-level write |
| 0x52 | Read Tag Fragmented | Partial read |
| 0x53 | Write Tag Fragmented | Partial write |
| 0x54 | Forward Open | Standard connection |
| 0x55 | Get Instance Attribute List | Tag list retrieval |
| 0x5B | Large Forward Open | Large connection |
| 0x0A | Multiple Service Packet | Batch operations |

### 6.5 Error Handling
Implement CIP error code mapping:

| Code | Description |
|------|-------------|
| 0x00 | Success |
| 0x01 | Connection failure |
| 0x04 | Path segment error |
| 0x05 | Path destination unknown |
| 0x06 | Partial transfer |
| 0x08 | Service not supported |
| 0x13 | Not enough data |
| 0x14 | Attribute not supported |
| 0x26 | Path size invalid |
| ... | (see full list in implementation) |

---

## 7. Usage Examples

### 7.1 Basic Read
```csharp
using CSLogix;

using (var plc = new PLC("192.168.1.10"))
{
    var response = plc.Read("MyTag");
    Console.WriteLine($"{response.TagName}: {response.Value} ({response.Status})");
}
```

### 7.2 Array Read
```csharp
using (var plc = new PLC("192.168.1.10"))
{
    var response = plc.Read("MyArray", count: 10);
    var values = (object[])response.Value;
    foreach (var val in values)
        Console.WriteLine(val);
}
```

### 7.3 Multiple Tag Read
```csharp
using (var plc = new PLC("192.168.1.10"))
{
    var tags = new[] { "Tag1", "Tag2", "Tag3" };
    var responses = plc.Read(tags);
    foreach (var r in responses)
        Console.WriteLine($"{r.TagName}: {r.Value}");
}
```

### 7.4 Write Operations
```csharp
using (var plc = new PLC("192.168.1.10"))
{
    // Single write
    plc.Write("MyDINT", 42);

    // Array write
    plc.Write("MyArray[0]", new int[] { 1, 2, 3, 4, 5 });

    // Multiple writes
    var writes = new[]
    {
        ("Tag1", (object)100, (byte?)null),
        ("Tag2", (object)200, (byte?)null)
    };
    plc.Write(writes);
}
```

### 7.5 Tag List Retrieval
```csharp
using (var plc = new PLC("192.168.1.10"))
{
    var response = plc.GetTagList();
    var tags = (List<Tag>)response.Value;
    foreach (var tag in tags)
        Console.WriteLine($"{tag.TagName} ({tag.DataType})");
}
```

### 7.6 Device Discovery
```csharp
using (var plc = new PLC())
{
    var response = plc.Discover();
    var devices = (List<Device>)response.Value;
    foreach (var device in devices)
        Console.WriteLine($"{device.IPAddress}: {device.ProductName}");
}
```

### 7.7 ControlLogix Slot Configuration
```csharp
using (var plc = new PLC("192.168.1.10") { ProcessorSlot = 2 })
{
    var response = plc.Read("MyTag");
}
```

### 7.8 Micro800 Configuration
```csharp
using (var plc = new PLC("192.168.1.10") { Micro800 = true })
{
    var response = plc.Read("MyTag");
}
```

---

## 8. Project Structure

```
CSLogix/
├── src/
│   └── CSLogix/
│       ├── PLC.cs                 # Main PLC class
│       ├── Connection.cs          # Socket/connection handling
│       ├── Models/
│       │   ├── Response.cs        # Response class
│       │   ├── Tag.cs             # Tag class
│       │   ├── UDT.cs             # UDT class
│       │   └── Device.cs          # Device class
│       ├── Constants/
│       │   ├── CIPTypes.cs        # Data type definitions
│       │   ├── CIPServices.cs     # Service codes
│       │   ├── ErrorCodes.cs      # Error code mapping
│       │   ├── Vendors.cs         # Vendor ID mapping
│       │   └── DeviceTypes.cs     # Device type mapping
│       └── Helpers/
│           ├── TagParser.cs       # Tag name parsing
│           ├── PacketBuilder.cs   # Packet construction
│           └── BitOperations.cs   # Bit manipulation utilities
├── tests/
│   └── CSLogix.Tests/
│       ├── PLCTests.cs
│       ├── ConnectionTests.cs
│       └── ParserTests.cs
├── examples/
│   ├── ReadExample/
│   ├── WriteExample/
│   └── DiscoverExample/
├── CSLogix.sln
└── README.md
```

---

## 9. Testing Requirements

### 9.1 Unit Tests
- Tag name parsing (arrays, UDT members, bit addressing)
- Packet building and parsing
- Data type conversion
- Error code mapping

### 9.2 Integration Tests (requires PLC)
- Read/write operations for all data types
- Array operations
- Multi-service messaging
- Tag list retrieval
- Device discovery
- Connection handling (connect, disconnect, reconnect)

### 9.3 Test Coverage Goals
- Minimum 80% code coverage
- All public API methods covered
- Edge cases for tag parsing

---

## 10. Non-Functional Requirements

### 10.1 Performance
- Connection reuse for multiple operations
- Batch operations via multi-service messaging
- Efficient buffer management

### 10.2 Reliability
- Automatic connection retry on failure
- Proper resource cleanup (IDisposable pattern)
- Thread-safe operations (future consideration)

### 10.3 Maintainability
- XML documentation comments on all public members
- Consistent naming conventions (C# style)
- Clear separation of concerns

### 10.4 Compatibility
- Maintain API compatibility with pylogix function names
- Support same tag addressing formats
- Match pylogix behavior for edge cases

---

## 11. Future Considerations

### 11.1 Potential Enhancements
- Async/await support for all operations
- Tag subscription/polling mechanism
- Connection pooling
- Extended UDT support
- Logging/diagnostics integration

### 11.2 Out of Scope
- GUI components
- PLC programming functionality
- Protocol support for non-Logix PLCs (PLC5, SLC, MicroLogix)

---

## 12. References

- **pylogix Repository**: https://github.com/dmroeder/pylogix
- **EtherNet/IP Specification**: ODVA CIP Networks Library
- **Allen Bradley Documentation**: Rockwell Automation literature

---

## 13. Revision History

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0 | 2025-12-29 | Auto-generated | Initial PRD based on pylogix analysis |
