using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DTCL.Transport
{
    internal interface ITransport
    {
        // Connects to a remote endpoint.
        void ConnectAsync(string address, int port);

        // Disconnects from the remote endpoint.
        void DisconnectAsync();

        // Sends data to the remote endpoint.
        Task SendAsync(byte[] data, int offset, int dataLength);

        // Checks if the transport is currently connected.
        bool IsConnected { get; }

        // Event that is triggered when data is received.
        event EventHandler<DataReceivedEventArgs> DataReceived;

        // Event that is triggered when an error occurs.
        event EventHandler<ErrorEventArgs> ErrorOccurred;

        void Dispose();
    }
}
