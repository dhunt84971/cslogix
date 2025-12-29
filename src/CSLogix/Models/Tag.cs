using System;
using System.Collections.Generic;
using System.Text;

namespace CSLogix.Models
{
    /// <summary>
    /// Represents a tag retrieved from the PLC tag list.
    /// Contains metadata about the tag including name, data type, and array information.
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Gets or sets the fully qualified tag name.
        /// May include program scope prefix (e.g., "Program:MainProgram.MyTag").
        /// </summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the instance ID of the tag in the PLC.
        /// </summary>
        public ushort InstanceID { get; set; }

        /// <summary>
        /// Gets or sets the symbol type code.
        /// </summary>
        public byte SymbolType { get; set; }

        /// <summary>
        /// Gets or sets the data type value (lower 12 bits of symbol type).
        /// </summary>
        public ushort DataTypeValue { get; set; }

        /// <summary>
        /// Gets or sets the human-readable data type name (e.g., "DINT", "REAL", "STRING").
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the array dimension flags.
        /// 0 = not an array, 1 = 1D array, 2 = 2D array, 3 = 3D array.
        /// </summary>
        public int Array { get; set; }

        /// <summary>
        /// Gets or sets whether this tag is a structure (UDT).
        /// 1 = struct, 0 = atomic type.
        /// </summary>
        public int Struct { get; set; }

        /// <summary>
        /// Gets or sets the array size (number of elements).
        /// 0 if not an array.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets or sets the access rights for this tag.
        /// </summary>
        public int? AccessRight { get; set; }

        /// <summary>
        /// Gets or sets whether this is an internal/hidden tag.
        /// </summary>
        public bool? Internal { get; set; }

        /// <summary>
        /// Gets or sets metadata flags.
        /// </summary>
        public int? Meta { get; set; }

        /// <summary>
        /// Gets or sets scope flag 0.
        /// </summary>
        public int? Scope0 { get; set; }

        /// <summary>
        /// Gets or sets scope flag 1.
        /// </summary>
        public int? Scope1 { get; set; }

        /// <summary>
        /// Gets or sets the raw bytes for this tag definition.
        /// </summary>
        public byte[]? Bytes { get; set; }

        /// <summary>
        /// Gets or sets the UDT this tag belongs to (for UDT field tags).
        /// </summary>
        public UDT? UDT { get; set; }

        /// <summary>
        /// Tag name prefixes that should be filtered out from tag lists.
        /// </summary>
        private static readonly string[] FilterPrefixes = { "__", "Routine:", "Map:", "Task:", "UDI:" };

        /// <summary>
        /// Checks if the tag name should be filtered out.
        /// </summary>
        /// <param name="tagName">The tag name to check.</param>
        /// <returns>True if the tag should be filtered, false otherwise.</returns>
        public static bool InFilter(string tagName)
        {
            foreach (var prefix in FilterPrefixes)
            {
                if (tagName.Contains(prefix))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Parses a tag from a raw packet.
        /// </summary>
        /// <param name="packet">The packet data containing tag information.</param>
        /// <param name="programName">Optional program name for scoped tags.</param>
        /// <returns>A new Tag instance parsed from the packet.</returns>
        public static Tag Parse(byte[] packet, string? programName = null)
        {
            var tag = new Tag();

            // Get tag name length at offset 4
            ushort nameLength = BitConverter.ToUInt16(packet, 4);

            // Get tag name starting at offset 6
            string name = Encoding.UTF8.GetString(packet, 6, nameLength);

            // Set full tag name with optional program scope
            if (!string.IsNullOrEmpty(programName))
            {
                tag.TagName = $"{programName}.{name}";
            }
            else
            {
                tag.TagName = name;
            }

            // Get instance ID at offset 0
            tag.InstanceID = BitConverter.ToUInt16(packet, 0);

            // Get type info at offset after name
            int typeOffset = 6 + nameLength;
            ushort val = BitConverter.ToUInt16(packet, typeOffset);

            tag.SymbolType = (byte)(val & 0xFF);
            tag.DataTypeValue = (ushort)(val & 0x0FFF);
            tag.Array = (val & 0x6000) >> 13;
            tag.Struct = (val & 0x8000) >> 15;

            // Get array size if this is an array
            if (tag.Array != 0)
            {
                tag.Size = BitConverter.ToUInt16(packet, typeOffset + 2);
            }
            else
            {
                tag.Size = 0;
            }

            return tag;
        }

        /// <summary>
        /// Returns a string representation of the tag.
        /// </summary>
        public override string ToString()
        {
            return $"{TagName} {InstanceID} {SymbolType} {DataTypeValue} {DataType} Array={Array} Struct={Struct} Size={Size}";
        }
    }
}
