using CSLogix;
using CSLogix.Models;

Console.WriteLine("CSLogix PLC Test Program");
Console.WriteLine("========================\n");

var plcAddress = "192.168.0.2";
var slot = 0;

using var plc = new PLC(plcAddress)
{
    ProcessorSlot = slot,
    SocketTimeout = 5.0
};

Console.WriteLine($"Target PLC: {plcAddress}, Slot: {slot}\n");

// Test 1: Single DINT read
Console.WriteLine("Test 1: Single DINT Read (HeartBeat)");
Console.WriteLine("-------------------------------------");
try
{
    var response = plc.Read("HeartBeat");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 2: Single STRING read
Console.WriteLine("Test 2: Single STRING Read (TextMessage)");
Console.WriteLine("-----------------------------------------");
try
{
    var response = plc.Read("TextMessage");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 3: Array element read
Console.WriteLine("Test 3: Array Element Read (Numbers[0])");
Console.WriteLine("----------------------------------------");
try
{
    var response = plc.Read("Numbers[0]");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 4: Multiple array elements
Console.WriteLine("Test 4: Array Read (Numbers, 5 elements)");
Console.WriteLine("-----------------------------------------");
try
{
    var response = plc.Read("Numbers", 5);
    Console.WriteLine($"  Status: {response.Status}");
    if (response.Value is Array arr)
    {
        Console.WriteLine($"  Array Length: {arr.Length}");
        for (int i = 0; i < Math.Min(5, arr.Length); i++)
        {
            Console.WriteLine($"  [{i}]: {arr.GetValue(i)}");
        }
    }
    else
    {
        Console.WriteLine($"  Value: {response.Value}");
    }
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 5: BOOL array element
Console.WriteLine("Test 5: BOOL Array Element Read (Booleans[0])");
Console.WriteLine("----------------------------------------------");
try
{
    var response = plc.Read("Booleans[0]");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 6: Timer tag (UDT)
Console.WriteLine("Test 6: Timer Read (HeartBeatTimer.ACC)");
Console.WriteLine("---------------------------------------");
try
{
    var response = plc.Read("HeartBeatTimer.ACC");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 7: Program-scoped tag
Console.WriteLine("Test 7: Program-Scoped Tag Read (Program:MainProgram.LocalReal)");
Console.WriteLine("----------------------------------------------------------------");
try
{
    var response = plc.Read("Program:MainProgram.LocalReal");
    Console.WriteLine($"  Status: {response.Status}");
    Console.WriteLine($"  Value: {response.Value}");
    Console.WriteLine($"  Value Type: {response.Value?.GetType().Name ?? "null"}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 8: Multi-tag read (batch)
Console.WriteLine("Test 8: Multi-Tag Read (HeartBeat, TextMessage, Numbers[0])");
Console.WriteLine("------------------------------------------------------------");
try
{
    var tags = new List<string> { "HeartBeat", "TextMessage", "Numbers[0]" };
    var responses = plc.Read(tags);

    Console.WriteLine($"  Responses received: {responses.Count}");
    for (int i = 0; i < responses.Count; i++)
    {
        var resp = responses[i];
        Console.WriteLine($"  [{i}] Tag: {resp.TagName}");
        Console.WriteLine($"      Status: {resp.Status}");
        Console.WriteLine($"      Value: {resp.Value}");
        Console.WriteLine($"      Type: {resp.Value?.GetType().Name ?? "null"}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
Console.WriteLine();

// Test 9: Get Tag List
Console.WriteLine("Test 9: Get Tag List (Controller Tags)");
Console.WriteLine("---------------------------------------");
try
{
    var response = plc.GetTagList(false);
    Console.WriteLine($"  Status: {response.Status}");
    if (response.Value is List<Tag> tags)
    {
        Console.WriteLine($"  Tags found: {tags.Count}");
        foreach (var tag in tags.Take(10))
        {
            Console.WriteLine($"    - {tag.TagName} ({tag.DataType})");
        }
        if (tags.Count > 10)
        {
            Console.WriteLine($"    ... and {tags.Count - 10} more");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 10: Get Device Properties
Console.WriteLine("Test 10: Get Device Properties");
Console.WriteLine("------------------------------");
try
{
    var response = plc.GetDeviceProperties();
    Console.WriteLine($"  Status: {response.Status}");
    if (response.Value is Device device)
    {
        Console.WriteLine($"  Vendor: {device.Vendor} ({device.VendorID})");
        Console.WriteLine($"  Device Type: {device.DeviceType}");
        Console.WriteLine($"  Product Name: {device.ProductName}");
        Console.WriteLine($"  Revision: {device.Revision}");
        Console.WriteLine($"  Serial: {device.SerialNumber}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
}
Console.WriteLine();

// Test 11: Debug - Dump raw response data for single read
Console.WriteLine("Test 11: Debug Raw Response");
Console.WriteLine("---------------------------");
try
{
    // We'll do a custom read to see raw data
    Console.WriteLine("  Reading HeartBeat and checking internal state...");
    var response = plc.Read("HeartBeat");
    Console.WriteLine($"  Response Status: {response.Status}");
    Console.WriteLine($"  Response Value: {response.Value}");

    // Read again to see if it's consistent
    Console.WriteLine("\n  Reading HeartBeat again...");
    response = plc.Read("HeartBeat");
    Console.WriteLine($"  Response Status: {response.Status}");
    Console.WriteLine($"  Response Value: {response.Value}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
Console.WriteLine();

// Test 12: Debug Multi-Service Raw
Console.WriteLine("Test 12: Debug Multi-Service Implementation");
Console.WriteLine("--------------------------------------------");
try
{
    // Check if we can use the Message method to send raw CIP
    // Let's try reading just one tag via multi-service to narrow down the issue
    Console.WriteLine("  Testing with just 1 tag in multi-service...");
    var singleTagList = new List<string> { "HeartBeat" };
    var responses = plc.Read(singleTagList);
    Console.WriteLine($"  Responses: {responses.Count}");
    foreach (var resp in responses)
    {
        Console.WriteLine($"    Tag: {resp.TagName}, Status: {resp.Status}, Value: {resp.Value}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception: {ex.Message}");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
Console.WriteLine();

Console.WriteLine("Tests complete.");
