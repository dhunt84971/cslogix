using System;
using System.Collections.Generic;

namespace CSLogix.Models
{
    /// <summary>
    /// Represents a User Defined Type (UDT) structure from the PLC.
    /// Contains the type definition and its member fields.
    /// </summary>
    public class UDT
    {
        /// <summary>
        /// Gets or sets the UDT type code/ID in the PLC.
        /// </summary>
        public ushort Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the UDT.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of fields (members) in this UDT.
        /// </summary>
        public List<Tag> Fields { get; set; } = new List<Tag>();

        /// <summary>
        /// Gets or sets a dictionary mapping field names to field definitions.
        /// </summary>
        public Dictionary<string, Tag> FieldsByName { get; set; } = new Dictionary<string, Tag>();

        /// <summary>
        /// Returns a string representation of the UDT.
        /// </summary>
        public override string ToString()
        {
            return $"UDT(Type={Type}, Name={Name}, Fields={Fields.Count})";
        }
    }
}
