using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using CSLogix.Constants;
using CSLogix.Models;

namespace CSLogix
{
    /// <summary>
    /// Manages the EtherNet/IP connection to a PLC.
    /// Handles session registration, forward open/close, and packet transmission.
    /// </summary>
    internal class Connection : IDisposable
    {
        private readonly PLC _parent;
        private Socket? _socket;
        private bool _disposed;

        /// <summary>
        /// The connection size in bytes. Defaults to auto-negotiated.
        /// 508 for standard connections, 4002 for large connections.
        /// </summary>
        public int? ConnectionSize { get; set; }

        /// <summary>
        /// Indicates whether the socket is connected.
        /// </summary>
        public bool SocketConnected { get; private set; }

        /// <summary>
        /// Indicates whether a CIP connected session is established.
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// Indicates whether the session is registered.
        /// </summary>
        public bool Registered { get; private set; }

        // Session state
        private uint _sessionHandle;
        private ulong _context;
        private int _contextIndex;
        private uint _otConnectionId;
        private uint _toConnectionId;
        private ushort _serialNumber;
        private ushort _sequenceCounter = 1;

        // Constants
        private const ushort VendorId = 0x1337;
        private const uint OriginatorSerial = 42;
        private static readonly Random _random = new Random();

        /// <summary>
        /// Context values passed to the PLC when reading/writing.
        /// </summary>
        private static readonly ulong[] ContextValues = new ulong[]
        {
            0x6572276557, 0x6f6e, 0x676e61727473, 0x737265, 0x6f74,
            0x65766f6c, 0x756f59, 0x776f6e6b, 0x656874, 0x73656c7572,
            0x646e61, 0x6f73, 0x6f64, 0x49, 0x41,
            0x6c6c7566, 0x74696d6d6f63, 0x7327746e656d, 0x74616877, 0x6d2749,
            0x6b6e696874, 0x676e69, 0x666f, 0x756f59, 0x746e646c756f77,
            0x746567, 0x73696874, 0x6d6f7266, 0x796e61, 0x726568746f,
            0x797567, 0x49, 0x7473756a, 0x616e6e6177, 0x6c6c6574,
            0x756f79, 0x776f68, 0x6d2749, 0x676e696c656566, 0x6174746f47,
            0x656b616d, 0x756f79, 0x7265646e75, 0x646e617473, 0x726576654e,
            0x616e6e6f67, 0x65766967, 0x756f79, 0x7075, 0x726576654e
        };

        /// <summary>
        /// Creates a new Connection instance.
        /// </summary>
        /// <param name="parent">The parent PLC instance.</param>
        public Connection(PLC parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        /// <summary>
        /// Connect to the PLC.
        /// </summary>
        /// <param name="connected">If true, establish a CIP connected session (ForwardOpen).</param>
        /// <returns>Tuple of (success, message).</returns>
        public (bool Success, string Message) Connect(bool connected = true)
        {
            // If already connected with same connection type, return success
            if (SocketConnected)
            {
                if (connected && !Connected)
                {
                    // Connection type changed, need to close and reconnect
                    CloseConnection();
                }
                else if (!connected && Connected)
                {
                    // Connection type changed, need to close and reconnect
                    CloseConnection();
                }
                else
                {
                    return (true, "Success");
                }
            }

            try
            {
                // Create and connect socket
                CloseSocket();
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.ReceiveTimeout = (int)(_parent.SocketTimeout * 1000);
                _socket.SendTimeout = (int)(_parent.SocketTimeout * 1000);

                var endpoint = new IPEndPoint(IPAddress.Parse(_parent.IPAddress), _parent.Port);
                _socket.Connect(endpoint);
            }
            catch (Exception ex)
            {
                SocketConnected = false;
                _sequenceCounter = 1;
                CloseSocket();
                return (false, ex.Message);
            }

            // Register the session
            var registerPacket = BuildRegisterSession();
            _socket.Send(registerPacket);

            var retData = ReceiveData();
            if (retData != null && retData.Length >= 8)
            {
                _sessionHandle = BitConverter.ToUInt32(retData, 4);
                Registered = true;
            }
            else
            {
                SocketConnected = false;
                return (false, "Register session failed");
            }

            // If connected mode requested, do forward open
            if (connected)
            {
                (bool success, string message) result;

                if (ConnectionSize.HasValue)
                {
                    result = ForwardOpen();
                }
                else
                {
                    // Try large forward open first (4002 bytes)
                    ConnectionSize = 4002;
                    result = ForwardOpen();

                    // If large forward open fails, try standard (504 bytes)
                    if (!result.success)
                    {
                        ConnectionSize = 504;
                        result = ForwardOpen();
                    }
                }

                return result;
            }

            SocketConnected = true;
            return (true, "Success");
        }

        /// <summary>
        /// Send a request to the PLC and receive the response.
        /// </summary>
        /// <param name="request">The request payload.</param>
        /// <param name="connected">Whether to use connected messaging.</param>
        /// <param name="slot">Optional slot for unconnected routing.</param>
        /// <returns>Tuple of (status code, response data).</returns>
        public (byte Status, byte[]? Data) Send(byte[] request, bool connected = true, int? slot = null)
        {
            byte[] eipHeader;

            if (connected)
            {
                eipHeader = BuildEIPHeader(request);
            }
            else
            {
                byte[] frame;
                if (_parent.Route != null || slot.HasValue)
                {
                    var path = BuildUnconnectedPath(slot ?? _parent.ProcessorSlot);
                    if (request.Length % 2 != 0)
                    {
                        // Pad to word boundary
                        var paddedRequest = new byte[request.Length + 1];
                        Buffer.BlockCopy(request, 0, paddedRequest, 0, request.Length);
                        frame = CombineArrays(BuildUnconnectedSend(request.Length), paddedRequest, path);
                    }
                    else
                    {
                        frame = CombineArrays(BuildUnconnectedSend(request.Length), request, path);
                    }
                }
                else
                {
                    frame = request;
                }
                eipHeader = CombineArrays(BuildRRDataHeader(frame.Length), frame);
            }

            return GetBytes(eipHeader, connected);
        }

        /// <summary>
        /// Close the connection to the PLC.
        /// </summary>
        public void Close()
        {
            CloseConnection();
        }

        private void CloseConnection()
        {
            SocketConnected = false;
            try
            {
                if (Connected && _socket != null)
                {
                    var closePacket = BuildForwardClosePacket();
                    _socket.Send(closePacket);
                    ReceiveData();
                    Connected = false;
                }
                if (Registered && _socket != null)
                {
                    var unregisterPacket = BuildUnregisterSession();
                    _socket.Send(unregisterPacket);
                }
            }
            catch
            {
                // Ignore errors during close
            }
            finally
            {
                CloseSocket();
            }
        }

        private void CloseSocket()
        {
            try
            {
                _socket?.Close();
                _socket?.Dispose();
            }
            catch
            {
                // Ignore
            }
            _socket = null;
        }

        private (byte Status, byte[]? Data) GetBytes(byte[] data, bool connected)
        {
            try
            {
                _socket!.Send(data);
                var retData = ReceiveData();
                if (retData != null)
                {
                    byte status = connected
                        ? retData[48]
                        : retData[42];
                    return (status, retData);
                }
                else
                {
                    SocketConnected = false;
                    return (1, null);
                }
            }
            catch (SocketException)
            {
                SocketConnected = false;
                return (1, null);
            }
            catch (IOException)
            {
                SocketConnected = false;
                return (7, null);
            }
        }

        /// <summary>
        /// Receive data from the socket, handling fragmented responses.
        /// </summary>
        public byte[]? ReceiveData()
        {
            try
            {
                var buffer = new byte[4096];
                int received = _socket!.Receive(buffer);

                if (received < 4)
                    return null;

                // Get payload length from header
                ushort payloadLen = BitConverter.ToUInt16(buffer, 2);
                int totalExpected = 24 + payloadLen;

                // If we got all data, return it
                if (received >= totalExpected)
                {
                    var result = new byte[received];
                    Buffer.BlockCopy(buffer, 0, result, 0, received);
                    return result;
                }

                // Need to receive more data
                var fullBuffer = new byte[totalExpected];
                Buffer.BlockCopy(buffer, 0, fullBuffer, 0, received);
                int totalReceived = received;

                while (totalReceived < totalExpected)
                {
                    received = _socket.Receive(buffer);
                    if (received == 0)
                        break;
                    Buffer.BlockCopy(buffer, 0, fullBuffer, totalReceived, received);
                    totalReceived += received;
                }

                return fullBuffer;
            }
            catch
            {
                return null;
            }
        }

        #region Packet Building Methods

        private byte[] BuildRegisterSession()
        {
            var packet = new byte[28];
            int offset = 0;

            // EIP Command - Register Session (0x0065)
            WriteUInt16(packet, ref offset, EIPCommands.RegisterSession);
            // Length
            WriteUInt16(packet, ref offset, 4);
            // Session handle
            WriteUInt32(packet, ref offset, _sessionHandle);
            // Status
            WriteUInt32(packet, ref offset, 0);
            // Sender context (version string)
            var contextBytes = System.Text.Encoding.UTF8.GetBytes("1.0.0   ");
            Buffer.BlockCopy(contextBytes, 0, packet, offset, 8);
            offset += 8;
            // Options
            WriteUInt32(packet, ref offset, 0);
            // Protocol version
            WriteUInt16(packet, ref offset, 1);
            // Option flags
            WriteUInt16(packet, ref offset, 0);

            return packet;
        }

        private byte[] BuildUnregisterSession()
        {
            var packet = new byte[24];
            int offset = 0;

            // EIP Command - Unregister Session (0x0066)
            WriteUInt16(packet, ref offset, EIPCommands.UnregisterSession);
            // Length
            WriteUInt16(packet, ref offset, 0);
            // Session handle
            WriteUInt32(packet, ref offset, _sessionHandle);
            // Status
            WriteUInt32(packet, ref offset, 0);
            // Sender context
            WriteUInt64(packet, ref offset, _context);
            // Options
            WriteUInt32(packet, ref offset, 0);

            return packet;
        }

        internal byte[] BuildRRDataHeader(int frameLen)
        {
            var packet = new byte[40];
            int offset = 0;

            // EIP Command - Send RR Data (0x006F)
            WriteUInt16(packet, ref offset, EIPCommands.SendRRData);
            // Length
            WriteUInt16(packet, ref offset, (ushort)(16 + frameLen));
            // Session handle
            WriteUInt32(packet, ref offset, _sessionHandle);
            // Status
            WriteUInt32(packet, ref offset, 0);
            // Sender context
            WriteUInt64(packet, ref offset, _context);
            // Options
            WriteUInt32(packet, ref offset, 0);
            // Interface handle
            WriteUInt32(packet, ref offset, 0);
            // Timeout
            WriteUInt16(packet, ref offset, 0);
            // Item count
            WriteUInt16(packet, ref offset, 2);
            // Item 1 type (Null Address)
            WriteUInt16(packet, ref offset, 0);
            // Item 1 length
            WriteUInt16(packet, ref offset, 0);
            // Item 2 type (Unconnected Data)
            WriteUInt16(packet, ref offset, 0xB2);
            // Item 2 length
            WriteUInt16(packet, ref offset, (ushort)frameLen);

            return packet;
        }

        private byte[] BuildUnconnectedSend(int serviceSize)
        {
            var packet = new byte[10];
            int offset = 0;

            // Service - Unconnected Send (0x52)
            packet[offset++] = CIPServices.UnconnectedSend;
            // Path size
            packet[offset++] = 0x02;
            // Class type (8-bit)
            packet[offset++] = 0x20;
            // Class - Connection Manager
            packet[offset++] = CIPClasses.ConnectionManager;
            // Instance type (8-bit)
            packet[offset++] = 0x24;
            // Instance
            packet[offset++] = 0x01;
            // Priority
            packet[offset++] = 0x0A;
            // Timeout ticks
            packet[offset++] = 0xFF;
            // Service size
            WriteUInt16(packet, ref offset, (ushort)serviceSize);

            return packet;
        }

        private byte[] BuildEIPHeader(byte[] ioi)
        {
            if (_contextIndex >= ContextValues.Length)
                _contextIndex = 0;

            int connDataLen = ioi.Length + 2;
            var packet = new byte[44 + ioi.Length];
            int offset = 0;

            // EIP Command - Send Unit Data (0x0070)
            WriteUInt16(packet, ref offset, EIPCommands.SendUnitData);
            // Length
            WriteUInt16(packet, ref offset, (ushort)(22 + ioi.Length));
            // Session handle
            WriteUInt32(packet, ref offset, _sessionHandle);
            // Status
            WriteUInt32(packet, ref offset, 0);
            // Sender context
            WriteUInt64(packet, ref offset, ContextValues[_contextIndex++]);
            // Options
            WriteUInt32(packet, ref offset, 0);
            // Interface handle
            WriteUInt32(packet, ref offset, 0);
            // Timeout
            WriteUInt16(packet, ref offset, 0);
            // Item count
            WriteUInt16(packet, ref offset, 2);
            // Item 1 type (Connected Address)
            WriteUInt16(packet, ref offset, 0xA1);
            // Item 1 length
            WriteUInt16(packet, ref offset, 4);
            // Connection ID
            WriteUInt32(packet, ref offset, _otConnectionId);
            // Item 2 type (Connected Data)
            WriteUInt16(packet, ref offset, 0xB1);
            // Item 2 length
            WriteUInt16(packet, ref offset, (ushort)connDataLen);
            // Sequence
            WriteUInt16(packet, ref offset, _sequenceCounter++);
            if (_sequenceCounter == 0)
                _sequenceCounter = 1;

            // Copy IOI
            Buffer.BlockCopy(ioi, 0, packet, offset, ioi.Length);

            return packet;
        }

        private (bool success, string message) ForwardOpen()
        {
            var forwardOpenPacket = BuildForwardOpenPacket();
            _socket!.Send(forwardOpenPacket);

            byte[]? retData;
            try
            {
                retData = ReceiveData();
            }
            catch (SocketException ex)
            {
                return (false, ex.Message);
            }

            if (retData == null)
            {
                SocketConnected = false;
                return (false, "Forward open failed");
            }

            sbyte status = (sbyte)retData[42];
            if (status == 0)
            {
                _otConnectionId = BitConverter.ToUInt32(retData, 44);
                Connected = true;
            }
            else
            {
                SocketConnected = false;
                return (false, "Forward open failed");
            }

            SocketConnected = true;
            return (true, "Success");
        }

        private byte[] BuildForwardOpenPacket()
        {
            var forwardOpen = BuildCIPForwardOpen();
            var header = BuildRRDataHeader(forwardOpen.Length);
            return CombineArrays(header, forwardOpen);
        }

        private byte[] BuildCIPForwardOpen()
        {
            _serialNumber = (ushort)_random.Next(65000);
            var toConnectionId = (uint)_random.Next(65000);

            bool useLargeForwardOpen = ConnectionSize > 511;
            byte service = useLargeForwardOpen ? CIPServices.LargeForwardOpen : CIPServices.ForwardOpen;

            var packet = new List<byte>();

            // Service
            packet.Add(service);
            // Path size
            packet.Add(0x02);
            // Class type (8-bit)
            packet.Add(0x20);
            // Class - Connection Manager
            packet.Add(CIPClasses.ConnectionManager);
            // Instance type (8-bit)
            packet.Add(0x24);
            // Instance
            packet.Add(0x01);
            // Priority
            packet.Add(0x0A);
            // Timeout ticks
            packet.Add(0x0E);

            // O->T Connection ID
            AddUInt32(packet, 0x20000002);
            // T->O Connection ID
            AddUInt32(packet, toConnectionId);
            // Connection serial number
            AddUInt16(packet, _serialNumber);
            // Vendor ID
            AddUInt16(packet, VendorId);
            // Originator serial
            AddUInt32(packet, OriginatorSerial);
            // Multiplier
            AddUInt32(packet, 3);
            // O->T RPI
            AddUInt32(packet, 0x00201234);

            // Connection parameters
            if (useLargeForwardOpen)
            {
                // Large format: 32-bit parameters
                uint connParams = (0x4200u << 16) | (uint)ConnectionSize!.Value;
                AddUInt32(packet, connParams);
            }
            else
            {
                // Standard format: 16-bit parameters
                ushort connParams = (ushort)(0x4200 | ConnectionSize!.Value);
                AddUInt16(packet, connParams);
            }

            // T->O RPI
            AddUInt32(packet, 0x00204001);

            // T->O Connection parameters (same format as O->T)
            if (useLargeForwardOpen)
            {
                uint connParams = (0x4200u << 16) | (uint)ConnectionSize!.Value;
                AddUInt32(packet, connParams);
            }
            else
            {
                ushort connParams = (ushort)(0x4200 | ConnectionSize!.Value);
                AddUInt16(packet, connParams);
            }

            // Transport trigger
            packet.Add(0xA3);

            // Connection path
            var (pathSize, path) = BuildConnectedPath();
            packet.Add(pathSize);
            packet.AddRange(path);

            return packet.ToArray();
        }

        private byte[] BuildForwardClosePacket()
        {
            var forwardClose = BuildForwardClose();
            var header = BuildRRDataHeader(forwardClose.Length);
            return CombineArrays(header, forwardClose);
        }

        private byte[] BuildForwardClose()
        {
            var packet = new List<byte>();

            // Service - Forward Close
            packet.Add(CIPServices.ForwardClose);
            // Path size
            packet.Add(0x02);
            // Class type (8-bit)
            packet.Add(0x20);
            // Class - Connection Manager
            packet.Add(CIPClasses.ConnectionManager);
            // Instance type (8-bit)
            packet.Add(0x24);
            // Instance
            packet.Add(0x01);
            // Priority
            packet.Add(0x0A);
            // Timeout ticks
            packet.Add(0x0E);

            // Connection serial number
            AddUInt16(packet, _serialNumber);
            // Vendor ID
            AddUInt16(packet, VendorId);
            // Originator serial
            AddUInt32(packet, OriginatorSerial);

            // Connection path
            var (pathSize, path) = BuildConnectedPath();
            packet.Add(pathSize);
            packet.Add(0x00); // Reserved
            packet.AddRange(path);

            return packet.ToArray();
        }

        private (byte pathSize, byte[] path) BuildConnectedPath()
        {
            var path = new List<byte>();

            // Build route
            if (_parent.Route != null && _parent.Route.Length > 0)
            {
                foreach (var segment in _parent.Route)
                {
                    if (segment is ValueTuple<int, int> portSlot)
                    {
                        path.Add((byte)portSlot.Item1);
                        path.Add((byte)portSlot.Item2);
                    }
                    else if (segment is ValueTuple<int, string> portLink)
                    {
                        path.Add((byte)(portLink.Item1 + 0x10));
                        path.Add((byte)portLink.Item2.Length);
                        foreach (char c in portLink.Item2)
                            path.Add((byte)c);
                        // Byte align
                        if (path.Count % 2 != 0)
                            path.Add(0x00);
                    }
                }
            }
            else if (!_parent.Micro800)
            {
                // Default route: backplane, slot
                path.Add(PortIDs.Backplane);
                path.Add((byte)_parent.ProcessorSlot);
            }

            // Add Message Router path
            path.Add(0x20);
            path.Add(CIPClasses.MessageRouter);
            path.Add(0x24);
            path.Add(0x01);

            byte pathSize = (byte)(path.Count / 2);
            return (pathSize, path.ToArray());
        }

        private byte[] BuildUnconnectedPath(int slot)
        {
            var path = new List<byte>();

            // Build route
            if (_parent.Route != null && _parent.Route.Length > 0)
            {
                foreach (var segment in _parent.Route)
                {
                    if (segment is ValueTuple<int, int> portSlot)
                    {
                        path.Add((byte)portSlot.Item1);
                        path.Add((byte)portSlot.Item2);
                    }
                    else if (segment is ValueTuple<int, string> portLink)
                    {
                        path.Add((byte)(portLink.Item1 + 0x10));
                        path.Add((byte)portLink.Item2.Length);
                        foreach (char c in portLink.Item2)
                            path.Add((byte)c);
                        // Byte align
                        if (path.Count % 2 != 0)
                            path.Add(0x00);
                    }
                }
            }
            else
            {
                // Default route: backplane, slot
                path.Add(PortIDs.Backplane);
                path.Add((byte)slot);
            }

            byte pathSize = (byte)(path.Count / 2);

            var result = new byte[path.Count + 2];
            result[0] = pathSize;
            result[1] = 0x00; // Reserved
            path.CopyTo(result, 2);

            return result;
        }

        #endregion

        #region Helper Methods

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

        private static void WriteUInt64(byte[] buffer, ref int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset++] = (byte)((value >> (i * 8)) & 0xFF);
            }
        }

        private static void AddUInt16(List<byte> list, ushort value)
        {
            list.Add((byte)(value & 0xFF));
            list.Add((byte)((value >> 8) & 0xFF));
        }

        private static void AddUInt32(List<byte> list, uint value)
        {
            list.Add((byte)(value & 0xFF));
            list.Add((byte)((value >> 8) & 0xFF));
            list.Add((byte)((value >> 16) & 0xFF));
            list.Add((byte)((value >> 24) & 0xFF));
        }

        private static byte[] CombineArrays(params byte[][] arrays)
        {
            int totalLength = 0;
            foreach (var arr in arrays)
                totalLength += arr.Length;

            var result = new byte[totalLength];
            int offset = 0;
            foreach (var arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }
            return result;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        #endregion
    }
}
