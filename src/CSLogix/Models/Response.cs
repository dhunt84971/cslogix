using System;
using System.Collections.Generic;

namespace CSLogix.Models
{
    /// <summary>
    /// Response class returned by all PLC operations.
    /// Contains the tag name, value, and status of the operation.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets the name of the tag that was read or written.
        /// </summary>
        public string? TagName { get; }

        /// <summary>
        /// Gets the value read from or written to the tag.
        /// For reads, this contains the tag value(s).
        /// For writes, this contains the value(s) that were written.
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// Gets the status message of the operation.
        /// "Success" indicates the operation completed successfully.
        /// Other values indicate error conditions.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Creates a new Response instance.
        /// </summary>
        /// <param name="tagName">The name of the tag.</param>
        /// <param name="value">The value read or written.</param>
        /// <param name="status">The status code or message.</param>
        public Response(string? tagName, object? value, object status)
        {
            TagName = tagName;
            Value = value;
            Status = GetErrorCode(status);
        }

        /// <summary>
        /// Converts a CIP status code to a human-readable error message.
        /// </summary>
        /// <param name="status">The status code (byte/int) or string message.</param>
        /// <returns>Human-readable status message.</returns>
        public static string GetErrorCode(object status)
        {
            // If status is already a string, return it directly
            if (status is string statusString)
            {
                return statusString;
            }

            // Convert numeric status to byte for lookup
            byte statusCode;
            if (status is byte b)
            {
                statusCode = b;
            }
            else if (status is int i)
            {
                statusCode = (byte)i;
            }
            else
            {
                return $"Unknown error {status}";
            }

            // Look up the error code
            if (CipErrorCodes.TryGetValue(statusCode, out var errorMessage))
            {
                return errorMessage;
            }

            return $"Unknown error {statusCode}";
        }

        /// <summary>
        /// Returns a string representation of the response.
        /// </summary>
        public override string ToString()
        {
            return $"{TagName} {Value} {Status}";
        }

        /// <summary>
        /// CIP (Common Industrial Protocol) error code mappings.
        /// </summary>
        public static readonly Dictionary<byte, string> CipErrorCodes = new Dictionary<byte, string>
        {
            { 0x00, "Success" },
            { 0x01, "Connection failure" },
            { 0x02, "Resource unavailable" },
            { 0x03, "Invalid parameter value" },
            { 0x04, "Path segment error" },
            { 0x05, "Path destination unknown" },
            { 0x06, "Partial transfer" },
            { 0x07, "Connection lost" },
            { 0x08, "Service not supported" },
            { 0x09, "Invalid Attribute" },
            { 0x0A, "Attribute list error" },
            { 0x0B, "Already in requested mode/state" },
            { 0x0C, "Object state conflict" },
            { 0x0D, "Object already exists" },
            { 0x0E, "Attribute not settable" },
            { 0x0F, "Privilege violation" },
            { 0x10, "Device state conflict" },
            { 0x11, "Reply data too large" },
            { 0x12, "Fragmentation of a primitive value" },
            { 0x13, "Not enough data" },
            { 0x14, "Attribute not supported" },
            { 0x15, "Too much data" },
            { 0x16, "Object does not exist" },
            { 0x17, "Service fragmentation sequence not in progress" },
            { 0x18, "No stored attribute data" },
            { 0x19, "Store operation failure" },
            { 0x1A, "Routing failure, request packet too large" },
            { 0x1B, "Routing failure, response packet too large" },
            { 0x1C, "Missing attribute list entry data" },
            { 0x1D, "Invalid attribute value list" },
            { 0x1E, "Embedded service error" },
            { 0x1F, "Vendor specific" },
            { 0x20, "Invalid Parameter" },
            { 0x21, "Write once value or medium already written" },
            { 0x22, "Invalid reply received" },
            { 0x23, "Buffer overflow" },
            { 0x24, "Invalid message format" },
            { 0x25, "Key failure in path" },
            { 0x26, "Path size invalid" },
            { 0x27, "Unexpected attribute in list" },
            { 0x28, "Invalid member ID" },
            { 0x29, "Member not settable" },
            { 0x2A, "Group 2 only server general failure" },
            { 0x2B, "Unknown Modbus error" },
            { 0x2C, "Attribute not gettable" }
        };
    }
}
