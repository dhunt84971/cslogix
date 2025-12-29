using System;
using CSLogix;
using CSLogix.Models;

namespace BasicExample
{
    /// <summary>
    /// Basic example demonstrating CSLogix usage for reading and writing PLC tags.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Configure the PLC connection
            string ipAddress = args.Length > 0 ? args[0] : "192.168.1.10";

            Console.WriteLine($"CSLogix Basic Example");
            Console.WriteLine($"Connecting to PLC at {ipAddress}...");
            Console.WriteLine();

            // Create PLC instance with using statement for automatic cleanup
            using var plc = new PLC(ipAddress)
            {
                // Optional: Configure connection parameters
                ProcessorSlot = 0,      // Default slot 0
                SocketTimeout = 5.0,    // 5 second timeout
                // Micro800 = false,    // Set true for Micro800 series
            };

            // =====================
            // READING TAGS
            // =====================
            Console.WriteLine("=== Reading Tags ===");

            // Read a DINT (32-bit integer)
            var dintResult = plc.Read("MyDINT");
            PrintResult("MyDINT", dintResult);

            // Read a REAL (floating point)
            var realResult = plc.Read("MyREAL");
            PrintResult("MyREAL", realResult);

            // Read a BOOL
            var boolResult = plc.Read("MyBOOL");
            PrintResult("MyBOOL", boolResult);

            // Read a STRING
            var stringResult = plc.Read("MyString");
            PrintResult("MyString", stringResult);

            Console.WriteLine();

            // =====================
            // READING ARRAYS
            // =====================
            Console.WriteLine("=== Reading Arrays ===");

            // Read a single array element
            var arrayElement = plc.Read("MyArray[0]");
            PrintResult("MyArray[0]", arrayElement);

            // Read multiple array elements (10 elements starting from index 0)
            var arrayMultiple = plc.Read("MyArray[0]", 10);
            PrintResult("MyArray[0] (10 elements)", arrayMultiple);

            Console.WriteLine();

            // =====================
            // READING UDT MEMBERS
            // =====================
            Console.WriteLine("=== Reading UDT Members ===");

            // Read a member of a User Defined Type
            var udtMember = plc.Read("MyUDT.Counter");
            PrintResult("MyUDT.Counter", udtMember);

            // Read nested UDT member
            var nestedMember = plc.Read("MyUDT.SubStruct.Value");
            PrintResult("MyUDT.SubStruct.Value", nestedMember);

            Console.WriteLine();

            // =====================
            // READING PROGRAM-SCOPED TAGS
            // =====================
            Console.WriteLine("=== Program-Scoped Tags ===");

            // Read a tag scoped to a specific program
            var programTag = plc.Read("Program:MainProgram.LocalCounter");
            PrintResult("Program:MainProgram.LocalCounter", programTag);

            Console.WriteLine();

            // =====================
            // WRITING TAGS
            // =====================
            Console.WriteLine("=== Writing Tags ===");

            // Write a DINT
            var writeIntResult = plc.Write("MyDINT", 12345);
            PrintResult("Write MyDINT", writeIntResult);

            // Write a REAL
            var writeRealResult = plc.Write("MyREAL", 3.14159f);
            PrintResult("Write MyREAL", writeRealResult);

            // Write a BOOL
            var writeBoolResult = plc.Write("MyBOOL", true);
            PrintResult("Write MyBOOL", writeBoolResult);

            // Write a STRING
            var writeStringResult = plc.Write("MyString", "Hello from C#!");
            PrintResult("Write MyString", writeStringResult);

            Console.WriteLine();

            // =====================
            // WRITING ARRAYS
            // =====================
            Console.WriteLine("=== Writing Arrays ===");

            // Write a single array element
            var writeArrayElement = plc.Write("MyArray[0]", 100);
            PrintResult("Write MyArray[0]", writeArrayElement);

            // Write multiple array elements
            int[] values = { 10, 20, 30, 40, 50 };
            var writeArrayMultiple = plc.Write("MyArray[0]", values);
            PrintResult("Write MyArray[0] (5 elements)", writeArrayMultiple);

            Console.WriteLine();
            Console.WriteLine("Example complete!");
        }

        static void PrintResult(string operation, Response result)
        {
            if (result.Status == "Success")
            {
                Console.WriteLine($"  {operation}: {result.Value}");
            }
            else
            {
                Console.WriteLine($"  {operation}: ERROR - {result.Status}");
            }
        }
    }
}
