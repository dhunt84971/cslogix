using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CSLogix.Helpers
{
    /// <summary>
    /// Parses PLC tag names and builds IOI (Instance Object Identifier) paths.
    /// Supports arrays, UDT members, bit addressing, and program scope.
    /// </summary>
    internal static class TagParser
    {
        /// <summary>
        /// Represents a parsed tag name with its components.
        /// </summary>
        public class ParsedTag
        {
            public string BaseTag { get; set; } = string.Empty;
            public int[] ArrayIndices { get; set; } = Array.Empty<int>();
            public int? BitIndex { get; set; }
            public string? ProgramName { get; set; }
            public List<string> Members { get; set; } = new List<string>();
        }

        /// <summary>
        /// Parses a tag name into its components.
        /// </summary>
        /// <param name="tagName">The tag name to parse.</param>
        /// <returns>A ParsedTag object with the components.</returns>
        public static ParsedTag Parse(string tagName)
        {
            var result = new ParsedTag();
            string working = tagName;

            // Check for program scope (Program:MainProgram.MyTag)
            if (working.StartsWith("Program:", StringComparison.OrdinalIgnoreCase))
            {
                int dotIndex = working.IndexOf('.');
                if (dotIndex > 0)
                {
                    result.ProgramName = working.Substring(0, dotIndex);
                    working = working.Substring(dotIndex + 1);
                }
            }

            // Split by '.' to get members
            var parts = working.Split('.');
            bool isFirst = true;

            foreach (var part in parts)
            {
                string memberName = part;
                int[] indices = Array.Empty<int>();
                int? bitIndex = null;

                // Check for array index [x] or [x,y,z]
                int bracketStart = memberName.IndexOf('[');
                if (bracketStart >= 0)
                {
                    int bracketEnd = memberName.IndexOf(']');
                    if (bracketEnd > bracketStart)
                    {
                        string indexStr = memberName.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                        memberName = memberName.Substring(0, bracketStart);

                        // Parse indices (could be multi-dimensional)
                        var indexParts = indexStr.Split(',');
                        indices = new int[indexParts.Length];
                        for (int i = 0; i < indexParts.Length; i++)
                        {
                            if (int.TryParse(indexParts[i].Trim(), out int idx))
                            {
                                indices[i] = idx;
                            }
                        }
                    }
                }

                // Check for bit addressing on numeric suffix (MyDINT.5)
                if (!isFirst && int.TryParse(memberName, out int bit))
                {
                    result.BitIndex = bit;
                    continue;
                }

                if (isFirst)
                {
                    result.BaseTag = memberName;
                    result.ArrayIndices = indices;
                    isFirst = false;
                }
                else
                {
                    // For members, we store them with their indices
                    if (indices.Length > 0)
                    {
                        result.Members.Add($"{memberName}[{string.Join(",", indices)}]");
                    }
                    else
                    {
                        result.Members.Add(memberName);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds an IOI (Instance Object Identifier) path from a tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="dataType">Optional data type for BOOL array handling.</param>
        /// <returns>The IOI byte array.</returns>
        public static byte[] BuildIOI(string tagName, byte? dataType = null)
        {
            var ioi = new List<byte>();
            var parsed = Parse(tagName);

            // Add program scope if present
            if (!string.IsNullOrEmpty(parsed.ProgramName))
            {
                AddSymbolicSegment(ioi, parsed.ProgramName);
            }

            // Add base tag
            AddSymbolicSegment(ioi, parsed.BaseTag);

            // Add array indices for base tag
            if (parsed.ArrayIndices.Length > 0)
            {
                // For BOOL arrays stored as DWORD, adjust the index
                if (dataType == Constants.CIPTypes.DWORD)
                {
                    // BOOL arrays: index points to the DWORD, bit is internal
                    int dwordIndex = parsed.ArrayIndices[0] / 32;
                    AddElementSegment(ioi, dwordIndex);
                }
                else
                {
                    foreach (var index in parsed.ArrayIndices)
                    {
                        AddElementSegment(ioi, index);
                    }
                }
            }

            // Add member segments
            foreach (var member in parsed.Members)
            {
                // Check if member has array index
                int bracketStart = member.IndexOf('[');
                if (bracketStart >= 0)
                {
                    string memberName = member.Substring(0, bracketStart);
                    int bracketEnd = member.IndexOf(']');
                    string indexStr = member.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

                    AddSymbolicSegment(ioi, memberName);

                    var indexParts = indexStr.Split(',');
                    foreach (var idxStr in indexParts)
                    {
                        if (int.TryParse(idxStr.Trim(), out int idx))
                        {
                            AddElementSegment(ioi, idx);
                        }
                    }
                }
                else
                {
                    AddSymbolicSegment(ioi, member);
                }
            }

            return ioi.ToArray();
        }

        /// <summary>
        /// Adds a symbolic segment (tag name) to the IOI.
        /// </summary>
        private static void AddSymbolicSegment(List<byte> ioi, string name)
        {
            // Symbolic segment: 0x91, length, name bytes
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            ioi.Add(0x91); // Symbolic segment
            ioi.Add((byte)nameBytes.Length);
            ioi.AddRange(nameBytes);

            // Pad to word boundary
            if (nameBytes.Length % 2 != 0)
            {
                ioi.Add(0x00);
            }
        }

        /// <summary>
        /// Adds an element segment (array index) to the IOI.
        /// </summary>
        private static void AddElementSegment(List<byte> ioi, int index)
        {
            if (index < 256)
            {
                // 8-bit element segment: 0x28, index
                ioi.Add(0x28);
                ioi.Add((byte)index);
            }
            else if (index < 65536)
            {
                // 16-bit element segment: 0x29, 0x00, index (2 bytes)
                ioi.Add(0x29);
                ioi.Add(0x00);
                ioi.Add((byte)(index & 0xFF));
                ioi.Add((byte)((index >> 8) & 0xFF));
            }
            else
            {
                // 32-bit element segment: 0x2A, 0x00, index (4 bytes)
                ioi.Add(0x2A);
                ioi.Add(0x00);
                ioi.Add((byte)(index & 0xFF));
                ioi.Add((byte)((index >> 8) & 0xFF));
                ioi.Add((byte)((index >> 16) & 0xFF));
                ioi.Add((byte)((index >> 24) & 0xFF));
            }
        }

        /// <summary>
        /// Calculates the bit index for BOOL array access.
        /// </summary>
        /// <param name="arrayIndex">The array index.</param>
        /// <returns>The bit index within the DWORD (0-31).</returns>
        public static int GetBitIndex(int arrayIndex)
        {
            return arrayIndex % 32;
        }
    }
}
