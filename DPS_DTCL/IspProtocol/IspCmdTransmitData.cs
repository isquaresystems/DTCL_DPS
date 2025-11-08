using DTCL.Log;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IspProtocol
{
    public class IspCmdTransmitData : IIspCommandHandler
    {
        public enum TxState
        {
            Idle,
            WaitAck,
            PingResp
        }

        const int MaxRetries = 4;
        const int AckTimeoutMs = 3000; // 3 seconds timeout
        const int AckDoneTimeoutMs = 3000; // 3 seconds timeout for ACK_DONE
        const int MaxPacketDataSize = 56;

        byte[] txBuffer;
        int txSize;
        int sentSize;
        ushort currentSeq;
        ushort lastAckedSeq;
        byte subCommand;
        TxState currentState;
        int retryCount;

        // Thread safety and timeout handling
        readonly object stateLock = new object();
        Timer ackTimeoutTimer;
        Timer ackDoneTimeoutTimer;
        HashSet<ushort> ackedSequences;
        int duplicateAckCount;

        readonly UartIspTransport transport;
        readonly IspSubCommandProcessor processor;

        public bool TxCompleted { get; private set; }
        public IspSubCmdResponse SubCmdResponse { get; set; } = IspSubCmdResponse.NO_RESPONSE;

        static readonly Dictionary<byte, string> CommandMap = new Dictionary<byte, string>()
        {
            { (byte)IspCommand.TX_DATA_RESET, "TX_DATA_RESET" },
            { (byte)IspCommand.RX_DATA_RESET, "RX_DATA_RESET" },
            { (byte)IspCommand.TX_DATA, "TX_DATA" },
            { (byte)IspResponse.ACK, "ACK" },
            { (byte)IspResponse.ACK_DONE, "ACK_DONE" },
            { (byte)IspResponse.RX_MODE_ACK, "RX_MODE_ACK" },
            { (byte)IspResponse.RX_MODE_NACK, "RX_MODE_NACK" },
            { (byte)IspResponse.NACK, "NACK" }
        };

        public IspCmdTransmitData(UartIspTransport transport, IspSubCommandProcessor processor)
        {
            this.transport = transport;
            this.processor = processor;
            ackedSequences = new HashSet<ushort>();
            Reset();

            transport.TransmissionCompleted += OnTransmissionCompleted;
            Log.Debug("[TX-INIT] IspCmdTransmitData initialized");
        }

        public bool Match(byte command)
        {
            if (CommandMap.TryGetValue(command, out var name))
            {
                Log.Debug($"[TX-MATCH] Command 0x{command:X2} ({name}) matched");
                return true;
            }

            Log.Debug($"[TX-MATCH] Command 0x{command:X2} not handled by transmit handler");
            return false;
        }

        public void Execute(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            var cmd = data[0];
            var cmdName = CommandMap.TryGetValue(cmd, out var name) ? name : $"0x{cmd:X2}";

            // Stop ACK timeout timer on any received data
            StopAckTimeout();

            switch (cmd)
            {
                case (byte)IspCommand.TX_DATA_RESET:
                case (byte)IspCommand.RX_DATA_RESET:
                    Log.Info($"[TX-FLOW] {cmdName} received - Setting slave to reset mode");
                    SetSlaveResetMode(data);
                    break;

                case (byte)IspCommand.TX_DATA when data.Length >= 2:
                    Log.Info($"[TX-FLOW] {cmdName} received (SubCmd: 0x{data[1]:X2}) - Starting transmission setup");
                    SubCmdResponse = IspSubCmdResponse.IN_PROGRESS;
                    SetMode(data);
                    break;

                case (byte)IspResponse.TX_MODE_ACK:
                    Log.Debug($"[TX-FLOW] {cmdName} received - State: {currentState} -> Idle");
                    currentState = TxState.Idle;
                    break;

                case (byte)IspResponse.RX_MODE_ACK when data.Length >= 2:
                    Log.Info($"[TX-FLOW] {cmdName} received (SubCmd: 0x{data[1]:X2}) - Preparing data for transmission");
                    HandleRxModeAck(data);
                    break;

                case (byte)IspResponse.ACK when data.Length > 2:
                    var seq1 = (ushort)((data[1] << 8) | data[2]);
                    var code1 = (IspReturnCodes)(data[3]);
                    Log.Debug($"[TX-FLOW] ACK received - Seq: {seq1}, Code: {code1}");
                    HandleAck(seq1, code1);
                    break;

                case (byte)IspResponse.NACK when data.Length > 2:
                    var seq2 = (ushort)((data[1] << 8) | data[2]);
                    var code2 = (IspReturnCodes)(data[3]);
                    Log.Warning($"[TX-FLOW] NACK received - Seq: {seq2}, Code: {code2}");
                    HandleNack(seq2, code2);
                    break;

                case (byte)IspResponse.ACK_DONE:
                    var result = data[3];
                    var resultCode = (IspReturnCodes)result;
                    Log.Info($"[TX-FLOW] ACK_DONE received - Result: {resultCode}, Transmission COMPLETE");

                    // Stop ACK_DONE timeout as we received the response
                    StopAckDoneTimeout();

                    if (result == (byte)IspReturnCodes.SUBCMD_SUCESS)
                        SubCmdResponse = IspSubCmdResponse.SUCESS;
                    else
                        SubCmdResponse = IspSubCmdResponse.FAILED;
                    Reset();
                    break;

                default:
                    Log.Warning($"[TX-FLOW] Unexpected command {cmdName} received");
                    break;
            }
        }

        void HandleRxModeAck(byte[] data)
        {
            subCommand = data[1];
            var tempData = new byte[data.Length - 2];
            Buffer.BlockCopy(data, 2, tempData, 0, tempData.Length);

            Log.Info($"[TX-FLOW] Processing RX_MODE_ACK for SubCmd: 0x{subCommand:X2}");

            var result = processor.PrepareTxData(subCommand, tempData);

            if (result != null && result.Length > 0)
            {
                Log.Info($"[TX-DATA] Data prepared - Size: {result.Length} bytes, Starting transmission...");
                SetDataToSend(result);
                StartTransmission();
            }
            else
            {
                if (result != null && result.Length == 0)
                {
                    Log.Debug("[TX-DATA] OKB File");
                    SubCmdResponse = IspSubCmdResponse.SUCESS;
                }
                else
                {
                    Log.Error("[TX-DATA] Processor returned null/empty data - TRANSMISSION FAILED");
                    SubCmdResponse = IspSubCmdResponse.FAILED;
                }

                currentState = TxState.Idle;
                Reset();
            }
        }

        void HandleAck(ushort seq, IspReturnCodes code)
        {
            lock (stateLock)
            {
                // Check for duplicate ACK
                if (ackedSequences.Contains(seq))
                {
                    duplicateAckCount++;
                    Log.Warning($"[TX-ACK] Duplicate ACK for seq {seq} (count: {duplicateAckCount}) - Firmware may have missed seq {seq + 1}");

                    // Firmware might not have received the next packet, resend it
                    var nextSeq = (ushort)(seq + 1);
                    var nextPos = nextSeq * MaxPacketDataSize;

                    if (nextPos < txSize && duplicateAckCount <= 3)
                    {
                        Log.Info($"[TX-RETRY] Resending packet seq {nextSeq} due to duplicate ACK");
                        _ = SendSpecificPacketAsync(nextSeq);
                        StartAckTimeout();
                    }

                    return;
                }

                // Check if this is the expected ACK
                if (seq != currentSeq)
                {
                    Log.Warning($"[TX-ACK] Unexpected ACK seq {seq} (expected: {currentSeq})");

                    // If it's an old ACK, firmware might be behind
                    if (seq < currentSeq)
                    {
                        Log.Info($"[TX-RETRY] Old ACK received - Resending seq {seq + 1}");
                        var missedSeq = (ushort)(seq + 1);
                        _ = SendSpecificPacketAsync(missedSeq);
                    }

                    return;
                }

                // This is the expected ACK
                ackedSequences.Add(seq);
                duplicateAckCount = 0;
                lastAckedSeq = seq;

                // Calculate actual position and bytes sent for this sequence
                var seqStartPos = seq * MaxPacketDataSize;
                var bytesInThisPacket = Math.Min(MaxPacketDataSize, txSize - seqStartPos);
                sentSize = seqStartPos + bytesInThisPacket;

                Log.Info($"[TX-PROGRESS] ACK seq {seq} - Progress: {sentSize}/{txSize} bytes ({(sentSize * 100 / txSize)}%)");

                currentSeq++;
                retryCount = MaxRetries;

                if (sentSize < txSize)
                {
                    Log.Debug($"[TX-FLOW] Sending next packet seq {currentSeq}");
                    _ = SendNextPacketAsync(currentSeq);
                    StartAckTimeout();
                    SubCmdResponse = IspSubCmdResponse.IN_PROGRESS;
                }
                else
                {
                    Log.Info($"[TX-FLOW] All {sentSize} bytes transmitted - Waiting for ACK_DONE");
                    currentState = TxState.Idle;

                    // Start ACK_DONE timeout - if not received in 3 sec, consider as success
                    StartAckDoneTimeout();
                }
            }
        }

        void HandleNack(ushort seq, IspReturnCodes code)
        {
            lock (stateLock)
            {
                Log.Warning($"[TX-NACK] NACK for seq {seq}, code: {code}, retries left: {retryCount}");

                if (retryCount > 0 && code == IspReturnCodes.SUBCMD_SEQMATCH)
                {
                    retryCount--;
                    Log.Info($"[TX-RETRY] Resending seq {seq} (attempt {MaxRetries - retryCount + 1}/{MaxRetries})");
                    _ = SendSpecificPacketAsync(seq);
                    StartAckTimeout();
                }
                else
                {
                    Log.Error($"[TX-FAIL] Max retries exceeded or fatal error for seq {seq} - TRANSMISSION FAILED");
                    Reset();
                    SubCmdResponse = IspSubCmdResponse.FAILED;
                }
            }
        }

        public void SetMode(byte[] data)
        {
            Reset();

            data[0] = (byte)IspCommand.RX_DATA;
            txBuffer = data;
            txSize = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | (data[5]);

            var frame = IspFramingUtils.EncodeFrame(data);
            currentState = TxState.WaitAck;

            Log.Info($"[TX-SETUP] SetMode - Data size: {txSize} bytes, State: Idle -> WaitAck");
            _ = transport.TransmitAsync(frame);
        }

        public void SetSlaveResetMode(byte[] data)
        {
            Reset();

            txBuffer = data;
            txSize = 1;

            var frame = IspFramingUtils.EncodeFrame(data);
            currentState = TxState.Idle;
            _ = transport.TransmitAsync(frame);
            Reset();
            Log.Debug("[TX-SETUP] Slave reset mode completed");
        }

        public void SendPingRequest(byte[] data)
        {
            Reset();
            txBuffer = data;
            txSize = 1;
            var frame = IspFramingUtils.EncodeFrame(data);
            currentState = TxState.PingResp;
            _ = transport.TransmitAsync(frame);
            Log.Debug("[TX-PING] Ping request sent");
        }

        IspBoardId HandlePingResponse(byte[] data)
        {
            Log.Debug("[TX-PING] Ping response received");
            var cmd = new IspCommandManager();
            return cmd.GetMatchedBoardId(data[1]);
        }

        public void SetDataToSend(byte[] data)
        {
            lock (stateLock)
            {
                txBuffer = data;
                txSize = data.Length;
                sentSize = 0;
                currentSeq = 0;
                retryCount = MaxRetries;
                currentState = TxState.Idle;
                ackedSequences.Clear();
                duplicateAckCount = 0;

                Log.Info($"[TX-DATA] Data set for transmission - Size: {txSize} bytes");
            }
        }

        public void StartTransmission()
        {
            lock (stateLock)
            {
                if (txBuffer != null && txSize > 0)
                {
                    Log.Info($"[TX-START] Starting transmission - {txSize} bytes, first packet seq 0");
                    currentState = TxState.WaitAck;
                    _ = SendNextPacketAsync(currentSeq);
                    StartAckTimeout();
                }
            }
        }

        async Task SendNextPacketAsync(ushort seq) => await SendSpecificPacketAsync(seq);

        async Task SendSpecificPacketAsync(ushort seq)
        {
            try
            {
                if (txBuffer == null)
                {
                    Log.Warning($"[TX-SEND] Cannot send seq {seq} - txBuffer is null");
                    return;
                }

                // Calculate position based on sequence number
                var startPos = seq * MaxPacketDataSize;

                // Check bounds
                if (startPos >= txSize)
                {
                    Log.Debug($"[TX-SEND] No data to send for seq {seq} (pos {startPos} >= size {txSize})");
                    return;
                }

                var chunkLen = Math.Min(MaxPacketDataSize, txSize - startPos);
                var chunk = new byte[4 + chunkLen];
                chunk[0] = (byte)IspCommand.RX_DATA;
                chunk[1] = (byte)(seq >> 8);
                chunk[2] = (byte)(seq & 0xFF);
                chunk[3] = (byte)chunkLen;

                Array.Copy(txBuffer, startPos, chunk, 4, chunkLen);

                var frame = IspFramingUtils.EncodeFrame(chunk);
                Log.Debug($"[TX-SEND] Packet seq {seq} - {chunkLen} bytes at pos {startPos}/{txSize}");

                await transport.TransmitAsync(frame);
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-SEND] Error sending packet seq {seq}: {ex.Message}");
            }
        }

        void StartAckTimeout()
        {
            try
            {
                StopAckTimeout();

                Log.Debug($"[TX-TIMEOUT] Starting ACK timeout for seq {currentSeq} ({AckTimeoutMs}ms)");

                ackTimeoutTimer = new Timer(
                    HandleAckTimeout,
                    null,
                    AckTimeoutMs,
                    Timeout.Infinite
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Failed to start ACK timeout timer: {ex.Message}");
            }
        }

        void StopAckTimeout()
        {
            try
            {
                if (ackTimeoutTimer != null)
                {
                    Log.Debug("[TX-TIMEOUT] Stopping ACK timeout timer");
                    ackTimeoutTimer.Dispose();
                    ackTimeoutTimer = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Error stopping ACK timeout timer: {ex.Message}");
            }
        }

        void StartAckDoneTimeout()
        {
            try
            {
                StopAckDoneTimeout();

                Log.Debug($"[TX-TIMEOUT] Starting ACK_DONE timeout ({AckDoneTimeoutMs}ms)");

                ackDoneTimeoutTimer = new Timer(
                    HandleAckDoneTimeout,
                    null,
                    AckDoneTimeoutMs,
                    Timeout.Infinite
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Failed to start ACK_DONE timeout timer: {ex.Message}");
            }
        }

        void StopAckDoneTimeout()
        {
            try
            {
                if (ackDoneTimeoutTimer != null)
                {
                    Log.Debug("[TX-TIMEOUT] Stopping ACK_DONE timeout timer");
                    ackDoneTimeoutTimer.Dispose();
                    ackDoneTimeoutTimer = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Error stopping ACK_DONE timeout timer: {ex.Message}");
            }
        }

        void HandleAckDoneTimeout(object state)
        {
            try
            {
                lock (stateLock)
                {
                    Log.Warning("[TX-TIMEOUT] ACK_DONE timeout - Considering transmission as SUCCESS and resetting");

                    // If ACK_DONE not received in 3 seconds, consider it as success
                    SubCmdResponse = IspSubCmdResponse.SUCESS;
                    Reset();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Exception in HandleAckDoneTimeout: {ex.Message}");
                Reset();
            }
        }

        void HandleAckTimeout(object state)
        {
            try
            {
                lock (stateLock)
                {
                    if (currentState != TxState.WaitAck)
                    {
                        Log.Debug("[TX-TIMEOUT] Timeout occurred but not in WaitAck state");
                        return;
                    }

                    retryCount--;

                    if (retryCount > 0)
                    {
                        Log.Warning($"[TX-TIMEOUT] ACK timeout for seq {currentSeq} - Retrying ({retryCount} attempts left)");
                        _ = SendSpecificPacketAsync(currentSeq);
                        StartAckTimeout();
                    }
                    else
                    {
                        Log.Error($"[TX-TIMEOUT] Max retries exceeded for seq {currentSeq} - TRANSMISSION FAILED");
                        Reset();
                        SubCmdResponse = IspSubCmdResponse.FAILED;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TX-TIMEOUT] Exception in HandleAckTimeout: {ex.Message}");
                Reset();
            }
        }

        public void Reset()
        {
            StopAckTimeout();
            StopAckDoneTimeout();

            lock (stateLock)
            {
                txBuffer = null;
                txSize = 0;
                sentSize = 0;
                currentSeq = 0;
                lastAckedSeq = 0;
                subCommand = 0;
                retryCount = MaxRetries;
                currentState = TxState.Idle;
                TxCompleted = false;
                ackedSequences.Clear();
                duplicateAckCount = 0;
            }

            Log.Debug("[TX-RESET] Transmission state reset to Idle");
        }

        public int GetTransmittedByteCount() => sentSize;

        public int GetTotalByteCount() => txSize;

        public IspSubCmdResponse GetTxSubCmdResponse() => SubCmdResponse;

        public bool IsTransmitting()
        {
            lock (stateLock)
                return currentState == TxState.WaitAck;
        }

        void OnTransmissionCompleted(bool success)
        {
            TxCompleted = success;
            Log.Debug($"[TX-COMPLETE] Physical transmission completed - Success: {success}");
        }
    }
}