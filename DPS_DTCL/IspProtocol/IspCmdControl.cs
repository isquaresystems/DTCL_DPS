using DTCL.Log;
using System;
using static IspProtocol.IspCmdTransmitData;
using System.Windows;
using System.Threading.Tasks;

namespace IspProtocol
{
    public class IspCmdControl
    {
        UartIspTransport transport;
        byte[] txBuffer;
        int txSize;
        public IspCMDState currentState;
        readonly IspSubCommandProcessor processor;
        byte subCmd;

        public IspCmdControl(UartIspTransport transport, IspSubCommandProcessor processor)
        {
            this.transport = transport;
            this.processor = processor;
            currentState = IspCMDState.IDLE;
        }

        public bool Match(byte cmd)
        {
            return cmd == (byte)IspCommand.COMMAND_REQUEST ||
                   cmd == (byte)IspResponse.COMMAND_RESPONSE;
        }

        public async Task<byte[]> ExecuteCmd(byte[] data, int expectedRespLength, int timeOut = 1000)
        {
            if (data == null || data.Length == 0) return null;

            if (data[0] == (byte)IspCommand.COMMAND_REQUEST)
            {
                subCmd = data[1];
                Log.Info($"[EVT200] Executing command: subCmd=0x{subCmd:X2}, dataLen={data.Length}");

                transport.DisableEventDriven();
                SendCmd(data);

                await Task.Delay(25);

                var response = await transport.PollOnceAsync(expectedRespLength, timeOut, 10);

                transport.EnableEventDriven();

                var res = IspFramingUtils.TryDecodeFrame(response, out byte[] decodeFrame);

                if (res == true)
                {
                    if (decodeFrame[0] == (byte)IspResponse.COMMAND_RESPONSE)
                    {
                        var len = (decodeFrame[2] << 8) | decodeFrame[3];
                        var buffer = new byte[len];

                        try
                        {
                            Array.Copy(decodeFrame, 4, buffer, 0, len);
                            return buffer;
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[EVT106] Error copying received command response into buffer", ex);
                            currentState = IspCMDState.IDLE;
                            return null;
                        }
                    }
                }

                return null;
            }

            return null;
        }

        public void SendCmd(byte[] data)
        {
            // Reset();
            txBuffer = data;
            txSize = (data[2] << 8) | data[3];

            var frame = IspFramingUtils.EncodeFrame(data);
            currentState = IspCMDState.RECEIVING;

            Log.Debug($"[EVT2014] Sending Command frame. Size = {txSize}");
            _ = transport.TransmitAsync(frame);
        }

        public bool IsCmdCtrl() => currentState == IspCMDState.RECEIVING;
    }

    public enum IspCMDState : byte
    {
        IDLE = 0x00,
        RECEIVING = 0x01,
        TRANSMITTING = 0x02
    }
}