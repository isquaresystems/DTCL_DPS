using DTCL.Transport;
using DTCL.Log;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IspProtocol;
using DTCL.Cartridges;
using System.Threading;
using System.Windows.Markup;
using System.Windows.Documents;
using System.Windows;

public sealed class DataHandlerIsp
{
    static readonly Lazy<DataHandlerIsp> _instance = new Lazy<DataHandlerIsp>(() => new DataHandlerIsp());
    public static DataHandlerIsp Instance => _instance.Value;

    UartIspTransport _transport;
    IspCommandManager _cmdManager;
    public IspCmdTransmitData _tx;
    public IspCmdReceiveData _rx;
    IspCmdControl _ctrl;
    IspSubCommandProcessor _subCommandProcessor;

    public int totalDataSize { get; set; }
    public int totalDataProcessed { get; set; }
    public event EventHandler<ProgressEventArgs> ProgressChanged;
    private DataHandlerIsp() { }

    public void Initialize(UartIspTransport transport, ICart cartObj)
    {
        _transport = transport;

        // _transport.Open();

        _subCommandProcessor = new IspSubCommandProcessor();
        _rx = new IspCmdReceiveData(transport, _subCommandProcessor);
        _tx = new IspCmdTransmitData(transport, _subCommandProcessor);
        _ctrl = new IspCmdControl(transport, _subCommandProcessor);

        if ("DPS2_4_IN_1" == HardwareInfo.Instance.BoardId)
            _rx.setMaxChunkSize(22400);
        else if ("DPS3_4_IN_1" == HardwareInfo.Instance.BoardId)
            _rx.setMaxChunkSize(1023);
        else
            _rx.setMaxChunkSize(22400);

        _cmdManager = new IspCommandManager();
        _cmdManager.AddHandler(_rx);
        _cmdManager.AddHandler(_tx);

        _transport.DataReceived += OnDataReceived;

        Log.Info("[EVT1000] [DataHandlerIsp] Initialized transport and command handlers.");
    }

    public void RegisterSubCommandHandlers(ICart cartObj)
    {
        if (cartObj != null)
        {
            var DarinName = cartObj.GetType().Name;

            if (DarinName == "Darin2")
            {
                _subCommandProcessor.Register((byte)IspSubCommand.D2_WRITE, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D2_READ, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D2_ERASE, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D2_ERASE_BLOCK, cartObj);
                Log.Debug($"RegisterSubCommandHandlers :{DarinName}");
            }

            if (DarinName == "Darin3")
            {
                _subCommandProcessor.Register((byte)IspSubCommand.D3_WRITE, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D3_READ, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D3_READ_FILES, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D3_ERASE, cartObj);
                _subCommandProcessor.Register((byte)IspSubCommand.D3_FORMAT, cartObj);
                Log.Debug($"RegisterSubCommandHandlers :{DarinName}");
            }
        }
    }

    public void SetProgressValues(int totalSize, int processedSize)
    {
        totalDataSize += totalSize;
        totalDataProcessed = processedSize;
        Log.Info($"Total Data Progress Size set to :{totalDataSize}");
        Log.Info($"Total Data processed set to :{totalDataProcessed}");
    }

    public void ResetProgressValues()
    {
        totalDataSize = 0;
        totalDataProcessed = 0;
    }

    public async Task<IspSubCmdResponse> Execute(byte[] payload, IProgress<int> progress)
    {
        var reset = new byte[1];
        reset[0] = (byte)IspCommand.TX_DATA_RESET;
        _cmdManager.HandleData(reset);
        reset[0] = (byte)IspCommand.RX_DATA_RESET;
        _cmdManager.HandleData(reset);

        // CRITICAL: Give firmware time to process RESET commands and clear its state
        // Without this delay, firmware's sequence counter may not reset, causing NACK on retry
        await Task.Delay(50);
        Log.Info("[EXECUTE-RESET] 50ms delay after RESET commands for firmware state stabilization");

        _tx.SubCmdResponse = IspSubCmdResponse.NO_RESPONSE;
        _rx.SubCmdResponse = IspSubCmdResponse.NO_RESPONSE;
        _ctrl.currentState = IspCMDState.IDLE;

        _cmdManager.HandleData(payload);

        var tx = _tx.IsTransmitting();
        var rx = _rx.IsReceiving();
        var ctrl = _ctrl.IsCmdCtrl();

        while (tx || rx || ctrl || (_tx.SubCmdResponse == IspSubCmdResponse.IN_PROGRESS) || (_rx.SubCmdResponse == IspSubCmdResponse.IN_PROGRESS))
        {
            tx = _tx.IsTransmitting();
            rx = _rx.IsReceiving();
            ctrl = _ctrl.IsCmdCtrl();

            if (_tx.IsTransmitting())
            {
                OnProgressChanged("", _tx.GetTransmittedByteCount(), _tx.GetTotalByteCount(), progress);
            }
            else if (_rx.IsReceiving())
            {
                OnProgressChanged("", _rx.GetReceivedByteCount(), _rx.GetTotalByteCount(), progress);
            }

            await Task.Delay(10);
        }

        // Return TX response if it's SUCCESS
        if (_tx.SubCmdResponse == IspSubCmdResponse.SUCESS)
        {
            return _tx.SubCmdResponse;
        }

        // Return RX response if it's SUCCESS
        if (_rx.SubCmdResponse == IspSubCmdResponse.SUCESS)
        {
            return _rx.SubCmdResponse;
        }

        // CRITICAL FIX: Return actual response value (SPURIOUS_RESPONSE, FAILED, etc.)
        // Don't convert SPURIOUS_RESPONSE to FAILED - caller needs to know!
        if (_tx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE)
        {
            return _tx.SubCmdResponse;  // Could be SPURIOUS_RESPONSE, TX_FAILED, etc.
        }

        if (_rx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE)
        {
            return _rx.SubCmdResponse;  // Could be FAILED, etc.
        }

        // Both are NO_RESPONSE - return FAILED as fallback
        return IspSubCmdResponse.FAILED;
    }

    public async Task<byte[]> ExecuteCMD(byte[] payload, int expectedRespLength, int timeOut = 1000, IProgress<int> progress = null)
    {
        if (_ctrl != null)
        {
            var buffer = await _ctrl.ExecuteCmd(payload, expectedRespLength, timeOut);

            return buffer;
        }

        return null;
    }

    // Helper class for frame buffering
    class DecodedFrame
    {
        public byte[] Payload { get; set; }
        public byte[] FullFrame { get; set; }
        public int FrameNumber { get; set; }
        public byte Command { get; set; }
    }

    void OnDataReceived(byte[] rawData)
    {
        try
        {
            // CRITICAL DEBUG: Log raw buffer with ALL bytes - no data loss
            Log.Info($"[ISP-RX-RAW] Received {rawData.Length} bytes: {BitConverter.ToString(rawData)}");

            // OPTION 2: Collect ALL frames FIRST, then intelligently process
            // This prevents processing spurious frames before seeing expected responses
            var decodedFrames = new System.Collections.Generic.List<DecodedFrame>();
            int offset = 0;
            int framesDecoded = 0;
            int bytesConsumed = 0;

            // ====== STEP 1: COLLECT all valid frames from buffer ======
            while (offset < rawData.Length)
            {
                int remainingLen = rawData.Length - offset;
                byte[] segment = new byte[remainingLen];
                Array.Copy(rawData, offset, segment, 0, remainingLen);

                if (IspFramingUtils.TryDecodeFrame(segment, out byte[] payload))
                {
                    // Valid frame decoded - extract full frame bytes
                    int frameSize = 4 + payload.Length;  // START + LEN + PAYLOAD + CRC + END
                    framesDecoded++;
                    bytesConsumed += frameSize;

                    byte[] fullFrame = new byte[frameSize];
                    Array.Copy(rawData, offset, fullFrame, 0, frameSize);

                    var frame = new DecodedFrame
                    {
                        Payload = payload,
                        FullFrame = fullFrame,
                        FrameNumber = framesDecoded,
                        Command = payload.Length > 0 ? payload[0] : (byte)0
                    };

                    decodedFrames.Add(frame);

                    // DETAILED LOGGING: Show frame structure
                    Log.Info($"[ISP-RX-COLLECT] Frame {framesDecoded}: Cmd=0x{frame.Command:X2}, " +
                             $"PayloadLen={payload.Length}, FullFrame={BitConverter.ToString(fullFrame)}");

                    offset += frameSize;
                }
                else
                {
                    // Decode FAILED - log exact bytes that failed
                    Log.Warning($"[ISP-RX-DECODE] FAILED at offset {offset}/{rawData.Length}, " +
                                $"remaining={remainingLen}B: {BitConverter.ToString(segment, 0, Math.Min(20, remainingLen))}");

                    // Search for next START byte to skip garbage
                    int nextStartOffset = -1;
                    for (int i = 1; i < remainingLen; i++)
                    {
                        if (segment[i] == IspFramingUtils.StartByte)
                        {
                            nextStartOffset = i;
                            break;
                        }
                    }

                    if (nextStartOffset >= 0)
                    {
                        Log.Warning($"[ISP-RX] Skipped {nextStartOffset} garbage bytes, found START at offset {offset + nextStartOffset}");
                        offset += nextStartOffset;
                    }
                    else
                    {
                        // No more START bytes - log remaining data
                        int bytesLeft = rawData.Length - offset;
                        if (bytesLeft > 0)
                        {
                            Log.Warning($"[ISP-RX] Discarding {bytesLeft}B at end (no valid frame): " +
                                        $"{BitConverter.ToString(rawData, offset, Math.Min(bytesLeft, 20))}");
                        }
                        break;
                    }
                }
            }

            // ====== STEP 2: ANALYZE what we collected ======
            int totalBytes = rawData.Length;
            int lostBytes = totalBytes - bytesConsumed;

            Log.Info($"[ISP-RX-ANALYZE] Collected {decodedFrames.Count} frames from {totalBytes}B buffer. " +
                     $"Consumed={bytesConsumed}B, Lost={lostBytes}B");

            if (lostBytes > 0)
            {
                Log.Warning($"[ISP-RX-ANALYZE] WARNING: {lostBytes} bytes were NOT decoded into valid frames!");
            }

            // ====== STEP 3: PROCESS collected frames ======
            foreach (var frame in decodedFrames)
            {
                Log.Info($"[ISP-RX-PROCESS] Processing Frame#{frame.FrameNumber}: Cmd=0x{frame.Command:X2}");
                _cmdManager.HandleData(frame.Payload);
            }

            // Log multi-frame behavior
            if (decodedFrames.Count > 1)
            {
                Log.Info($"[ISP-RX] Multi-frame event: {decodedFrames.Count} frames in single USB CDC buffer");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[Isp RX] Error decoding frames: {ex.Message}");
            Log.Error($"[Isp RX] Stack trace: {ex.StackTrace}");
        }
    }

    public void OnProgressChanged(string operation, long bytesProcessed, long totalBytes, IProgress<int> progress)
    {
        if (progress == null) return;

        var percentage = (int)((double)bytesProcessed / totalBytes * 100);
        progress.Report(percentage);
    }
}