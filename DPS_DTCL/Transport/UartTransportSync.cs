using System;
using System.IO.Ports;
using System.Threading;

namespace DTCL.Transport
{
    public class UartTransportSync : IDisposable
    {
        SerialPort _serialPort;
        int _expectedBytes = 512;
        byte[] _receivedBuffer;
        bool _isPortOpen;
        Thread _portMonitorThread;
        bool _isMonitoring;

        public event EventHandler<PortEventArgs> PortClosed;
        public event EventHandler<PortEventArgs> PortOpened;

        AutoResetEvent _dataReadyEvent = new AutoResetEvent(false);

        public UartTransportSync(string portName, int baudRate, int expectedBytes = 512)
        {
            _serialPort = new SerialPort(portName, baudRate);
            _expectedBytes = expectedBytes;
            _serialPort.DataReceived += OnSerialPortDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;
            _receivedBuffer = new byte[0];
            _serialPort.ReadBufferSize = 16384;
        }

        public bool Connect()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                    _isPortOpen = true;
                    Log.Log.Info("port opened");
                    OnPortOpened(new PortEventArgs(_serialPort.PortName));

                    _isMonitoring = true;
                    _portMonitorThread = new Thread(MonitorPortStatus);
                    _portMonitorThread.Start();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening serial port: {ex.Message}");
                return false;
            }

            return false;
        }

        public void Disconnect()
        {
            if (_serialPort.IsOpen)
            {
                _isMonitoring = false;  // Stop the monitoring thread
                _serialPort.Close();
                _isPortOpen = false;
                OnPortClosed(new PortEventArgs(_serialPort.PortName));  // Trigger PortClosed event
            }
        }

        // Flush the receive and transmit buffers
        public void FlushBuffer()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.DiscardInBuffer();  // Clear receive buffer
                _serialPort.DiscardOutBuffer();  // Clear transmit buffer

                lock (_receivedBuffer)
                    _receivedBuffer = new byte[0];
                // Clear local buffer
            }
        }

        // Background thread method to monitor the port status
        void MonitorPortStatus()
        {
            while (_isMonitoring)
            {
                try
                {
                    if (!_serialPort.IsOpen && _isPortOpen)
                    {
                        _isPortOpen = false;
                        OnPortClosed(new PortEventArgs(_serialPort.PortName));
                    }
                }
                catch (Exception ex)
                {
                    // Catch exceptions during the port monitoring process
                    Log.Log.Error($"Error monitoring port: {ex.Message}");
                }

                Thread.Sleep(1000);  // Sleep for 1 second before checking again
            }
        }

        // Handle errors from the serial port (disconnection, etc.)
        void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // If an error is received and the port is no longer open, trigger PortClosed
            if (!_serialPort.IsOpen && _isPortOpen)
            {
                _isPortOpen = false;
                OnPortClosed(new PortEventArgs(_serialPort.PortName));  // Trigger PortClosed event
            }
        }

        // Fire the PortClosed event
        protected virtual void OnPortClosed(PortEventArgs e)
        {
            _isMonitoring = false;
            PortClosed?.Invoke(this, e);
        }

        // Fire the PortOpened event
        protected virtual void OnPortOpened(PortEventArgs e) => PortOpened?.Invoke(this, e);

        public void Send(byte[] data, int offset, int dataLength)
        {
            try
            {
                if (_isPortOpen)
                {
                    ReadExistingData();
                    FlushBuffer();
                    _serialPort.Write(data, offset, dataLength);
                }
                else
                {
                    // throw new InvalidOperationException("Serial port is not open.");
                }
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error Send: {ex.Message}");
            }
        }

        public byte[] ReadExistingData()
        {
            if (_serialPort.IsOpen && _serialPort.BytesToRead > 0)
            {
                _serialPort.ReadExisting();
            }

            _receivedBuffer = new byte[0];
            return new byte[0];
        }

        void OnSerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var buffer = new byte[_serialPort.BytesToRead];
                _serialPort.Read(buffer, 0, buffer.Length);

                lock (_receivedBuffer)
                {
                    _receivedBuffer = Combine(_receivedBuffer, buffer);
                    _dataReadyEvent.Set();
                }
            }
            catch (Exception ex)
            {
                Log.Log.Error($"OnSerialPortDataReceived exception : {ex.Message}");
            }
        }

        byte[] Combine(byte[] first, byte[] second)
        {
            var combined = new byte[first.Length + second.Length];
            Array.Copy(first, combined, first.Length);
            Array.Copy(second, 0, combined, first.Length, second.Length);
            return combined;
        }

        public byte[] WaitForResponse(int expectedBytes, int timeoutMilliseconds)
        {
            if (expectedBytes == 0)
            {
                expectedBytes = _serialPort.BytesToRead;
            }

            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMilliseconds)
            {
                lock (_receivedBuffer)
                {
                    if (_receivedBuffer.Length >= expectedBytes)
                    {
                        var response = new byte[expectedBytes];
                        Array.Copy(_receivedBuffer, response, expectedBytes);

                        // Remove the processed data from the buffer
                        var remaining = new byte[_receivedBuffer.Length - expectedBytes];
                        Array.Copy(_receivedBuffer, expectedBytes, remaining, 0, remaining.Length);
                        _receivedBuffer = remaining;

                        return response;
                    }
                }

                // await Task.Delay(10);
                Thread.Sleep(10);  // Small delay to prevent tight looping
            }

            return null;  // Return null if timeout occurs
        }

        public void Dispose()
        {
            _isMonitoring = false;  // Stop the background monitoring thread

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort.Dispose();
            }
        }

        // Event arguments for port events
        public class PortEventArgs : EventArgs
        {
            public string PortName { get; }

            public PortEventArgs(string portName) => PortName = portName;
        }
    }
}