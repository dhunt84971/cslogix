using System;
using System.Collections.Generic;

namespace CSLogix.Constants
{
    /// <summary>
    /// CIP (Common Industrial Protocol) data type definitions.
    /// Each type maps to its size in bytes, name, and pack format.
    /// </summary>
    public static class CIPTypes
    {
        /// <summary>
        /// Represents a CIP data type definition.
        /// </summary>
        public class CIPType
        {
            /// <summary>
            /// Size of the data type in bytes.
            /// </summary>
            public int Size { get; }

            /// <summary>
            /// Human-readable name of the data type.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// The .NET type that corresponds to this CIP type.
            /// </summary>
            public Type DotNetType { get; }

            /// <summary>
            /// Creates a new CIP type definition.
            /// </summary>
            public CIPType(int size, string name, Type dotNetType)
            {
                Size = size;
                Name = name;
                DotNetType = dotNetType;
            }
        }

        /// <summary>Unknown or unrecognized type code.</summary>
        public const byte UNKNOWN = 0x00;
        /// <summary>Structure/UDT type code.</summary>
        public const byte STRUCT = 0xA0;
        /// <summary>Date and Time type code.</summary>
        public const byte DT = 0xC0;
        /// <summary>Boolean type code (1 byte).</summary>
        public const byte BOOL = 0xC1;
        /// <summary>Signed 8-bit integer type code.</summary>
        public const byte SINT = 0xC2;
        /// <summary>Signed 16-bit integer type code.</summary>
        public const byte INT = 0xC3;
        /// <summary>Signed 32-bit integer type code.</summary>
        public const byte DINT = 0xC4;
        /// <summary>Signed 64-bit integer type code.</summary>
        public const byte LINT = 0xC5;
        /// <summary>Unsigned 8-bit integer type code.</summary>
        public const byte USINT = 0xC6;
        /// <summary>Unsigned 16-bit integer type code.</summary>
        public const byte UINT = 0xC7;
        /// <summary>Unsigned 32-bit integer type code.</summary>
        public const byte UDINT = 0xC8;
        /// <summary>64-bit word type code.</summary>
        public const byte LWORD = 0xC9;
        /// <summary>32-bit floating point type code.</summary>
        public const byte REAL = 0xCA;
        /// <summary>64-bit floating point type code.</summary>
        public const byte LREAL = 0xCB;
        /// <summary>Long Date and Time type code.</summary>
        public const byte LDT = 0xCC;
        /// <summary>Old-style string type code.</summary>
        public const byte O_STRING = 0xD0;
        /// <summary>8-bit byte type code.</summary>
        public const byte BYTE = 0xD1;
        /// <summary>16-bit word type code.</summary>
        public const byte WORD = 0xD2;
        /// <summary>32-bit double word type code (also used for BOOL arrays).</summary>
        public const byte DWORD = 0xD3;
        /// <summary>32-bit time type code.</summary>
        public const byte TIME32 = 0xD6;
        /// <summary>64-bit time type code.</summary>
        public const byte TIME = 0xD7;
        /// <summary>Standard Logix STRING type code.</summary>
        public const byte STRING = 0xDA;
        /// <summary>Long time (64-bit) type code.</summary>
        public const byte LTIME = 0xDF;

        /// <summary>
        /// String ID for standard Logix STRING type.
        /// </summary>
        public const ushort StringID = 0x0FCE;

        /// <summary>
        /// Dictionary mapping CIP type codes to their definitions.
        /// </summary>
        public static readonly Dictionary<byte, CIPType> Types = new Dictionary<byte, CIPType>
        {
            { UNKNOWN, new CIPType(1, "UNKNOWN", typeof(byte)) },
            { STRUCT, new CIPType(88, "STRUCT", typeof(byte[])) },
            { DT, new CIPType(8, "DT", typeof(DateTime)) },
            { BOOL, new CIPType(1, "BOOL", typeof(bool)) },
            { SINT, new CIPType(1, "SINT", typeof(sbyte)) },
            { INT, new CIPType(2, "INT", typeof(short)) },
            { DINT, new CIPType(4, "DINT", typeof(int)) },
            { LINT, new CIPType(8, "LINT", typeof(long)) },
            { USINT, new CIPType(1, "USINT", typeof(byte)) },
            { UINT, new CIPType(2, "UINT", typeof(ushort)) },
            { UDINT, new CIPType(4, "UDINT", typeof(uint)) },
            { LWORD, new CIPType(8, "LWORD", typeof(ulong)) },
            { REAL, new CIPType(4, "REAL", typeof(float)) },
            { LREAL, new CIPType(8, "LREAL", typeof(double)) },
            { LDT, new CIPType(8, "LDT", typeof(DateTime)) },
            { O_STRING, new CIPType(1, "O_STRING", typeof(string)) },
            { BYTE, new CIPType(1, "BYTE", typeof(byte)) },
            { WORD, new CIPType(2, "WORD", typeof(ushort)) },
            { DWORD, new CIPType(4, "DWORD", typeof(uint)) },
            { TIME32, new CIPType(4, "TIME32", typeof(uint)) },
            { TIME, new CIPType(8, "TIME", typeof(ulong)) },
            { STRING, new CIPType(1, "STRING", typeof(string)) },
            { LTIME, new CIPType(8, "LTIME", typeof(ulong)) }
        };

        /// <summary>
        /// Gets the size in bytes for a CIP type code.
        /// </summary>
        /// <param name="typeCode">The CIP type code.</param>
        /// <returns>Size in bytes, or 1 if unknown.</returns>
        public static int GetSize(byte typeCode)
        {
            if (Types.TryGetValue(typeCode, out var cipType))
            {
                return cipType.Size;
            }
            return 1;
        }

        /// <summary>
        /// Gets the name for a CIP type code.
        /// </summary>
        /// <param name="typeCode">The CIP type code.</param>
        /// <returns>Type name, or "UNKNOWN" if not found.</returns>
        public static string GetName(byte typeCode)
        {
            if (Types.TryGetValue(typeCode, out var cipType))
            {
                return cipType.Name;
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// Gets the .NET type for a CIP type code.
        /// </summary>
        /// <param name="typeCode">The CIP type code.</param>
        /// <returns>.NET Type, or typeof(byte) if unknown.</returns>
        public static Type GetDotNetType(byte typeCode)
        {
            if (Types.TryGetValue(typeCode, out var cipType))
            {
                return cipType.DotNetType;
            }
            return typeof(byte);
        }

        /// <summary>
        /// Checks if a type code represents a structure/UDT.
        /// </summary>
        public static bool IsStruct(byte typeCode) => typeCode == STRUCT;

        /// <summary>
        /// Checks if a type code represents a string type.
        /// </summary>
        public static bool IsString(byte typeCode) => typeCode == STRING || typeCode == O_STRING || typeCode == STRUCT;

        /// <summary>
        /// Checks if a type code represents a bool array (DWORD used for BOOL[]).
        /// </summary>
        public static bool IsBoolArray(byte typeCode) => typeCode == DWORD;

        /// <summary>
        /// Checks if a type code represents a floating point type.
        /// </summary>
        public static bool IsFloat(byte typeCode) => typeCode == REAL || typeCode == LREAL;
    }
}
