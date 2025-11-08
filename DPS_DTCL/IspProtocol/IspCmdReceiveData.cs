using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using DTCL.Log;
using static IspProtocol.IspCmdTransmitData;

namespace IspProtocol
{
    public class IspCmdReceiveData : IIspCommandHandler
    {
        private enum RxState
        {
            Idle,
            Receiving
        }

        byte[] buffer;
        long totalSize;
        int receivedSize;
        ushort expectedSeq;
        byte subCommand;
        RxState currentState;
        int nackRetryCount;
        const int MaxNackRetries = 3;
        int MaxChunkSize = 1023;
        readonly UartIspTransport transport;
        readonly IspSubCommandProcessor processor;

        // Multi-chunk transfer state
        long remainingData;
        long totalDataReceived; // Tracks data received across all chunks
        byte[] Data;

        // Duplicate ACK handling
        HashSet<ushort> ackedSequences;
        ushort lastSuccessfulSeq;
        byte[] lastDataPacket; // Store last data packet for retransmission
        const int MaxDuplicateAcks = 3;
        Dictionary<ushort, int> duplicateAckCount;

        // ACK retry mechanism for timeout
        System.Threading.Timer ackRetryTimer;
        readonly object timerLock = new object();
        const int AckRetryTimeoutMs = 3000; // 3 seconds timeout
        const int MaxAckRetries = 3;
        int ackRetryCount;
        ushort lastAckedSeq;

        public IspSubCmdResponse SubCmdResponse { get; set; } = IspSubCmdResponse.NO_RESPONSE;

        public IspCmdReceiveData(UartIspTransport transport, IspSubCommandProcessor processor)
        {
            this.transport = transport;
            this.processor = processor;
            ackedSequences = new HashSet<ushort>();
            duplicateAckCount = new Dictionary<ushort, int>();
            Reset();
            Log.Debug("[RX-INIT] IspCmdReceiveData initialized");
        }

        public bool Match(byte cmd)
        {
            var isMatch = cmd == (byte)IspCommand.RX_DATA || cmd == (byte)IspResponse.TX_MODE_ACK || cmd == (byte)IspResponse.TX_MODE_NACK;
            Log.Debug($"[RX-MATCH] Command 0x{cmd:X2} {(isMatch ? "matched" : "not handled by receive handler")}");
            return isMatch;
        }

        public void Execute(byte[] data)
        {
            try
            {
                if (data == null || data.Length == 0)
                {
                    Log.Warning("[RX-FLOW] Execute called with null/empty data");
                    return;
                }

                var cmd = data[0];

                // Start of frame or new chunk request
                if (currentState == RxState.Idle && data.Length >= 8)
                {
                    Log.Info($"[RX-FLOW] New transfer starting - Command: 0x{cmd:X2}, Data length: {data.Length}");
                    HandleStartOfTransfer(data);
                    return;
                }

                // In the middle of receiving
                if (currentState == RxState.Receiving)
                {
                    Log.Debug($"[RX-FLOW] Data chunk received - Command: 0x{cmd:X2}, Expected seq: {expectedSeq}, State: Receiving");
                    HandleDataChunk(data);
                    return;
                }

                // Unexpected state
                Log.Warning($"[RX-FLOW] Execute in unexpected state: {currentState}, Command: 0x{cmd:X2}, Data length: {data?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-ERROR] Exception in Execute: {ex.Message}");
                Reset();
                SubCmdResponse = IspSubCmdResponse.FAILED;
            }
        }

        void HandleStartOfTransfer(byte[] data)
        {
            Data = data;
            subCommand = data[1];
            totalSize = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | (data[5]);

            Log.Info($"[RX-START] Transfer initiated - SubCmd: 0x{subCommand:X2}, Total size: {totalSize} bytes");

            // Initialize or continue transfer based on offset

            // First chunk - initialize everything
            buffer = new byte[totalSize];
            totalDataReceived = 0;
            remainingData = totalSize;

            // Clear tracking structures for new chunk
            ackedSequences.Clear();
            duplicateAckCount.Clear();
            lastSuccessfulSeq = 0;
            lastDataPacket = null;

            // Calculate chunk size for this transfer
            var currentChunkSize = Math.Min(remainingData, MaxChunkSize);

            expectedSeq = 0;
            receivedSize = 0; // Reset for current chunk
            nackRetryCount = 0;
            currentState = RxState.Receiving;

            Log.Info($"[RX-CHUNK] Starting chunk 1 - Size: {currentChunkSize} bytes, State: Idle -> Receiving");

            // Prepare request to start data transmission form processor side
            var request = new byte[Data.Length];
            request[0] = (byte)IspCommand.TX_DATA;
            request[1] = subCommand;
            request[2] = (byte)(currentChunkSize >> 24);
            request[3] = (byte)(currentChunkSize >> 16);
            request[4] = (byte)(currentChunkSize >> 8);
            request[5] = (byte)(currentChunkSize & 0xFF);
            var i = 0;

            for (i = 6; i < Data.Length; ++i)
                request[i] = Data[i];

            var startFrame = IspFramingUtils.EncodeFrame(request);
            Log.Debug($"[RX-REQUEST] Sending TX_DATA request for chunk");
            _ = transport.TransmitAsync(startFrame);
            SubCmdResponse = IspSubCmdResponse.IN_PROGRESS;
        }

        void HandleDataChunk(byte[] data)
        {
            // Cancel any pending ACK retry timer since we received data
            Log.Debug("[RX-TIMER] Data received, stopping ACK retry timer");
            StopAckRetryTimer();

            if (data[0] == (byte)IspResponse.TX_MODE_NACK)
            {
                Log.Error($"[RX-FLOW] TX_MODE_NACK received - SubCmd: 0x{data[1]:X2}, Error: 0x{data[2]:X2} - TRANSFER FAILED");
                Reset();
                SubCmdResponse = IspSubCmdResponse.FAILED;
                return;
            }

            if (data.Length < 4)
            {
                Log.Warning($"[RX-DATA] Received undersized chunk ({data.Length} bytes) - Ignored");
                return;
            }

            var seq = (ushort)((data[1] << 8) | data[2]);
            int chunkSize = data[3];

            // Validate chunk size
            if (data.Length < 4 + chunkSize)
            {
                Log.Warning($"[RX-DATA] Chunk size mismatch - Expected: {chunkSize}, Actual: {data.Length - 4} - Ignored");
                return;
            }

            // Check if this is a duplicate packet (already ACKed)
            if (ackedSequences.Contains(seq))
            {
                // Track duplicate count
                if (!duplicateAckCount.ContainsKey(seq))
                    duplicateAckCount[seq] = 0;

                duplicateAckCount[seq]++;

                Log.Warning($"[RX-DUP] Duplicate data for ACKed seq {seq} (count: {duplicateAckCount[seq]}) - Re-sending ACK");

                // Re-send the ACK since the sender might not have received it
                SendAck(seq, IspReturnCodes.SUBCMD_SEQMATCH);

                // If we get too many duplicates of the same sequence, might indicate a problem
                if (duplicateAckCount[seq] >= MaxDuplicateAcks)
                {
                    Log.Error($"[RX-DUP] Too many duplicate packets for seq {seq} - Communication issue detected");
                }

                return; // Don't process the data again
            }

            // Sequence out of order?
            if (seq != expectedSeq)
            {
                // Check if this is a retransmission of an old sequence we already processed
                if (seq < expectedSeq && ackedSequences.Contains(seq))
                {
                    Log.Info($"[RX-SEQ] Old seq {seq} received (expected: {expectedSeq}) - Re-sending ACK");
                    SendAck(seq, IspReturnCodes.SUBCMD_SEQMATCH);
                    return;
                }

                // This is genuinely out of order
                if (nackRetryCount < MaxNackRetries)
                {
                    nackRetryCount++;
                    Log.Warning($"[RX-SEQ] Sequence mismatch - Expected: {expectedSeq}, Got: {seq} - Sending NACK ({nackRetryCount}/{MaxNackRetries})");
                    SendNack((ushort)expectedSeq, IspReturnCodes.SUBCMD_SEQMATCH);
                }
                else
                {
                    Log.Error($"[RX-FAIL] Max NACK retries exceeded for expected seq {expectedSeq} - TRANSFER FAILED");
                    Reset();
                    SubCmdResponse = IspSubCmdResponse.FAILED;
                }

                return;
            }

            // Good seq, reset retry counter
            nackRetryCount = 0;

            // Store this packet in case we need to retransmit
            lastDataPacket = new byte[data.Length];
            Array.Copy(data, lastDataPacket, data.Length);

            // Calculate buffer position for this chunk
            var bufferPosition = totalDataReceived + receivedSize;

            // Validate buffer bounds
            if (bufferPosition + chunkSize > totalSize)
            {
                Log.Error($"[RX-OVERFLOW] Buffer overflow - Position: {bufferPosition}, Chunk: {chunkSize}, Total: {totalSize} - TRANSFER FAILED");
                Reset();
                SubCmdResponse = IspSubCmdResponse.FAILED;
                return;
            }

            // Copy data into buffer
            try
            {
                Array.Copy(data, 4, buffer, bufferPosition, chunkSize);
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-COPY] Error copying chunk to buffer: {ex.Message} - TRANSFER FAILED");
                Reset();
                SubCmdResponse = IspSubCmdResponse.FAILED;
                return;
            }

            receivedSize += chunkSize;
            lastSuccessfulSeq = seq;
            ackedSequences.Add(seq); // Mark this sequence as processed
            expectedSeq++;

            // Calculate how much data we expect for current chunk
            var currentChunkExpectedSize = Math.Min(remainingData, MaxChunkSize);
            var totalProgress = totalDataReceived + receivedSize;
            var progressPercent = (int)((totalProgress * 100) / totalSize);

            Log.Debug($"[RX-PROGRESS] Packet seq {seq} - Chunk: {receivedSize}/{currentChunkExpectedSize} bytes, Total: {totalProgress}/{totalSize} bytes ({progressPercent}%)");

            // Always start retry timer after sending ACK, regardless of position in chunk
            // The timer will be cancelled when the next packet arrives or chunk completes
            SendAckWithRetryTimer(seq, IspReturnCodes.SUBCMD_SEQMATCH);

            // Check if current chunk is complete
            if (receivedSize >= currentChunkExpectedSize)
            {
                // Stop retry timer since chunk is complete
                Log.Debug("[RX-TIMER] Chunk complete, stopping ACK retry timer");
                StopAckRetryTimer();

                totalDataReceived += receivedSize;
                remainingData -= receivedSize;

                Log.Info($"[RX-CHUNK] Chunk complete - {receivedSize} bytes received, Total progress: {totalDataReceived}/{totalSize} bytes");

                // Always send ACK_DONE for completed chunk
                Log.Debug($"[RX-ACK] Sending ACK_DONE for completed chunk (last seq: {expectedSeq - 1})");
                SendAckDone((ushort)(expectedSeq - 1), IspReturnCodes.SUBCMD_SUCESS);

                // Check if entire transfer is complete
                if (totalDataReceived >= totalSize)
                {
                    Log.Info($"[RX-COMPLETE] Full transfer complete - {totalDataReceived} bytes received, Processing data...");
                    ProcessCompleteData();
                }
                else
                {
                    // More chunks needed - request next chunk
                    Log.Info($"[RX-CHUNK] Requesting next chunk - Remaining: {remainingData} bytes");
                    RequestNextChunk();
                }
            }
        }

        void RequestNextChunk()
        {
            // Clear tracking for new chunk
            ackedSequences.Clear();
            duplicateAckCount.Clear();
            lastDataPacket = null;

            var nextChunkSize = Math.Min(remainingData, MaxChunkSize);

            var request = new byte[Data.Length];
            request[0] = (byte)IspCommand.TX_DATA;
            request[1] = subCommand;
            request[2] = (byte)(nextChunkSize >> 24);
            request[3] = (byte)(nextChunkSize >> 16);
            request[4] = (byte)(nextChunkSize >> 8);
            request[5] = (byte)(nextChunkSize & 0xFF);
            var i = 0;

            for (i = 6; i < Data.Length; ++i)
                request[i] = Data[i];

            var frame = IspFramingUtils.EncodeFrame(request);
            Log.Info($"[RX-REQUEST] Requesting next chunk - Size: {nextChunkSize} bytes");

            // Reset expectedSeq to 0 for new chunk - processor starts each chunk with seq 0
            expectedSeq = 0;
            receivedSize = 0; // Reset for current chunk
            nackRetryCount = 0;
            currentState = RxState.Receiving;

            _ = transport.TransmitAsync(frame);
        }

        void ProcessCompleteData()
        {
            try
            {
                Log.Info($"[RX-PROCESS] Processing complete data - SubCmd: 0x{subCommand:X2}, Size: {totalDataReceived} bytes");
                var res = processor.ProcessRxSubCommand(subCommand, buffer);

                if (res == 0)
                {
                    SubCmdResponse = IspSubCmdResponse.SUCESS;
                    Log.Info($"[RX-SUCCESS] Data processing completed successfully - SubCmd: 0x{subCommand:X2}");
                }
                else
                {
                    SubCmdResponse = IspSubCmdResponse.FAILED;
                    Log.Error($"[RX-FAIL] Data processing failed - SubCmd: 0x{subCommand:X2}, Result: {res}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-EXCEPTION] Processor exception for SubCmd 0x{subCommand:X2}: {ex.Message}");
                SubCmdResponse = IspSubCmdResponse.FAILED;
            }
            finally
            {
                Reset();
            }
        }

        void SendAckWithRetryTimer(ushort seq, IspReturnCodes code)
        {
            try
            {
                // Send the ACK
                SendAck(seq, code);

                // Store the sequence for potential retry
                lastAckedSeq = seq;
                ackRetryCount = 0;

                Log.Debug($"[RX-TIMER] Starting ACK retry timer for seq {seq} (expecting next: {expectedSeq})");

                // Start retry timer for next data packet
                StartAckRetryTimer();
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-TIMER] Error in SendAckWithRetryTimer for seq {seq}: {ex.Message}");
            }
        }

        void StartAckRetryTimer()
        {
            try
            {
                lock (timerLock)
                {
                    StopAckRetryTimer();

                    Log.Debug($"[RX-TIMER] Starting ACK retry timer ({AckRetryTimeoutMs}ms timeout)");

                    ackRetryTimer = new System.Threading.Timer(
                        HandleAckRetryTimeout,
                        null,
                        AckRetryTimeoutMs,
                        System.Threading.Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-TIMER] Failed to start ACK retry timer: {ex.Message}");
            }
        }

        void StopAckRetryTimer()
        {
            try
            {
                lock (timerLock)
                {
                    if (ackRetryTimer != null)
                    {
                        Log.Debug("[RX-TIMER] Stopping ACK retry timer");
                        ackRetryTimer.Dispose();
                        ackRetryTimer = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-TIMER] Error stopping ACK retry timer: {ex.Message}");
            }
        }

        void HandleAckRetryTimeout(object state)
        {
            try
            {
                Log.Warning($"[RX-TIMEOUT] ACK retry timeout triggered - State: {currentState}, Last ACK: {lastAckedSeq}, Expected: {expectedSeq}");

                // Use Task.Run to avoid blocking the timer thread
                Task.Run(() =>
                {
                    lock (timerLock)
                    {
                        if (currentState != RxState.Receiving)
                        {
                            Log.Debug("[RX-TIMEOUT] Timer expired but not in receiving state");
                            return;
                        }

                        ackRetryCount++;

                        if (ackRetryCount <= MaxAckRetries)
                        {
                            Log.Warning($"[RX-RETRY] No data for seq {expectedSeq} after ACK {lastAckedSeq} - Retry {ackRetryCount}/{MaxAckRetries}");

                            // Resend the last successful ACK
                            // This tells firmware we didn't receive the next sequence
                            SendAck(lastAckedSeq, IspReturnCodes.SUBCMD_SEQMATCH);

                            // Restart the timer for next retry
                            StartAckRetryTimer();
                        }
                        else
                        {
                            Log.Error($"[RX-TIMEOUT] Max ACK retries exceeded waiting for seq {expectedSeq} - TRANSFER FAILED");

                            // Reset the state
                            Reset();
                            SubCmdResponse = IspSubCmdResponse.FAILED;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[RX-TIMEOUT] Exception in HandleAckRetryTimeout: {ex.Message}");
                Reset();
                SubCmdResponse = IspSubCmdResponse.FAILED;
            }
        }

        void SendAck(ushort seq, IspReturnCodes code)
        {
            Log.Debug($"[RX-ACK] Sending ACK - Seq: {seq}, Code: {code}");

            var frame = IspFramingUtils.EncodeFrame(new byte[]
            {
                (byte)IspResponse.ACK,
                (byte)(seq >> 8),
                (byte)(seq & 0xFF),
                (byte)code
            });

            _ = transport.TransmitAsync(frame);
        }

        void SendNack(ushort seq, IspReturnCodes code)
        {
            Log.Warning($"[RX-NACK] Sending NACK - Seq: {seq}, Code: {code}");

            var frame = IspFramingUtils.EncodeFrame(new byte[]
            {
                (byte)IspResponse.NACK,
                (byte)(seq >> 8),
                (byte)(seq & 0xFF),
                (byte)code
            });

            _ = transport.TransmitAsync(frame);
        }

        void SendAckDone(ushort lastSeq, IspReturnCodes code)
        {
            Log.Info($"[RX-ACK] Sending ACK_DONE - Last seq: {lastSeq}, Code: {code}");

            var frame = IspFramingUtils.EncodeFrame(new byte[]
            {
                (byte)IspResponse.ACK_DONE,
                (byte)(lastSeq >> 8),
                (byte)(lastSeq & 0xFF),
                (byte)code
            });

            _ = transport.TransmitAsync(frame);
        }

        public void Reset()
        {
            // Stop any pending retry timer
            StopAckRetryTimer();

            buffer = null;
            totalSize = 0;
            receivedSize = 0;
            totalDataReceived = 0;
            remainingData = 0;
            expectedSeq = 0;
            nackRetryCount = 0;
            currentState = RxState.Idle;

            // Clear duplicate tracking
            ackedSequences.Clear();
            duplicateAckCount.Clear();
            lastSuccessfulSeq = 0;
            lastDataPacket = null;
            lastAckedSeq = 0;
            ackRetryCount = 0;

            Log.Debug("[RX-RESET] State reset to Idle");
        }

        public void setMaxChunkSize(int size)
        {
            MaxChunkSize = size;
            Log.Info($"[RX-CONFIG] Max chunk size set to {MaxChunkSize} bytes");
        }

        /// <summary>
        /// Returns true if currently in the process of receiving data.
        /// </summary>
        public bool IsReceiving() => currentState == RxState.Receiving;

        /// <summary>
        /// Gets the current expected sequence number (for testing or diagnostics).
        /// </summary>
        public ushort GetExpectedSequence() => expectedSeq;

        public IspSubCmdResponse GetRxSubCmdResponse() => SubCmdResponse;

        public int GetReceivedByteCount() => receivedSize;

        public long GetTotalByteCount() => totalSize;

        public long GetTotalReceivedBytes() => totalDataReceived + receivedSize;
    }
}