using DTCL.Log;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using static DTCL.Transport.UartTransportSync;

namespace IspProtocol
{
    public class UartIspTransport : IDisposable
    {
        readonly SerialPort serialPort;
        readonly object lockObj = new object();
        bool disposed;

        public event Action<byte[]> DataReceived;
        public event Action PortOpened;
        public event Action PortClosed;
        public bool isPortOpen => serialPort.IsOpen;
        public event Action<bool> TransmissionCompleted;
        bool eventDrivenEnabled = true;

        Thread _portMonitorThread;
        bool _isMonitoring;

        public UartIspTransport(string portName, int baudRate = 115200)
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            serialPort.DataReceived += OnDataReceived;
            serialPort.ReadTimeout = 1000;
            serialPort.WriteTimeout = 1000;
        }

        public void Open()
        {
            if (!serialPort.IsOpen)
            {
                serialPort.Open();
                PortOpened?.Invoke();

                _isMonitoring = true;
                _portMonitorThread = new Thread(MonitorPortStatus);
                _portMonitorThread.Start();
            }
        }

        public void Close()
        {
            try
            {
                // Stop monitoring when closing
                _isMonitoring = false;

                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                    PortClosed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"UART close error: {ex.Message}");
            }
        }

        public async Task TransmitAsync(byte[] data)
        {
            if (!serialPort.IsOpen)
            {
                TransmissionCompleted?.Invoke(false);
                Log.Error("UART transmit error: Port is not open.");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    lock (lockObj)
                        serialPort.Write(data, 0, data.Length);

                    TransmissionCompleted?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Log.Error($"UART transmit error: {ex.Message}");
                    TransmissionCompleted?.Invoke(false);
                }
            });
        }

        void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var bytes = serialPort.BytesToRead;

                if (bytes > 0)
                {
                    var buffer = new byte[bytes];
                    serialPort.Read(buffer, 0, bytes);
                    DataReceived?.Invoke(buffer);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"UART receive error: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls the serial port until at least <paramref name="expectedBytes"/> are available
        /// or the timeout elapses. Returns exactly <paramref name="expectedBytes"/> bytes (or
        /// an empty array on timeout).
        /// </summary>
        /// <param name="expectedBytes">How many bytes you need before returning.</param>
        /// <param name="timeoutMs">Max time in milliseconds to wait for data.</param>
        /// <param name="pollIntervalMs">Delay between polls in milliseconds.</param>
        public async Task<byte[]> PollOnceAsync(
            int expectedBytes,
            int timeoutMs = 1000,
            int pollIntervalMs = 50)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (serialPort.IsOpen)
                {
                    var available = serialPort.BytesToRead;

                    if (available >= expectedBytes)
                    {
                        var buffer = new byte[expectedBytes];
                        serialPort.Read(buffer, 0, expectedBytes);
                        return buffer;
                    }
                }

                await Task.Delay(pollIntervalMs).ConfigureAwait(false);
            }

            // timeout: not enough data arrived
            return Array.Empty<byte>();
        }

        /// <summary>Turn on event-driven (DataReceived) handling.</summary>
        public void EnableEventDriven()
        {
            if (!eventDrivenEnabled)
            {
                serialPort.DataReceived += OnDataReceived;
                eventDrivenEnabled = true;
            }
        }

        /// <summary>Turn off event-driven handling so PollOnceAsync can run cleanly.</summary>
        public void DisableEventDriven()
        {
            if (eventDrivenEnabled)
            {
                serialPort.DataReceived -= OnDataReceived;
                eventDrivenEnabled = false;
            }
        }

        void MonitorPortStatus()
        {
            while (_isMonitoring)
            {
                try
                {
                    if (!serialPort.IsOpen)
                    {
                        PortClosed?.Invoke();
                        // Don't call Join() from within the thread itself - this causes deadlock
                        // Just break out of the loop and let the thread terminate
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Catch exceptions during the port monitoring process
                    Log.Error($"Error monitoring port: {ex.Message}");
                }

                // Check if we should still be monitoring before sleeping
                if (_isMonitoring)
                {
                    Thread.Sleep(1000);  // Sleep for 1 second before checking again
                }
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    // Stop monitoring thread first
                    _isMonitoring = false;

                    // Wait for monitoring thread to complete (with timeout)
                    if (_portMonitorThread != null && _portMonitorThread.IsAlive)
                    {
                        if (!_portMonitorThread.Join(2000)) // 2 second timeout
                        {
                            Log.Warning("Port monitor thread did not terminate gracefully, aborting");
                            _portMonitorThread.Abort();
                        }

                        _portMonitorThread = null;
                    }

                    // Cleanup serial port
                    serialPort.DataReceived -= OnDataReceived;

                    if (serialPort.IsOpen)
                    {
                        serialPort.Close();
                        PortClosed?.Invoke();
                    }

                    serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error($"UART dispose error: {ex.Message}");
                }

                disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}