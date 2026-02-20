using DTCL;
using DTCL.Cartridges;
using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using IspProtocol;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DTCL.CustomMessageBox;

namespace StandaloneWriteTest
{
    /// <summary>
    /// Console version of MainWindow - exact same logic without GUI
    /// </summary>
    public class ConsoleMainWindow : INotifyPropertyChanged
    {
        const string GUI_VERSION = "10.2-Console";

        StringBuilder _logMessages;
        public HardwareInfo hwInfo;
        PopUpMessagesContainer PopUpMessagesContainerObj;
        bool stopPcFlag;
        bool isLogSelected;
        bool isLogTypeSelected;
        bool isPcStarted;
        bool isPCMode;
        bool isPcStartScan;
        bool IstRun;
        bool copyCompareOperation;
        bool commandInProgress;
        string additionalMsgForDPS = "";
        CartType selectedCartForPC = CartType.Unknown;
        string _pendingMessageId;
        bool _isMstSelected = false;
        
        // Console-specific logging
        StreamWriter _testLog;
        int _testIterations;
        int _successCount;
        int _failureCount;
        string _targetPort;

        public string LogMessages
        {
            get { return _logMessages.ToString(); }
            private set
            {
                _logMessages = new StringBuilder(value);
                OnPropertyChanged(nameof(LogMessages));
            }
        }

        public ConsoleMainWindow(string portName = null, int iterations = 1000)
        {
            _targetPort = portName;
            _testIterations = iterations;
            
            // Initialize log file
            var logDir = "TestLogs";
            Directory.CreateDirectory(logDir);
            _testLog = new StreamWriter($"{logDir}\\ConsoleTest_{DateTime.Now:yyyyMMdd_HHmmss}.log") { AutoFlush = true };
            
            Console.WriteLine($"=== DTCL Console Test v{GUI_VERSION} ===");
            Console.WriteLine($"Target Port: {_targetPort ?? "Auto-detect"}");
            Console.WriteLine($"Iterations: {_testIterations}\n");
            
            Initialize();
        }

        void Initialize()
        {
            _logMessages = new StringBuilder();
            
            // Initialize exactly like MainWindow
            DataHandlerIsp.Instance.ProgressChanged += OnProgressChanged;

            hwInfo = HardwareInfo.Instance;

            // Subscribe to events
            hwInfo.HardwareDetected += OnHwConnected;
            hwInfo.HardwareDisconnected += OnHwDisconnected;
            //hwInfo.CartDetected += OnCartDetected;

            var PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();
            
            try
            {
                PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");
            }
            catch
            {
                Console.WriteLine("Warning: PopUpMessages.json not found, using defaults");
                PopUpMessagesContainerObj = new PopUpMessagesContainer();
            }

            //hwInfo.StartScanning();

            // Initialize SlotInfo tags exactly like MainWindow
            if (hwInfo.SlotInfo != null)
            {
                LogConsole($"Initialized with {hwInfo.SlotInfo.Length} slots");
            }

            // Subscribe to LED state changes
            LedState.LedStateChanged += OnLedStateChanged;
        }

        public async Task RunWriteTests()
        {
            Console.Write("Waiting for hardware detection...");
            await hwInfo.StopScanningAsync();

             bool isconnected = await hwInfo.ScanForHardwareAsync();

            // Wait for hardware to be detected
            int timeout = 100000;
            while (!hwInfo.IsConnected && timeout-- > 0)
            {
                Thread.Sleep(1000);
                Console.Write(".");
            }
            Console.WriteLine();

            if (hwInfo.IsConnected == false)
            {
                Console.WriteLine("ERROR: No hardware detected!");
                return;
            }

            var hw = HardwareInfo.Instance;
            Console.WriteLine($"Found hardware - Board ID: {hwInfo.BoardId}");
            
            // Assume all 4 slots are present with Darin2 cartridges
            SetupMockSlots();
            
            // Run write tests using the exact MainWindow flow
            RunWriteTestsUsingMainWindowFlow();
        }

        void SetupMockSlots()
        {
            Console.WriteLine("Setting up mock slots - assuming all 4 slots have Darin2 cartridges");
            
            // Initialize SlotInfo array if not already done
            /*if (hwInfo.SlotInfo == null || hwInfo.SlotInfo.Length < 5)
            {
                hwInfo.SlotInfo = new SlotInfo[5]; // 0 index unused, slots 1-4
            }*/

            // Setup all 4 slots as Darin2
            for (int i = 1; i <= 4; i++)
            {
                if (hwInfo.SlotInfo[i] == null)
                {
                    hwInfo.SlotInfo[i] = new SlotInfo();
                }
                
                hwInfo.SlotInfo[i].SlotNumber = i;
                hwInfo.SlotInfo[i].DetectedCartTypeAtSlot = CartType.Darin2;
                hwInfo.SlotInfo[i].IsCartDetectedAtSlot = true;
                //hwInfo.SlotInfo[i].CartType = CartType.Darin2;
                
                Console.WriteLine($"  Slot {i}: Darin2 cartridge ready");
            }
        }

        async void RunWriteTestsUsingMainWindowFlow()
        {
            Console.WriteLine("\n=== Starting Write Tests Using MainWindow Flow ===");
            
            // Test slots - assume we want to test slot 1 first
            var targetSlots = new List<int> { 1 };
            
            for (int iteration = 0; iteration < _testIterations; iteration++)
            {
                try
                {
                    LogConsole($"[{iteration}] Starting write operation using ExecuteWriteOperationOnSlots");
                    
                    // Use exact MainWindow flow
                    var progress = new Progress<int>(percent => 
                    {
                        if (percent % 25 == 0)
                        {
                            Console.Write($"\rIteration {iteration}: {percent}%");
                        }
                    });

                    // This is the exact method MainWindow uses
                    await ExecuteWriteOperationOnSlots(this, targetSlots, progress);
                    
                    _successCount++;
                    Console.Write(".");
                    
                    if ((iteration + 1) % 50 == 0)
                    {
                        Console.WriteLine($" [{iteration + 1}/{_testIterations}]");
                    }
                }
                catch (Exception ex)
                {
                    _failureCount++;
                    Console.Write("F");
                    LogConsole($"[{iteration}] Exception in MainWindow flow: {ex.Message}");
                }
                
                // Small delay between iterations
                await Task.Delay(100);
            }

            // Print results
            Console.WriteLine($"\n\n=== TEST RESULTS ===");
            Console.WriteLine($"Total: {_testIterations}");
            Console.WriteLine($"Success: {_successCount} ({(double)_successCount/_testIterations*100:F1}%)");
            Console.WriteLine($"Failures: {_failureCount} ({(double)_failureCount/_testIterations*100:F1}%)");
            Console.WriteLine($"Log file: {(_testLog.BaseStream as FileStream)?.Name}");
        }

        /// <summary>
        /// Execute write operation on specified slots - EXACT copy from MainWindow
        /// </summary>
        async Task ExecuteWriteOperationOnSlots(object sender, List<int> targetSlots, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;
            var processedSlots = 0;

            foreach (int slotNumber in targetSlots)
            {
                processedSlots++;
                var isLastSlot = (processedSlots == targetSlots.Count);

                // Execute write operation on this slot
                var result = await ExecuteWriteOnSingleSlot(sender, slotNumber, progress);

                // Handle result and user feedback
                var shouldContinue = ProcessWriteResult(result, slotNumber, isLastSlot);

                if (!shouldContinue && !isLastSlot)
                {
                    await postCommandExeOper(sender, slotNumber);
                    break;
                }

                await postCommandExeOper(sender, slotNumber);
            }
        }

        /// <summary>
        /// Execute write operation on a single slot - EXACT copy from MainWindow
        /// </summary>
        async Task<int> ExecuteWriteOnSingleSlot(object sender, int slotNumber, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            // Get appropriate cart instance for this slot
            var slotInfo = hardwareInfo.SlotInfo[slotNumber];
            var cartInstance = hardwareInfo.GetCartInstance(slotInfo.DetectedCartTypeAtSlot);

            if (cartInstance == null)
            {
                LogConsole($"ERROR: No cart instance available for slot {slotNumber}, cart type: {slotInfo.DetectedCartTypeAtSlot}");
                return returnCodes.DTCL_NO_RESPONSE;
            }

            // Execute write operation
            return await cartInstance.WriteUploadFiles(
                hardwareInfo.D2UploadFilePath,
                GetUserConfirmation,
                (byte)slotNumber,
                progress);
        }

        /// <summary>
        /// Process write operation result - Console version
        /// </summary>
        bool ProcessWriteResult(int result, int slotNumber, bool isLastSlot)
        {
            var slotMessage = GetSlotMessage(slotNumber);

            if (isLastSlot)
            {
                // Last slot - show completion message only
                ShowCompletionMessage(result, slotMessage);
                return true;
            }
            else
            {
                // Not last slot - in console mode, always continue
                ShowContinueMessage(result, slotMessage);
                return true; // Always continue in console mode
            }
        }

        /// <summary>
        /// Show completion message for the last slot - Console version
        /// </summary>
        void ShowCompletionMessage(int result, string slotMessage)
        {
            if (result == 0)
            {
                Console.WriteLine($"✅ Write Complete: {slotMessage}");
                LogConsole($"Write completed successfully: {slotMessage}");
            }
            else if (result == returnCodes.DTCL_MISSING_HEADER)
            {
                Console.WriteLine($"⚠️  Header Missing: {slotMessage}");
                LogConsole($"Header missing: {slotMessage}");
            }
            else
            {
                Console.WriteLine($"❌ Write Failed: {slotMessage} (Code: {result})");
                LogConsole($"Write failed: {slotMessage}, result code: {result}");
            }
        }

        /// <summary>
        /// Show continue message - Console version
        /// </summary>
        void ShowContinueMessage(int result, string slotMessage)
        {
            if (result == 0)
            {
                Console.WriteLine($"✅ {slotMessage} - continuing...");
            }
            else if (result == returnCodes.DTCL_MISSING_HEADER)
            {
                Console.WriteLine($"⚠️  {slotMessage} Header missing - continuing...");
            }
            else
            {
                Console.WriteLine($"❌ {slotMessage} Failed (Code: {result}) - continuing...");
            }
        }

        /// <summary>
        /// Get slot-specific message for DPS hardware - simplified console version
        /// </summary>
        string GetSlotMessage(int slotNumber)
        {
            return $"Slot {slotNumber}";
        }

        

        CustomMessageBox.MessageBoxResult GetUserConfirmation(string msgID, string AdditionalMsg = "")
        {
            return MessageBoxResult.Yes;
        }

        /// <summary>
        /// Get user confirmation - console always returns true
        /// </summary>
        Task<bool> GetUserConfirmation(string message)
        {
            LogConsole($"User confirmation requested: {message}");
            return Task.FromResult(true); // Auto-confirm in console mode
        }

        /// <summary>
        /// Pre-command operations - simplified console version
        /// </summary>
        async Task preCommandExeOper(object sender, int slotNumber)
        {
            LogConsole($"Pre-command operation for slot {slotNumber}");
            commandInProgress = true;
            await Task.Delay(10); // Small delay
        }

        /// <summary>
        /// Post-command operations - simplified console version
        /// </summary>
        async Task postCommandExeOper(object sender, int slotNumber)
        {
            LogConsole($"Post-command operation for slot {slotNumber}");
            commandInProgress = false;
            await Task.Delay(10); // Small delay
        }

        // Event handlers - minimal console versions
        void OnHwConnected(object sender, EventArgs e)
        {
            Console.WriteLine($"Hardware Connected: {hwInfo.BoardId}");
            LogConsole("Hardware connected event");
        }

        void OnHwDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Hardware Disconnected!");
            LogConsole("Hardware disconnected event");
        }

       /* void OnCartDetected(object sender,CartDetectedEventArgs e)
        {
            Console.WriteLine($"Cart Detected - Slot: {e.SlotNumber}, Type: {e.CartType}");
            LogConsole($"Cart detected: Slot {e.SlotNumber} = {e.CartType}");
        }*/

        void OnLedStateChanged(object sender, LedStateChangedEventArgs e)
        {
            // In console mode, just log LED state changes
            LogConsole($"LED State: Cart {e.CartNo} Busy={e.IsBusy}");
        }

        void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            // Console progress indicator
           // if (e.BytesProcessed % 10 == 0)
            //{
                Console.Write($"\rData transfer: {e.BytesProcessed}%");
            //}
        }

        void LogConsole(string message)
        {
            //var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            //_testLog?.WriteLine(logLine);
            //Debug.WriteLine(logLine);
        }

        public void ShowError(string message)
        {
            Console.WriteLine($"ERROR: {message}");
            LogConsole($"ERROR: {message}");
        }

        public void Cleanup()
        {
            hwInfo?.StopScanningAsync();
            _testLog?.Close();
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    // Entry point for console test
    class Program
    {
        static void Main(string[] args)
        {
            string port = args.Length > 0 ? args[0] : null;
            int iterations = args.Length > 1 ? int.Parse(args[1]) : 1000;

            var consoleWindow = new ConsoleMainWindow(port, iterations);
            
            try
            {
                consoleWindow.RunWriteTests();
            }
            finally
            {
                consoleWindow.Cleanup();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}