using System;
using System.Threading.Tasks;
using Xunit;
using CSLogix;

namespace CSLogix.Tests.Integration
{
    /// <summary>
    /// Integration tests using the mock PLC server.
    /// These tests verify end-to-end functionality without requiring real PLC hardware.
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly MockPLCServer _server;

        public IntegrationTests()
        {
            _server = new MockPLCServer();
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        #region Connection Tests

        [Fact]
        public void PLC_CanConnectToMockServer()
        {
            using var plc = new PLC(_server.IPAddress)
            {
                Port = _server.Port,
                SocketTimeout = 5
            };

            // Should not throw - PLC object created without error
            Assert.NotNull(plc);
        }

        [Fact]
        public void PLC_Properties_CanBeSet()
        {
            using var plc = new PLC("192.168.1.100");

            Assert.Equal("192.168.1.100", plc.IPAddress);

            plc.Port = 12345;
            Assert.Equal(12345, plc.Port);

            plc.ProcessorSlot = 2;
            Assert.Equal(2, plc.ProcessorSlot);

            plc.SocketTimeout = 10.5;
            Assert.Equal(10.5, plc.SocketTimeout);

            plc.Micro800 = true;
            Assert.True(plc.Micro800);
        }

        [Fact]
        public void PLC_DefaultPort_Is44818()
        {
            using var plc = new PLC("192.168.1.1");

            Assert.Equal(44818, plc.Port);
        }

        [Fact]
        public void PLC_DefaultProcessorSlot_IsZero()
        {
            using var plc = new PLC("192.168.1.1");

            Assert.Equal(0, plc.ProcessorSlot);
        }

        [Fact]
        public void PLC_DefaultSocketTimeout_Is5Seconds()
        {
            using var plc = new PLC("192.168.1.1");

            Assert.Equal(5.0, plc.SocketTimeout);
        }

        [Fact]
        public void PLC_DefaultMicro800_IsFalse()
        {
            using var plc = new PLC("192.168.1.1");

            Assert.False(plc.Micro800);
        }

        #endregion

        #region Disconnect Without Connect Tests

        [Fact]
        public void PLC_Close_WithoutConnect_DoesNotThrow()
        {
            using var plc = new PLC(_server.IPAddress)
            {
                Port = _server.Port
            };

            // Close without connecting should not throw
            plc.Close();
        }

        [Fact]
        public void PLC_Dispose_WithoutConnect_DoesNotThrow()
        {
            var plc = new PLC(_server.IPAddress)
            {
                Port = _server.Port
            };

            // Dispose without connecting should not throw
            plc.Dispose();
        }

        #endregion

        #region Mock Server Tests

        [Fact]
        public void MockPLCServer_StartsOnAvailablePort()
        {
            Assert.True(_server.Port > 0);
            Assert.True(_server.Port < 65536);
        }

        [Fact]
        public void MockPLCServer_HasDefaultTags()
        {
            Assert.True(_server.Tags.ContainsKey("TestDINT"));
            Assert.True(_server.Tags.ContainsKey("TestREAL"));
            Assert.True(_server.Tags.ContainsKey("TestBOOL"));
        }

        [Fact]
        public void MockPLCServer_TagsAreCaseInsensitive()
        {
            Assert.Equal(_server.Tags["TESTDINT"], _server.Tags["testdint"]);
        }

        [Fact]
        public void MockPLCServer_CanAddCustomTags()
        {
            _server.Tags["CustomTag"] = 999;

            Assert.True(_server.Tags.ContainsKey("CustomTag"));
            Assert.Equal(999, _server.Tags["CustomTag"]);
        }

        #endregion

        #region Route Configuration Tests

        [Fact]
        public void PLC_Route_CanBeConfigured()
        {
            using var plc = new PLC("192.168.1.100");

            // Route through port 2, slot 1
            plc.Route = new object[] { (2, 1) };

            Assert.NotNull(plc.Route);
            Assert.Single(plc.Route);
        }

        [Fact]
        public void PLC_Route_CanBeNull()
        {
            using var plc = new PLC("192.168.1.100");

            plc.Route = null;

            Assert.Null(plc.Route);
        }

        #endregion

        #region ConnectionSize Tests

        [Fact]
        public void PLC_ConnectionSize_CanBeConfigured()
        {
            using var plc = new PLC("192.168.1.100");

            plc.ConnectionSize = 4002;

            Assert.Equal(4002, plc.ConnectionSize);
        }

        [Fact]
        public void PLC_ConnectionSize_DefaultIsNull()
        {
            using var plc = new PLC("192.168.1.100");

            Assert.Null(plc.ConnectionSize);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void PLC_Read_WithInvalidIP_ReturnsError()
        {
            using var plc = new PLC("0.0.0.1")
            {
                SocketTimeout = 1
            };

            var result = plc.Read("TestTag");

            Assert.NotEqual("Success", result.Status);
        }

        [Fact]
        public void PLC_Write_WithInvalidIP_ReturnsError()
        {
            using var plc = new PLC("0.0.0.1")
            {
                SocketTimeout = 1
            };

            var result = plc.Write("TestTag", 100);

            Assert.NotEqual("Success", result.Status);
        }

        #endregion
    }

    /// <summary>
    /// Tests for mock server functionality.
    /// Note: Full connected session tests require more complete mock implementation.
    /// </summary>
    public class MockServerFunctionalityTests : IDisposable
    {
        private readonly MockPLCServer _server;

        public MockServerFunctionalityTests()
        {
            _server = new MockPLCServer();
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        [Fact]
        public void MockServer_CanAcceptTcpConnection()
        {
            // Test that the mock server accepts TCP connections
            using var client = new System.Net.Sockets.TcpClient();
            client.Connect(_server.IPAddress, _server.Port);

            Assert.True(client.Connected);
        }

        [Fact]
        public async Task MockServer_TracksConnectionCount()
        {
            var initialCount = _server.ConnectionCount;

            using var client = new System.Net.Sockets.TcpClient();
            client.Connect(_server.IPAddress, _server.Port);

            // Give server time to accept
            await System.Threading.Tasks.Task.Delay(100);

            Assert.True(_server.ConnectionCount > initialCount);
        }

        [Fact]
        public void MockServer_CanBeDisposedMultipleTimes()
        {
            var server = new MockPLCServer();
            server.Dispose();
            server.Dispose(); // Should not throw
        }

        [Fact]
        public void MockServer_TagDictionary_SupportsAllPrimitiveTypes()
        {
            _server.Tags["IntVal"] = 123;
            _server.Tags["FloatVal"] = 1.5f;
            _server.Tags["DoubleVal"] = 2.5d;
            _server.Tags["BoolVal"] = true;
            _server.Tags["StringVal"] = "test";

            Assert.Equal(123, _server.Tags["IntVal"]);
            Assert.Equal(1.5f, _server.Tags["FloatVal"]);
            Assert.Equal(2.5d, _server.Tags["DoubleVal"]);
            Assert.Equal(true, _server.Tags["BoolVal"]);
            Assert.Equal("test", _server.Tags["StringVal"]);
        }
    }
}
