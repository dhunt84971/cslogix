using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSLogix.Constants;
using CSLogix.Helpers;
using CSLogix.Models;

namespace CSLogix
{
    /// <summary>
    /// Main class for communicating with Allen Bradley PLCs over EtherNet/IP.
    /// Supports ControlLogix, CompactLogix, and Micro800 series PLCs.
    /// </summary>
    public class PLC : IDisposable
    {
        private Connection? _connection;
        private bool _disposed;
        private Dictionary<ushort, string> _knownTags = new Dictionary<ushort, string>();

        /// <summary>
        /// Gets or sets the IP address of the PLC.
        /// </summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port number (default: 44818).
        /// </summary>
        public int Port { get; set; } = 44818;

        /// <summary>
        /// Gets or sets the processor slot for ControlLogix (default: 0).
        /// </summary>
        public int ProcessorSlot { get; set; } = 0;

        /// <summary>
        /// Gets or sets the socket timeout in seconds (default: 5.0).
        /// </summary>
        public double SocketTimeout { get; set; } = 5.0;

        /// <summary>
        /// Gets or sets whether this is a Micro800 series PLC.
        /// </summary>
        public bool Micro800 { get; set; } = false;

        /// <summary>
        /// Gets or sets the custom routing path.
        /// </summary>
        public object[]? Route { get; set; }

        /// <summary>
        /// Gets or sets the connection size (packet size).
        /// </summary>
        public int? ConnectionSize { get; set; }

        /// <summary>
        /// Gets or sets the string encoding (default: "utf-8").
        /// </summary>
        public string StringEncoding { get; set; } = "utf-8";

        /// <summary>
        /// Creates a new PLC instance.
        /// </summary>
        public PLC()
        {
        }

        /// <summary>
        /// Creates a new PLC instance with the specified IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address of the PLC.</param>
        public PLC(string ipAddress)
        {
            IPAddress = ipAddress;
        }

        /// <summary>
        /// Gets the Connection object for this PLC.
        /// </summary>
        internal Connection GetConnection()
        {
            if (_connection == null)
            {
                _connection = new Connection(this);
                if (ConnectionSize.HasValue)
                {
                    _connection.ConnectionSize = ConnectionSize.Value;
                }
            }
            return _connection;
        }

        #region Read Operations

        /// <summary>
        /// Read a tag from the PLC.
        /// </summary>
        /// <param name="tag">Tag name to read.</param>
        /// <param name="count">Number of elements to read (for arrays).</param>
        /// <param name="datatype">Optional data type override.</param>
        /// <returns>Response with the tag value.</returns>
        public Response Read(string tag, int count = 1, byte? datatype = null)
        {
            // Ensure connected
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(tag, null, connectResult.Message);
            }

            // Build IOI
            var ioi = TagParser.BuildIOI(tag, datatype);

            // Build read request
            var request = BuildReadRequest(ioi, count);

            // Send request
            var (status, data) = conn.Send(request);

            if (status != 0 || data == null)
            {
                return new Response(tag, null, status);
            }

            // Parse response
            return ParseReadResponse(tag, data, count, datatype);
        }

        /// <summary>
        /// Read multiple tags from the PLC.
        /// </summary>
        /// <param name="tags">Collection of tag names to read.</param>
        /// <returns>List of responses for each tag.</returns>
        public List<Response> Read(IEnumerable<string> tags)
        {
            var results = new List<Response>();

            // Ensure connected
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                foreach (var tag in tags)
                {
                    results.Add(new Response(tag, null, connectResult.Message));
                }
                return results;
            }

            // Build multi-service request
            var tagList = new List<string>(tags);
            var requests = new List<byte[]>();

            foreach (var tag in tagList)
            {
                var ioi = TagParser.BuildIOI(tag);
                var request = BuildReadRequest(ioi, 1);
                requests.Add(request);
            }

            // Build multi-service packet
            var multiServiceRequest = BuildMultiServiceRequest(requests);

            // Send request
            var (status, data) = conn.Send(multiServiceRequest);

            if (status != 0 || data == null)
            {
                foreach (var tag in tagList)
                {
                    results.Add(new Response(tag, null, status));
                }
                return results;
            }

            // Parse multi-service response
            return ParseMultiServiceReadResponse(tagList, data);
        }

        private byte[] BuildReadRequest(byte[] ioi, int count)
        {
            var request = new List<byte>();

            // Service - Read Tag (0x4C)
            request.Add(CIPServices.ReadTag);

            // Path size (in words)
            request.Add((byte)(ioi.Length / 2));

            // IOI
            request.AddRange(ioi);

            // Number of elements
            request.Add((byte)(count & 0xFF));
            request.Add((byte)((count >> 8) & 0xFF));

            return request.ToArray();
        }

        private Response ParseReadResponse(string tag, byte[] data, int count, byte? datatype)
        {
            try
            {
                // Response starts at offset 50 for connected, 44 for unconnected
                int offset = 50;
                if (data.Length <= offset)
                {
                    return new Response(tag, null, "Invalid response length");
                }

                // Get data type from response
                byte typeCode = data[offset];
                offset += 2; // Skip type code and reserved byte

                // For STRUCT types, check if it's a STRING by reading the structure handle
                if (typeCode == CIPTypes.STRUCT && offset + 2 <= data.Length)
                {
                    ushort structHandle = BitConverter.ToUInt16(data, offset);
                    offset += 2; // Skip structure handle

                    // Check if this is a STRING structure (handle 0x0FCE)
                    if (structHandle == CIPTypes.StringID)
                    {
                        string strValue = ParseString(data, offset);
                        return new Response(tag, strValue, 0);
                    }
                }

                // If we requested multiple elements, return an array
                if (count > 1)
                {
                    return ParseArrayResponse(tag, data, offset, count, typeCode);
                }

                // Parse single value
                object? value = ParseValue(data, offset, typeCode);
                return new Response(tag, value, 0);
            }
            catch (Exception ex)
            {
                return new Response(tag, null, ex.Message);
            }
        }

        private Response ParseArrayResponse(string tag, byte[] data, int offset, int count, byte typeCode)
        {
            var values = new List<object?>();
            int elementSize = CIPTypes.GetSize(typeCode);

            for (int i = 0; i < count && offset < data.Length; i++)
            {
                var value = ParseValue(data, offset, typeCode);
                values.Add(value);
                offset += elementSize;
            }

            return new Response(tag, values.ToArray(), 0);
        }

        private object? ParseValue(byte[] data, int offset, byte typeCode)
        {
            if (offset >= data.Length)
                return null;

            switch (typeCode)
            {
                case CIPTypes.BOOL:
                    return data[offset] != 0;

                case CIPTypes.SINT:
                    return (sbyte)data[offset];

                case CIPTypes.INT:
                    return BitConverter.ToInt16(data, offset);

                case CIPTypes.DINT:
                    return BitConverter.ToInt32(data, offset);

                case CIPTypes.LINT:
                    return BitConverter.ToInt64(data, offset);

                case CIPTypes.USINT:
                case CIPTypes.BYTE:
                    return data[offset];

                case CIPTypes.UINT:
                case CIPTypes.WORD:
                    return BitConverter.ToUInt16(data, offset);

                case CIPTypes.UDINT:
                case CIPTypes.DWORD:
                    return BitConverter.ToUInt32(data, offset);

                case CIPTypes.LWORD:
                    return BitConverter.ToUInt64(data, offset);

                case CIPTypes.REAL:
                    return BitConverter.ToSingle(data, offset);

                case CIPTypes.LREAL:
                    return BitConverter.ToDouble(data, offset);

                case CIPTypes.STRING:
                case CIPTypes.STRUCT:
                    // Handle string structures
                    return ParseString(data, offset);

                default:
                    return data[offset];
            }
        }

        private string ParseString(byte[] data, int offset)
        {
            try
            {
                // Logix STRING structure: 4-byte length + 82 bytes data
                if (offset + 4 > data.Length)
                    return string.Empty;

                int length = BitConverter.ToInt32(data, offset);
                if (length <= 0 || offset + 4 + length > data.Length)
                    return string.Empty;

                return Encoding.GetEncoding(StringEncoding).GetString(data, offset + 4, Math.Min(length, 82));
            }
            catch
            {
                return string.Empty;
            }
        }

        private byte[] BuildMultiServiceRequest(List<byte[]> requests)
        {
            var packet = new List<byte>();

            // Service - Multiple Service Packet (0x0A)
            packet.Add(CIPServices.MultipleServicePacket);

            // Path size (in words) - 2 words for Message Router path
            packet.Add(0x02);
            // Class type (8-bit)
            packet.Add(0x20);
            // Class - Message Router
            packet.Add(0x02);
            // Instance type (8-bit)
            packet.Add(0x24);
            // Instance
            packet.Add(0x01);

            // Number of services
            packet.Add((byte)(requests.Count & 0xFF));
            packet.Add((byte)((requests.Count >> 8) & 0xFF));

            // Calculate offsets - offsets are relative to the start of the service data
            // (after the count and offset table)
            int headerSize = 2 + (requests.Count * 2); // Number (2 bytes) + offsets (2 bytes each)
            int currentOffset = headerSize;

            // Add offsets
            foreach (var req in requests)
            {
                packet.Add((byte)(currentOffset & 0xFF));
                packet.Add((byte)((currentOffset >> 8) & 0xFF));
                currentOffset += req.Length;
            }

            // Add requests
            foreach (var req in requests)
            {
                packet.AddRange(req);
            }

            return packet.ToArray();
        }

        private List<Response> ParseMultiServiceReadResponse(List<string> tags, byte[] data)
        {
            var results = new List<Response>();

            try
            {
                // Multi-service response parsing
                // For connected messaging, CIP data structure:
                // - EIP header: 24 bytes
                // - Interface handle: 4 bytes
                // - Timeout: 2 bytes
                // - Item count: 2 bytes
                // - Item 1 (Connected Address): type(2) + length(2) + connection ID(4) = 8 bytes
                // - Item 2 (Connected Data): type(2) + length(2) = 4 bytes
                // - Sequence: 2 bytes
                // - CIP data starts at offset 46 (24 + 4 + 2 + 2 + 8 + 4 + 2)
                //
                // CIP MSP Reply structure:
                // - Service reply (1 byte): 0x8A (Multiple Service Packet reply)
                // - Reserved (1 byte): 0x00
                // - General Status (1 byte): 0x00 for success
                // - Size of additional status (1 byte)
                // - Service count (2 bytes)
                // - Offsets (2 bytes each)
                // - Individual service replies

                int offset = 46; // Start of CIP data for connected messaging

                // Bounds check for minimum header
                if (data.Length < offset + 4)
                {
                    foreach (var tag in tags)
                    {
                        results.Add(new Response(tag, null, "Response too short"));
                    }
                    return results;
                }

                // Read service reply header
                byte service = data[offset];
                byte reserved = data[offset + 1];
                byte generalStatus = data[offset + 2];
                byte additionalStatusSize = data[offset + 3];

                offset += 4 + (additionalStatusSize * 2); // Skip additional status words

                // Check general status
                if (generalStatus != 0)
                {
                    foreach (var tag in tags)
                    {
                        results.Add(new Response(tag, null, generalStatus));
                    }
                    return results;
                }

                // Bounds check for reply count
                if (offset + 2 > data.Length)
                {
                    foreach (var tag in tags)
                    {
                        results.Add(new Response(tag, null, "Missing reply count"));
                    }
                    return results;
                }

                int replyCountOffset = offset;  // Save position of reply count
                int replyCount = BitConverter.ToUInt16(data, offset);
                offset += 2;

                // Read offsets for each reply
                var offsets = new List<int>();
                for (int i = 0; i < replyCount && i < tags.Count; i++)
                {
                    if (offset + 2 > data.Length)
                        break;
                    offsets.Add(BitConverter.ToUInt16(data, offset));
                    offset += 2;
                }

                // Parse each reply - offsets are relative to the start of the reply count field
                int dataStart = replyCountOffset;
                for (int i = 0; i < offsets.Count && i < tags.Count; i++)
                {
                    int replyOffset = dataStart + offsets[i];

                    // Bounds check
                    if (replyOffset + 4 > data.Length)
                    {
                        results.Add(new Response(tags[i], null, "Reply offset out of bounds"));
                        continue;
                    }

                    // Each service reply has:
                    // - Service code (1 byte)
                    // - Reserved (1 byte)
                    // - Status (1 byte)
                    // - Additional status size (1 byte)
                    // - Data type (2 bytes) if status == 0
                    // - Value data

                    byte replyService = data[replyOffset];
                    byte replyStatus = data[replyOffset + 2];
                    byte replyAdditionalSize = data[replyOffset + 3];

                    if (replyStatus != 0)
                    {
                        results.Add(new Response(tags[i], null, replyStatus));
                        continue;
                    }

                    int valueOffset = replyOffset + 4 + (replyAdditionalSize * 2);

                    if (valueOffset + 2 > data.Length)
                    {
                        results.Add(new Response(tags[i], null, "Value offset out of bounds"));
                        continue;
                    }

                    byte typeCode = data[valueOffset];
                    valueOffset += 2; // Skip type code and reserved byte

                    object? value = ParseValue(data, valueOffset, typeCode);
                    results.Add(new Response(tags[i], value, 0));
                }

                // Fill in any missing responses
                while (results.Count < tags.Count)
                {
                    results.Add(new Response(tags[results.Count], null, "No response"));
                }
            }
            catch (Exception ex)
            {
                // On error, return error for remaining tags
                while (results.Count < tags.Count)
                {
                    results.Add(new Response(tags[results.Count], null, ex.Message));
                }
            }

            return results;
        }

        #endregion

        #region Write Operations

        /// <summary>
        /// Write a value to a tag in the PLC.
        /// </summary>
        /// <param name="tag">Tag name to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="datatype">Optional data type override.</param>
        /// <returns>Response indicating success or failure.</returns>
        public Response Write(string tag, object value, byte? datatype = null)
        {
            // Ensure connected
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(tag, value, connectResult.Message);
            }

            // Build IOI
            var ioi = TagParser.BuildIOI(tag, datatype);

            // Determine data type and convert value
            byte typeCode = datatype ?? GuessDataType(value);
            byte[] valueBytes = ConvertToBytes(value, typeCode);

            // Determine element count
            int count = 1;
            if (value is Array arr)
            {
                count = arr.Length;
            }

            // Build write request
            var request = BuildWriteRequest(ioi, typeCode, count, valueBytes);

            // Send request
            var (status, data) = conn.Send(request);

            return new Response(tag, value, status);
        }

        /// <summary>
        /// Write multiple tags to the PLC.
        /// </summary>
        /// <param name="tags">Collection of (tag, value, datatype) tuples.</param>
        /// <returns>List of responses for each write.</returns>
        public List<Response> Write(IEnumerable<(string tag, object value, byte? datatype)> tags)
        {
            var results = new List<Response>();

            // Ensure connected
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                foreach (var (tag, value, _) in tags)
                {
                    results.Add(new Response(tag, value, connectResult.Message));
                }
                return results;
            }

            // Build multi-service request
            var tagList = new List<(string tag, object value, byte? datatype)>(tags);
            var requests = new List<byte[]>();

            foreach (var (tag, value, datatype) in tagList)
            {
                var ioi = TagParser.BuildIOI(tag, datatype);
                byte typeCode = datatype ?? GuessDataType(value);
                byte[] valueBytes = ConvertToBytes(value, typeCode);
                int count = value is Array arr ? arr.Length : 1;
                var request = BuildWriteRequest(ioi, typeCode, count, valueBytes);
                requests.Add(request);
            }

            // Build multi-service packet
            var multiServiceRequest = BuildMultiServiceRequest(requests);

            // Send request
            var (status, data) = conn.Send(multiServiceRequest);

            if (status != 0 || data == null)
            {
                foreach (var (tag, value, _) in tagList)
                {
                    results.Add(new Response(tag, value, status));
                }
                return results;
            }

            // Parse multi-service response
            return ParseMultiServiceWriteResponse(tagList, data);
        }

        private byte[] BuildWriteRequest(byte[] ioi, byte typeCode, int count, byte[] valueBytes)
        {
            var request = new List<byte>();

            // Service - Write Tag (0x4D)
            request.Add(CIPServices.WriteTag);

            // Path size (in words)
            request.Add((byte)(ioi.Length / 2));

            // IOI
            request.AddRange(ioi);

            // Data type
            request.Add(typeCode);
            request.Add(0x00); // Reserved

            // Number of elements
            request.Add((byte)(count & 0xFF));
            request.Add((byte)((count >> 8) & 0xFF));

            // Value data
            request.AddRange(valueBytes);

            return request.ToArray();
        }

        private byte GuessDataType(object value)
        {
            if (value == null)
                return CIPTypes.UNKNOWN;

            switch (value)
            {
                case bool _:
                    return CIPTypes.BOOL;
                case sbyte _:
                    return CIPTypes.SINT;
                case short _:
                    return CIPTypes.INT;
                case int _:
                    return CIPTypes.DINT;
                case long _:
                    return CIPTypes.LINT;
                case byte _:
                    return CIPTypes.USINT;
                case ushort _:
                    return CIPTypes.UINT;
                case uint _:
                    return CIPTypes.UDINT;
                case ulong _:
                    return CIPTypes.LWORD;
                case float _:
                    return CIPTypes.REAL;
                case double _:
                    return CIPTypes.LREAL;
                case string _:
                    return CIPTypes.STRING;
                case bool[] _:
                    return CIPTypes.BOOL;
                case int[] _:
                    return CIPTypes.DINT;
                case float[] _:
                    return CIPTypes.REAL;
                default:
                    return CIPTypes.DINT; // Default to DINT
            }
        }

        private byte[] ConvertToBytes(object value, byte typeCode)
        {
            if (value is Array arr)
            {
                return ConvertArrayToBytes(arr, typeCode);
            }

            switch (typeCode)
            {
                case CIPTypes.BOOL:
                    return new byte[] { (byte)(Convert.ToBoolean(value) ? 1 : 0) };

                case CIPTypes.SINT:
                    return new byte[] { (byte)Convert.ToSByte(value) };

                case CIPTypes.INT:
                    return BitConverter.GetBytes(Convert.ToInt16(value));

                case CIPTypes.DINT:
                    return BitConverter.GetBytes(Convert.ToInt32(value));

                case CIPTypes.LINT:
                    return BitConverter.GetBytes(Convert.ToInt64(value));

                case CIPTypes.USINT:
                case CIPTypes.BYTE:
                    return new byte[] { Convert.ToByte(value) };

                case CIPTypes.UINT:
                case CIPTypes.WORD:
                    return BitConverter.GetBytes(Convert.ToUInt16(value));

                case CIPTypes.UDINT:
                case CIPTypes.DWORD:
                    return BitConverter.GetBytes(Convert.ToUInt32(value));

                case CIPTypes.LWORD:
                    return BitConverter.GetBytes(Convert.ToUInt64(value));

                case CIPTypes.REAL:
                    return BitConverter.GetBytes(Convert.ToSingle(value));

                case CIPTypes.LREAL:
                    return BitConverter.GetBytes(Convert.ToDouble(value));

                case CIPTypes.STRING:
                    return ConvertStringToBytes(value.ToString() ?? string.Empty);

                default:
                    return BitConverter.GetBytes(Convert.ToInt32(value));
            }
        }

        private byte[] ConvertArrayToBytes(Array arr, byte typeCode)
        {
            var bytes = new List<byte>();
            int elementSize = CIPTypes.GetSize(typeCode);

            foreach (var item in arr)
            {
                bytes.AddRange(ConvertToBytes(item, typeCode));
            }

            return bytes.ToArray();
        }

        private byte[] ConvertStringToBytes(string value)
        {
            // Logix STRING structure: 4-byte length + 82 bytes data (padded)
            var bytes = new byte[88];
            var strBytes = Encoding.GetEncoding(StringEncoding).GetBytes(value);
            int length = Math.Min(strBytes.Length, 82);

            // Write length
            BitConverter.GetBytes(length).CopyTo(bytes, 0);

            // Write string data
            Array.Copy(strBytes, 0, bytes, 4, length);

            return bytes;
        }

        private List<Response> ParseMultiServiceWriteResponse(List<(string tag, object value, byte? datatype)> tags, byte[] data)
        {
            var results = new List<Response>();

            try
            {
                // Multi-service response parsing
                int offset = 50; // Start of CIP data

                // Skip service reply header
                offset += 6;

                int replyCount = BitConverter.ToUInt16(data, offset - 2);

                // Get offsets for each reply
                var offsets = new List<int>();
                for (int i = 0; i < replyCount && i < tags.Count; i++)
                {
                    offsets.Add(BitConverter.ToUInt16(data, offset));
                    offset += 2;
                }

                // Parse each reply
                int baseOffset = offset - 2 - (replyCount * 2);
                for (int i = 0; i < offsets.Count && i < tags.Count; i++)
                {
                    int replyOffset = baseOffset + offsets[i];

                    if (replyOffset + 4 > data.Length)
                    {
                        results.Add(new Response(tags[i].tag, tags[i].value, "Invalid response"));
                        continue;
                    }

                    byte status = data[replyOffset + 2];
                    results.Add(new Response(tags[i].tag, tags[i].value, status));
                }

                // Fill in any missing responses
                while (results.Count < tags.Count)
                {
                    results.Add(new Response(tags[results.Count].tag, tags[results.Count].value, "No response"));
                }
            }
            catch (Exception ex)
            {
                while (results.Count < tags.Count)
                {
                    results.Add(new Response(tags[results.Count].tag, tags[results.Count].value, ex.Message));
                }
            }

            return results;
        }

        #endregion

        #region Tag List Operations

        /// <summary>
        /// Get the list of tags from the PLC.
        /// </summary>
        /// <param name="allTags">If true, includes program-scoped tags; if false, controller tags only.</param>
        /// <returns>Response with Value as List of Tag.</returns>
        public Response GetTagList(bool allTags = true)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            var tags = new List<Tag>();
            ushort instanceId = 0;

            while (true)
            {
                var request = BuildGetAttributeListRequest(instanceId);
                var (status, data) = conn.Send(request);

                if (data == null)
                {
                    break;
                }

                // Check for partial transfer (more data available)
                bool moreData = (status == 0x06);

                // Parse tags from response
                var parsedTags = ParseTagListResponse(data, null);
                foreach (var tag in parsedTags)
                {
                    if (!Tag.InFilter(tag.TagName))
                    {
                        tags.Add(tag);
                    }
                    instanceId = (ushort)(tag.InstanceID + 1);
                }

                if (!moreData || parsedTags.Count == 0)
                {
                    break;
                }
            }

            // If allTags requested, also get program tags
            if (allTags)
            {
                var programsResponse = GetProgramsList();
                if (programsResponse.Status == "Success" && programsResponse.Value is List<string> programs)
                {
                    foreach (var program in programs)
                    {
                        var programTagsResponse = GetProgramTagList(program);
                        if (programTagsResponse.Status == "Success" && programTagsResponse.Value is List<Tag> programTags)
                        {
                            tags.AddRange(programTags);
                        }
                    }
                }
            }

            return new Response(null, tags, 0);
        }

        /// <summary>
        /// Get the list of tags for a specific program.
        /// </summary>
        /// <param name="programName">Program name (e.g., "Program:MainProgram").</param>
        /// <returns>Response with Value as List of Tag.</returns>
        public Response GetProgramTagList(string programName)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            var tags = new List<Tag>();
            ushort instanceId = 0;

            while (true)
            {
                var request = BuildGetAttributeListRequest(instanceId, programName);
                var (status, data) = conn.Send(request);

                if (data == null)
                {
                    break;
                }

                bool moreData = (status == 0x06);
                var parsedTags = ParseTagListResponse(data, programName);
                foreach (var tag in parsedTags)
                {
                    if (!Tag.InFilter(tag.TagName))
                    {
                        tags.Add(tag);
                    }
                    instanceId = (ushort)(tag.InstanceID + 1);
                }

                if (!moreData || parsedTags.Count == 0)
                {
                    break;
                }
            }

            return new Response(null, tags, 0);
        }

        /// <summary>
        /// Get the list of program names from the PLC.
        /// </summary>
        /// <returns>Response with Value as List of string.</returns>
        public Response GetProgramsList()
        {
            var tagListResponse = GetTagList(false);
            if (tagListResponse.Status != "Success")
            {
                return tagListResponse;
            }

            var programs = new List<string>();
            if (tagListResponse.Value is List<Tag> tags)
            {
                foreach (var tag in tags)
                {
                    if (tag.TagName.StartsWith("Program:"))
                    {
                        programs.Add(tag.TagName);
                    }
                }
            }

            return new Response(null, programs, 0);
        }

        private byte[] BuildGetAttributeListRequest(ushort instanceId, string? programName = null)
        {
            var request = new List<byte>();

            // Service - Get Instance Attribute List (0x55)
            request.Add(CIPServices.GetInstanceAttributeList);

            // Build path
            var path = new List<byte>();

            // If program name specified, add program path
            if (!string.IsNullOrEmpty(programName))
            {
                var programBytes = Encoding.ASCII.GetBytes(programName);
                path.Add(0x91); // Symbolic segment
                path.Add((byte)programBytes.Length);
                path.AddRange(programBytes);
                if (programBytes.Length % 2 != 0)
                    path.Add(0x00);
            }

            // Symbol class (0x6B)
            path.Add(0x20);
            path.Add(CIPClasses.Symbol);

            // Instance
            if (instanceId < 256)
            {
                path.Add(0x24);
                path.Add((byte)instanceId);
            }
            else
            {
                path.Add(0x25);
                path.Add(0x00);
                path.Add((byte)(instanceId & 0xFF));
                path.Add((byte)((instanceId >> 8) & 0xFF));
            }

            // Path size
            request.Add((byte)(path.Count / 2));
            request.AddRange(path);

            // Number of attributes to retrieve
            request.Add(0x03);
            request.Add(0x00);

            // Attribute 1: Symbol Name
            request.Add(0x01);
            request.Add(0x00);

            // Attribute 2: Symbol Type
            request.Add(0x02);
            request.Add(0x00);

            // Attribute 8: Array dimensions (if array)
            request.Add(0x08);
            request.Add(0x00);

            return request.ToArray();
        }

        private List<Tag> ParseTagListResponse(byte[] data, string? programName)
        {
            var tags = new List<Tag>();
            try
            {
                int offset = 50; // Start of CIP data

                while (offset + 6 < data.Length)
                {
                    var tag = new Tag();

                    // Instance ID
                    tag.InstanceID = BitConverter.ToUInt16(data, offset);
                    offset += 2;

                    // Skip attribute count and status
                    offset += 4;

                    // Attribute 1: Name
                    int nameLen = BitConverter.ToUInt16(data, offset);
                    offset += 2;
                    if (offset + nameLen > data.Length) break;

                    string name = Encoding.ASCII.GetString(data, offset, nameLen);
                    offset += nameLen;
                    if (nameLen % 2 != 0) offset++; // Pad

                    // Set tag name with optional program prefix
                    if (!string.IsNullOrEmpty(programName))
                    {
                        tag.TagName = $"{programName}.{name}";
                    }
                    else
                    {
                        tag.TagName = name;
                    }

                    // Attribute 2: Type
                    if (offset + 4 > data.Length) break;
                    offset += 2; // Skip attribute status
                    ushort typeVal = BitConverter.ToUInt16(data, offset);
                    offset += 2;

                    tag.SymbolType = (byte)(typeVal & 0xFF);
                    tag.DataTypeValue = (ushort)(typeVal & 0x0FFF);
                    tag.Array = (typeVal & 0x6000) >> 13;
                    tag.Struct = (typeVal & 0x8000) >> 15;
                    tag.DataType = CIPTypes.GetName(tag.SymbolType);

                    // Attribute 8: Array size (if array)
                    if (tag.Array != 0 && offset + 4 <= data.Length)
                    {
                        offset += 2; // Skip attribute status
                        tag.Size = BitConverter.ToUInt16(data, offset);
                        offset += 2;
                    }

                    tags.Add(tag);
                }
            }
            catch
            {
                // Parsing error - return what we have
            }

            return tags;
        }

        #endregion

        #region Discovery Operations

        /// <summary>
        /// Discover all EtherNet/IP devices on the network.
        /// </summary>
        /// <returns>Response with Value as List of Device.</returns>
        public Response Discover()
        {
            var devices = new List<Device>();

            try
            {
                var request = BuildListIdentityRequest();

                // Use UDP broadcast
                using (var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram,
                    System.Net.Sockets.ProtocolType.Udp))
                {
                    socket.SetSocketOption(
                        System.Net.Sockets.SocketOptionLevel.Socket,
                        System.Net.Sockets.SocketOptionName.Broadcast,
                        true);
                    socket.ReceiveTimeout = 500;

                    var broadcast = new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, Port);
                    socket.SendTo(request, broadcast);

                    var buffer = new byte[4096];
                    System.Net.EndPoint remoteEp = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);

                    while (true)
                    {
                        try
                        {
                            int received = socket.ReceiveFrom(buffer, ref remoteEp);
                            if (received > 28)
                            {
                                var responseData = new byte[received];
                                Array.Copy(buffer, responseData, received);

                                // Check context to verify it's our response
                                ulong context = BitConverter.ToUInt64(responseData, 12);
                                if (context == 0x006d6f4d6948) // "HiMom" context
                                {
                                    var device = Device.Parse(responseData);
                                    if (!string.IsNullOrEmpty(device.IPAddress))
                                    {
                                        devices.Add(device);
                                    }
                                }
                            }
                        }
                        catch (System.Net.Sockets.SocketException)
                        {
                            // Timeout - no more responses
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new Response(null, devices, ex.Message);
            }

            return new Response(null, devices, 0);
        }

        /// <summary>
        /// Get properties of a module in a specific slot.
        /// </summary>
        /// <param name="slot">Slot number.</param>
        /// <returns>Response with Value as Device.</returns>
        public Response GetModuleProperties(int slot)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect(false); // Unconnected messaging
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            var request = BuildGetAttributesAllRequest();
            var (status, data) = conn.Send(request, false, slot);

            if (status != 0 || data == null)
            {
                return new Response(null, null, status);
            }

            try
            {
                var device = ParseModuleResponse(data);
                return new Response(null, device, 0);
            }
            catch (Exception ex)
            {
                return new Response(null, null, ex.Message);
            }
        }

        /// <summary>
        /// Get properties of the device at the configured IP address.
        /// </summary>
        /// <returns>Response with Value as Device.</returns>
        public Response GetDeviceProperties()
        {
            return GetModuleProperties(ProcessorSlot);
        }

        private byte[] BuildListIdentityRequest()
        {
            var packet = new byte[24];
            int offset = 0;

            // Command - List Identity (0x0063)
            packet[offset++] = 0x63;
            packet[offset++] = 0x00;

            // Length
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;

            // Session handle
            offset += 4;

            // Status
            offset += 4;

            // Sender context - "HiMom" (for identification)
            packet[offset++] = 0x48; // H
            packet[offset++] = 0x69; // i
            packet[offset++] = 0x4D; // M
            packet[offset++] = 0x6F; // o
            packet[offset++] = 0x6D; // m
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;

            // Options
            offset += 4;

            return packet;
        }

        private byte[] BuildGetAttributesAllRequest()
        {
            var request = new List<byte>();

            // Service - Get Attributes All (0x01)
            request.Add(CIPServices.GetAttributesAll);

            // Path size
            request.Add(0x02);

            // Identity class (0x01)
            request.Add(0x20);
            request.Add(CIPClasses.Identity);

            // Instance 1
            request.Add(0x24);
            request.Add(0x01);

            return request.ToArray();
        }

        private Device ParseModuleResponse(byte[] data)
        {
            var device = new Device();
            int offset = 44; // Start of CIP response data

            if (offset + 14 > data.Length)
            {
                return device;
            }

            // Vendor ID
            device.VendorID = BitConverter.ToUInt16(data, offset);
            device.Vendor = Device.GetVendor(device.VendorID);
            offset += 2;

            // Device Type
            device.DeviceID = BitConverter.ToUInt16(data, offset);
            device.DeviceType = Device.GetDeviceType(device.DeviceID);
            offset += 2;

            // Product Code
            device.ProductCode = BitConverter.ToUInt16(data, offset);
            offset += 2;

            // Revision
            byte major = data[offset++];
            byte minor = data[offset++];
            device.Revision = $"{major}.{minor}";

            // Status
            device.Status = BitConverter.ToUInt16(data, offset);
            offset += 2;

            // Serial Number
            uint serial = BitConverter.ToUInt32(data, offset);
            device.SerialNumber = $"0x{serial:X8}";
            offset += 4;

            // Product Name Length
            device.ProductNameLength = data[offset++];

            // Product Name
            if (offset + device.ProductNameLength <= data.Length)
            {
                device.ProductName = Encoding.ASCII.GetString(data, offset, device.ProductNameLength);
            }

            return device;
        }

        #endregion

        #region Time Operations

        /// <summary>
        /// Get the PLC clock time.
        /// </summary>
        /// <param name="raw">If true, returns microseconds since epoch; if false, returns DateTime.</param>
        /// <returns>Response with Value as DateTime or long.</returns>
        public Response GetPLCTime(bool raw = false)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            var request = BuildGetTimeRequest();
            var (status, data) = conn.Send(request);

            if (status != 0 || data == null)
            {
                return new Response(null, null, status);
            }

            try
            {
                int offset = 50; // CIP data start

                // Read 8-byte timestamp (microseconds since 1970)
                long microseconds = BitConverter.ToInt64(data, offset);

                if (raw)
                {
                    return new Response(null, microseconds, 0);
                }

                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var time = epoch.AddTicks(microseconds * 10); // Convert microseconds to ticks

                return new Response(null, time, 0);
            }
            catch (Exception ex)
            {
                return new Response(null, null, ex.Message);
            }
        }

        /// <summary>
        /// Set the PLC clock time to current system time.
        /// </summary>
        /// <param name="dst">Daylight saving time flag (null = auto-detect).</param>
        /// <returns>Response indicating success or failure.</returns>
        public Response SetPLCTime(int? dst = null)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            // Calculate microseconds since epoch
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = DateTime.UtcNow;
            long microseconds = (now - epoch).Ticks / 10;

            var request = BuildSetTimeRequest(microseconds);
            var (status, _) = conn.Send(request);

            return new Response(null, now, status);
        }

        private byte[] BuildGetTimeRequest()
        {
            var request = new List<byte>();

            // Service - Get Attribute Single (0x0E)
            request.Add(CIPServices.GetAttributeSingle);

            // Path size
            request.Add(0x03);

            // Wall Clock Time class (0x8B)
            request.Add(0x20);
            request.Add(CIPClasses.WallClockTime);

            // Instance 1
            request.Add(0x24);
            request.Add(0x01);

            // Attribute 5 (DateTime)
            request.Add(0x30);
            request.Add(0x05);

            return request.ToArray();
        }

        private byte[] BuildSetTimeRequest(long microseconds)
        {
            var request = new List<byte>();

            // Service - Set Attribute Single (0x10)
            request.Add(CIPServices.SetAttributeSingle);

            // Path size
            request.Add(0x03);

            // Wall Clock Time class (0x8B)
            request.Add(0x20);
            request.Add(CIPClasses.WallClockTime);

            // Instance 1
            request.Add(0x24);
            request.Add(0x01);

            // Attribute 5 (DateTime)
            request.Add(0x30);
            request.Add(0x05);

            // Value (8 bytes)
            request.AddRange(BitConverter.GetBytes(microseconds));

            return request.ToArray();
        }

        #endregion

        #region Custom Messaging

        /// <summary>
        /// Send a custom CIP message.
        /// </summary>
        /// <param name="cipService">CIP service code.</param>
        /// <param name="cipClass">CIP class.</param>
        /// <param name="cipInstance">CIP instance.</param>
        /// <param name="cipAttribute">Optional CIP attribute.</param>
        /// <param name="data">Optional data payload.</param>
        /// <returns>Response with raw data.</returns>
        public Response Message(byte cipService, ushort cipClass, ushort cipInstance, byte? cipAttribute = null, byte[]? data = null)
        {
            var conn = GetConnection();
            var connectResult = conn.Connect();
            if (!connectResult.Success)
            {
                return new Response(null, null, connectResult.Message);
            }

            var request = BuildCustomMessage(cipService, cipClass, cipInstance, cipAttribute, data);
            var (status, responseData) = conn.Send(request);

            if (status != 0 || responseData == null)
            {
                return new Response(null, null, status);
            }

            // Extract CIP response data (skip EIP header)
            int dataStart = 50;
            if (dataStart < responseData.Length)
            {
                var cipData = new byte[responseData.Length - dataStart];
                Array.Copy(responseData, dataStart, cipData, 0, cipData.Length);
                return new Response(null, cipData, 0);
            }

            return new Response(null, null, 0);
        }

        private byte[] BuildCustomMessage(byte cipService, ushort cipClass, ushort cipInstance, byte? cipAttribute, byte[]? data)
        {
            var request = new List<byte>();

            // Service
            request.Add(cipService);

            // Calculate path size
            int pathLen = 4; // Class + Instance minimum
            if (cipAttribute.HasValue) pathLen += 2;

            request.Add((byte)(pathLen / 2));

            // Class
            if (cipClass < 256)
            {
                request.Add(0x20);
                request.Add((byte)cipClass);
            }
            else
            {
                request.Add(0x21);
                request.Add(0x00);
                request.Add((byte)(cipClass & 0xFF));
                request.Add((byte)((cipClass >> 8) & 0xFF));
            }

            // Instance
            if (cipInstance < 256)
            {
                request.Add(0x24);
                request.Add((byte)cipInstance);
            }
            else
            {
                request.Add(0x25);
                request.Add(0x00);
                request.Add((byte)(cipInstance & 0xFF));
                request.Add((byte)((cipInstance >> 8) & 0xFF));
            }

            // Attribute (optional)
            if (cipAttribute.HasValue)
            {
                request.Add(0x30);
                request.Add(cipAttribute.Value);
            }

            // Data payload (optional)
            if (data != null && data.Length > 0)
            {
                request.AddRange(data);
            }

            return request.ToArray();
        }

        #endregion

        /// <summary>
        /// Closes the connection to the PLC.
        /// </summary>
        public void Close()
        {
            _connection?.Close();
        }

        #region IDisposable

        /// <summary>
        /// Disposes of resources used by this PLC instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _connection?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
