using DTCL.Log;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

namespace IspProtocol
{
    public class IspSubCommandProcessor
    {
        readonly Dictionary<byte, IIspSubCommandHandler> handlers = new Dictionary<byte, IIspSubCommandHandler>();

        /// <summary>
        /// Registers a handler for a given subcommand.
        /// </summary>
        public void Register(byte subCmd, IIspSubCommandHandler handler)
        {
            // Check if already registered with same handler
            if (handlers.TryGetValue(subCmd, out var existingHandler) && existingHandler == handler)
            {
                return; // Already registered, do nothing
            }
            
            handlers[subCmd] = handler;
            Log.Info($"[EVT3001] Registered handler for subcommand 0x{subCmd:X2}.");
        }

        /// <summary>
        /// Invokes the receive data processor for a given subcommand.
        /// </summary>
        public uint ProcessRxSubCommand(byte subCmd, byte[] data)
        {
            if (handlers.TryGetValue(subCmd, out var handler))
            {
                Log.Info($"[EVT3002] Processing RX data for subcommand 0x{subCmd:X2}, Size={data?.Length ?? 0} bytes.");
                var res = handler.processRxData(data, subCmd);
                return res;
            }
            else
            {
                Log.Warning($"[EVT3003] No RX handler registered for subcommand 0x{subCmd:X2}.");
                return 1;
            }
        }

        /// <summary>
        /// Prepares the processor for receiving expected RX data.
        /// </summary>
        public long prepareForRx(byte subCmd, byte[] data)
        {
            if (handlers.TryGetValue(subCmd, out var handler))
            {
                Log.Info($"[EVT3004] prepareForRx called for subcommand 0x{subCmd:X2}, ExpectedSize={data?.Length ?? 0}.");
                return handler.prepareForRx(data, subCmd, data.Length);
            }
            else
            {
                Log.Warning($"[EVT3005] No prepareForRx handler registered for subcommand 0x{subCmd:X2}.");
                return 1024; // fallback default
            }
        }

        /// <summary>
        /// Retrieves TX data by invoking the appropriate subcommand handler.
        /// </summary>
        public byte[] PrepareTxData(byte subCmd, byte[] data)
        {
            if (handlers.TryGetValue(subCmd, out var handler))
            {
                Log.Info($"[EVT3006] Preparing TX data for subcommand 0x{subCmd:X2}, InputSize={data?.Length ?? 0}.");
                return handler.prepareDataToTx(data, subCmd);
            }
            else
            {
                Log.Warning($"[EVT3007] No TX handler registered for subcommand 0x{subCmd:X2}. Returning empty.");
                return Array.Empty<byte>();
            }
        }
    }
}
