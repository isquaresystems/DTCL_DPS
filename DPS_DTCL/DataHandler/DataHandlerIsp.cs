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

        if (_tx.SubCmdResponse == IspSubCmdResponse.SUCESS)
        {
            return _tx.SubCmdResponse;
        }

        if (_rx.SubCmdResponse == IspSubCmdResponse.SUCESS)
        {
            return _rx.SubCmdResponse;
        }
        else
        {
            return IspSubCmdResponse.FAILED;
        }
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

    void OnDataReceived(byte[] rawData)
    {
        try
        {
            if (IspFramingUtils.TryDecodeFrame(rawData, out byte[] payload))
            {
                _cmdManager.HandleData(payload);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Isp RX] Error decoding or dispatching: {ex.Message}");
        }
    }

    public void OnProgressChanged(string operation, long bytesProcessed, long totalBytes, IProgress<int> progress)
    {
        if (progress == null) return;

        var percentage = (int)((double)bytesProcessed / totalBytes * 100);
        progress.Report(percentage);
    }
}