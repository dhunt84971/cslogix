using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSLogix.Constants;

namespace CSLogix.Tests.Integration
{
    /// <summary>
    /// A mock PLC server for integration testing.
    /// Simulates EtherNet/IP protocol responses without requiring real hardware.
    /// </summary>
    public class MockPLCServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts;
        private Task? _acceptTask;
        private readonly List<TcpClient> _clients;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// The port the mock server is listening on.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// The IP address the mock server is listening on.
        /// </summary>
        public string IPAddress => "127.0.0.1";

        /// <summary>
        /// Simulated tag values (tag name -> value).
        /// </summary>
        public Dictionary<string, object> Tags { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Number of connections received.
        /// </summary>
        public int ConnectionCount { get; private set; }

        /// <summary>
        /// Number of requests received.
        /// </summary>
        public int RequestCount { get; private set; }

        /// <summary>
        /// Creates a new mock PLC server on a random available port.
        /// </summary>
        public MockPLCServer() : this(0) { }

        /// <summary>
        /// Creates a new mock PLC server on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on. Use 0 for random available port.</param>
        public MockPLCServer(int port)
        {
            _cts = new CancellationTokenSource();
            _clients = new List<TcpClient>();
            _listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            // Initialize some default tags
            Tags["TestDINT"] = 12345;
            Tags["TestREAL"] = 3.14159f;
            Tags["TestBOOL"] = true;
            Tags["TestArray[0]"] = 100;
            Tags["TestArray[1]"] = 200;
            Tags["TestArray[2]"] = 300;
            Tags["TestString"] = "Hello PLC";

            _acceptTask = AcceptClientsAsync();
        }

        private async Task AcceptClientsAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    lock (_lock)
                    {
                        _clients.Add(client);
                        ConnectionCount++;
                    }
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Expected when disposed
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[4096];
                uint sessionHandle = 0;

                while (!_cts.Token.IsCancellationRequested && client.Connected)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token);
                    }
                    catch
                    {
                        break;
                    }

                    if (bytesRead == 0)
                        break;

                    RequestCount++;

                    // Parse EIP command
                    ushort command = BitConverter.ToUInt16(buffer, 0);
                    byte[] response;

                    switch (command)
                    {
                        case EIPCommands.RegisterSession:
                            sessionHandle = (uint)new Random().Next(1, 65535);
                            response = BuildRegisterSessionResponse(sessionHandle);
                            break;

                        case EIPCommands.UnregisterSession:
                            response = Array.Empty<byte>();
                            break;

                        case EIPCommands.SendRRData:
                            response = HandleSendRRData(buffer, bytesRead, sessionHandle);
                            break;

                        case EIPCommands.SendUnitData:
                            response = HandleSendUnitData(buffer, bytesRead, sessionHandle);
                            break;

                        default:
                            response = BuildErrorResponse(sessionHandle, 0x01);
                            break;
                    }

                    if (response.Length > 0)
                    {
                        await stream.WriteAsync(response.AsMemory(0, response.Length), _cts.Token);
                    }
                }
            }
            catch (Exception)
            {
                // Client disconnected or error
            }
            finally
            {
                lock (_lock)
                {
                    _clients.Remove(client);
                }
                client.Dispose();
            }
        }

        private byte[] BuildRegisterSessionResponse(uint sessionHandle)
        {
            var response = new byte[28];
            int offset = 0;

            // Command
            WriteUInt16(response, ref offset, EIPCommands.RegisterSession);
            // Length
            WriteUInt16(response, ref offset, 4);
            // Session handle
            WriteUInt32(response, ref offset, sessionHandle);
            // Status
            WriteUInt32(response, ref offset, 0);
            // Sender context
            offset += 8;
            // Options
            WriteUInt32(response, ref offset, 0);
            // Protocol version
            WriteUInt16(response, ref offset, 1);
            // Options flags
            WriteUInt16(response, ref offset, 0);

            return response;
        }

        private byte[] HandleSendRRData(byte[] request, int length, uint sessionHandle)
        {
            // Parse the CIP service from the unconnected data
            // Minimal parsing - look for Forward Open/Close or Read/Write
            int serviceOffset = 40; // After EIP header and RR data header
            if (length <= serviceOffset)
                return BuildRRDataResponse(sessionHandle, 0, Array.Empty<byte>());

            byte service = request[serviceOffset];

            switch (service)
            {
                case CIPServices.ForwardOpen:
                case CIPServices.LargeForwardOpen:
                    return BuildForwardOpenResponse(sessionHandle);

                case CIPServices.ForwardClose:
                    return BuildForwardCloseResponse(sessionHandle);

                default:
                    // Generic CIP request handling
                    return HandleCIPRequest(request, serviceOffset, length, sessionHandle, false);
            }
        }

        private byte[] HandleSendUnitData(byte[] request, int length, uint sessionHandle)
        {
            // Connected messaging - service is at offset 44 (after sequence number)
            int serviceOffset = 44;
            if (length <= serviceOffset)
                return BuildUnitDataResponse(sessionHandle, 0, Array.Empty<byte>());

            return HandleCIPRequest(request, serviceOffset, length, sessionHandle, true);
        }

        private byte[] HandleCIPRequest(byte[] request, int serviceOffset, int length, uint sessionHandle, bool connected)
        {
            byte service = request[serviceOffset];
            byte[] data;

            switch (service)
            {
                case CIPServices.ReadTag:
                    data = HandleReadTag(request, serviceOffset);
                    break;

                case CIPServices.WriteTag:
                    data = HandleWriteTag(request, serviceOffset);
                    break;

                case CIPServices.MultipleServicePacket:
                    data = HandleMultipleService(request, serviceOffset);
                    break;

                case CIPServices.GetInstanceAttributeList:
                    data = HandleGetTagList();
                    break;

                default:
                    data = Array.Empty<byte>();
                    break;
            }

            if (connected)
                return BuildUnitDataResponse(sessionHandle, 0, data);
            else
                return BuildRRDataResponse(sessionHandle, 0, data);
        }

        private byte[] HandleReadTag(byte[] request, int offset)
        {
            // Parse tag name from IOI path
            // Skip service byte, path size
            offset++;
            byte pathSize = request[offset++];

            // For simplicity, return a DINT value
            var data = new List<byte>();
            data.Add((byte)(CIPServices.ReadTag | 0x80)); // Reply service
            data.Add(0x00); // Reserved
            data.Add(0x00); // Status
            data.Add(0x00); // Additional status size
            data.Add(CIPTypes.DINT); // Type
            data.Add(0x00); // Padding
            // Value (DINT = 12345 = 0x3039)
            data.Add(0x39);
            data.Add(0x30);
            data.Add(0x00);
            data.Add(0x00);

            return data.ToArray();
        }

        private byte[] HandleWriteTag(byte[] request, int offset)
        {
            // Acknowledge the write
            var data = new List<byte>();
            data.Add((byte)(CIPServices.WriteTag | 0x80)); // Reply service
            data.Add(0x00); // Reserved
            data.Add(0x00); // Status (success)
            data.Add(0x00); // Additional status size

            return data.ToArray();
        }

        private byte[] HandleMultipleService(byte[] request, int offset)
        {
            // Return empty multiple service response
            var data = new List<byte>();
            data.Add((byte)(CIPServices.MultipleServicePacket | 0x80));
            data.Add(0x00);
            data.Add(0x00);
            data.Add(0x00);
            // Service count = 0
            data.Add(0x00);
            data.Add(0x00);

            return data.ToArray();
        }

        private byte[] HandleGetTagList()
        {
            // Return empty tag list (status indicates no more data)
            var data = new List<byte>();
            data.Add((byte)(CIPServices.GetInstanceAttributeList | 0x80));
            data.Add(0x00);
            data.Add(0x00); // Status - success, no more data
            data.Add(0x00);

            return data.ToArray();
        }

        private byte[] BuildForwardOpenResponse(uint sessionHandle)
        {
            // Build a successful forward open response
            var cipData = new List<byte>();
            cipData.Add((byte)(CIPServices.ForwardOpen | 0x80)); // Reply
            cipData.Add(0x00); // Reserved
            cipData.Add(0x00); // Status (success)
            cipData.Add(0x00); // Additional status size
            // O->T Connection ID
            cipData.Add(0x01);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x20);
            // T->O Connection ID
            cipData.Add(0x02);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x20);
            // Connection serial number
            cipData.Add(0x01);
            cipData.Add(0x00);
            // Vendor ID
            cipData.Add(0x01);
            cipData.Add(0x00);
            // Originator serial
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            // O->T API
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            // T->O API
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            // Application reply size
            cipData.Add(0x00);
            // Reserved
            cipData.Add(0x00);

            return BuildRRDataResponse(sessionHandle, 0, cipData.ToArray());
        }

        private byte[] BuildForwardCloseResponse(uint sessionHandle)
        {
            var cipData = new List<byte>();
            cipData.Add((byte)(CIPServices.ForwardClose | 0x80));
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            // Connection serial
            cipData.Add(0x01);
            cipData.Add(0x00);
            // Vendor ID
            cipData.Add(0x01);
            cipData.Add(0x00);
            // Originator serial
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);
            cipData.Add(0x00);

            return BuildRRDataResponse(sessionHandle, 0, cipData.ToArray());
        }

        private byte[] BuildRRDataResponse(uint sessionHandle, byte status, byte[] cipData)
        {
            int payloadLen = 16 + cipData.Length;
            var response = new byte[24 + payloadLen];
            int offset = 0;

            // EIP Header
            WriteUInt16(response, ref offset, EIPCommands.SendRRData);
            WriteUInt16(response, ref offset, (ushort)payloadLen);
            WriteUInt32(response, ref offset, sessionHandle);
            WriteUInt32(response, ref offset, 0); // Status
            offset += 8; // Sender context
            WriteUInt32(response, ref offset, 0); // Options

            // RR Data
            WriteUInt32(response, ref offset, 0); // Interface handle
            WriteUInt16(response, ref offset, 0); // Timeout
            WriteUInt16(response, ref offset, 2); // Item count
            WriteUInt16(response, ref offset, 0); // Item 1 type (null)
            WriteUInt16(response, ref offset, 0); // Item 1 length
            WriteUInt16(response, ref offset, 0x00B2); // Item 2 type (unconnected data)
            WriteUInt16(response, ref offset, (ushort)cipData.Length);

            Buffer.BlockCopy(cipData, 0, response, offset, cipData.Length);

            return response;
        }

        private byte[] BuildUnitDataResponse(uint sessionHandle, byte status, byte[] cipData)
        {
            int connDataLen = cipData.Length + 2;
            int payloadLen = 16 + connDataLen + 4;
            var response = new byte[24 + payloadLen];
            int offset = 0;

            // EIP Header
            WriteUInt16(response, ref offset, EIPCommands.SendUnitData);
            WriteUInt16(response, ref offset, (ushort)payloadLen);
            WriteUInt32(response, ref offset, sessionHandle);
            WriteUInt32(response, ref offset, 0);
            offset += 8;
            WriteUInt32(response, ref offset, 0);

            // Unit data header
            WriteUInt32(response, ref offset, 0);
            WriteUInt16(response, ref offset, 0);
            WriteUInt16(response, ref offset, 2);
            WriteUInt16(response, ref offset, 0x00A1);
            WriteUInt16(response, ref offset, 4);
            WriteUInt32(response, ref offset, 0x20000001);
            WriteUInt16(response, ref offset, 0x00B1);
            WriteUInt16(response, ref offset, (ushort)connDataLen);
            WriteUInt16(response, ref offset, 1); // Sequence

            Buffer.BlockCopy(cipData, 0, response, offset, cipData.Length);

            return response;
        }

        private byte[] BuildErrorResponse(uint sessionHandle, byte status)
        {
            var response = new byte[24];
            int offset = 0;

            WriteUInt16(response, ref offset, 0);
            WriteUInt16(response, ref offset, 0);
            WriteUInt32(response, ref offset, sessionHandle);
            WriteUInt32(response, ref offset, status);

            return response;
        }

        private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
            buffer[offset++] = (byte)((value >> 16) & 0xFF);
            buffer[offset++] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// Stops the mock server and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            _listener.Stop();

            lock (_lock)
            {
                foreach (var client in _clients)
                {
                    try { client.Dispose(); } catch { }
                }
                _clients.Clear();
            }

            try
            {
                _acceptTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch { }

            _cts.Dispose();
        }
    }
}
