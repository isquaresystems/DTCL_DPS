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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DTCL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        const string GUI_VERSION = "10.2";

        StringBuilder _logMessages;
        public HardwareInfo hwInfo;
        PopUpMessagesContainer PopUpMessagesContainerObj;
        bool stopPcFlag;
        DPSButtonManager buttonManager;
        Color defaultColor = Color.FromArgb(255, 39, 174, 96);
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
        RadioButton withCart;
        RadioButton withOutCart;
        TextBox IterationCount;
        TextBox DurationMin;
        TextBox DurationSec;
        TextBox TimeElapsed;
        TextBox CurrentIteration;
        TextBox PCResultDisplay;
        RadioButton IterationSel;
        RadioButton DurationSel;
        ProgressBar PCProgressBar;
        TextBox InspectorName;
        TextBox TestNumber;
        TextBox[] DTCSINo;
        Label[] L_DTCSINo;
        TextBox UnitSINo;
        Button ConfirmLog;
        Button InitiatePC;
        Button StopPc;
        Button OldLog;
        Button NewLog;
        Button ClosePC;
        public MuxWindow _muxWindow;
        public bool isTransitioning;
        public bool isCreatingMuxWindow;
        bool isWindowLoaded;
        bool _hardwareDetectionPopupDismissed;
        public string LogMessages
        {
            get { return _logMessages.ToString(); }
            private set
            {
                _logMessages = new StringBuilder(value);
                OnPropertyChanged(nameof(LogMessages));
            }
        }

        public enum LayoutMode
        {
            DPSLayout = 1,
            DTCLLayout = 2
        }

        LayoutMode _currentLayout = LayoutMode.DTCLLayout;
        public MainWindow(LayoutMode _currentLayout = LayoutMode.DTCLLayout)
        {
            InitializeComponent();

            InitializeDPSButtonManager();

            DPSPerformanceCheckBlock.Visibility = Visibility.Collapsed;
            DTCLPerformanceCheckBlock.Visibility = Visibility.Collapsed;
            this._currentLayout = _currentLayout;
            DTCSINo = new TextBox[5];
            L_DTCSINo = new Label[5];
            SwitchLayout(_currentLayout);

            _logMessages = new StringBuilder();
            DataContext = this;

            DataHandlerIsp.Instance.ProgressChanged += OnProgressChanged;

            hwInfo = HardwareInfo.Instance;

            // Subscribe to events
            hwInfo.HardwareDetected += OnHwConnected;
            hwInfo.HardwareDisconnected += OnHwDisconnected;
            hwInfo.CartDetected += OnCartDetected;

            var PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();

            PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");

            TimeElapsed.IsEnabled = false;
            CurrentIteration.IsEnabled = false;
            PCResultDisplay.IsEnabled = false;
            Darin1Ellipse.Visibility = Visibility.Collapsed;
            Darin1Label.Visibility = Visibility.Collapsed;
            CommandsLabel.Visibility = Visibility.Hidden;
            LoopBack.Visibility = Visibility.Visible;
            hwInfo.StartScanning();

            DataContext = hwInfo;
            CollapseAllDtcSIFields();

            CollapseMasterSlave(Visibility.Collapsed);

            // Subscribe to LED state changes
            LedState.LedStateChanged += OnLedStateChanged;
        }

        void OnLedStateChanged(object sender, LedStateChangedEventArgs e)
        {
            if (!isPCMode)
            {
                Dispatcher.Invoke(() =>
                {
                    Color color;

                    if (hwInfo.BoardId == "DTCL")
                        color = e.IsBusy ? Colors.DodgerBlue : Colors.DodgerBlue;
                    else
                    {
                        color = e.IsBusy ? Colors.Red : Colors.DodgerBlue;
                        UpdateSlotEllipseColor(e.CartNo, color);
                    }
                });
            }
        }

        public void UpdateSlotEllipseColor(int cartNo, Color color)
        {
            if (hwInfo.BoardId == "DTCL")
            {
                switch (cartNo)
                {
                    case 1:
                        Darin1Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    case 2:
                        Darin2Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    case 3:
                        Darin3Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (cartNo)
                {
                    case 1:
                        Slot1Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    case 2:
                        Slot2Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    case 3:
                        Slot3Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    case 4:
                        Slot4Ellipse.Fill = new SolidColorBrush(color);
                        break;
                    default:
                        break;
                }
            }
        }

        public void SwitchLayout(LayoutMode mode)
        {
            _currentLayout = mode;

            switch (mode)
            {
                case LayoutMode.DPSLayout:
                    ShowDPSLayout();
                    withCart = DPSwithCart;
                    withOutCart = DPSwithOutCart;
                    IterationCount = DPSIterationCount;
                    DurationMin = DPSDurationMin;
                    DurationSec = DPSDurationSec;
                    TimeElapsed = DPSTimeElapsed;
                    CurrentIteration = DPSCurrentIteration;
                    PCResultDisplay = DPSPCResultDisplay;
                    IterationSel = DPSIterationSel;
                    DurationSel = DPSDurationSel;
                    PCProgressBar = DPSPCProgressBar;
                    InspectorName = DPSInspectorName;
                    TestNumber = DPSTestNumber;
                    DTCSINo[1] = DPSDTCSINo1;
                    DTCSINo[2] = DPSDTCSINo2;
                    DTCSINo[3] = DPSDTCSINo3;
                    DTCSINo[4] = DPSDTCSINo4;
                    L_DTCSINo[1] = L_DPSDTCSINo1;
                    L_DTCSINo[2] = L_DPSDTCSINo2;
                    L_DTCSINo[3] = L_DPSDTCSINo3;
                    L_DTCSINo[4] = L_DPSDTCSINo4;
                    UnitSINo = DPSUnitSINo;
                    ConfirmLog = DPSConfirmLog;
                    InitiatePC = DPSInitiatePC;
                    StopPc = DPSStopPc;
                    OldLog = DPSOldLog;
                    NewLog = DPSNewLog;
                    ClosePC = DPSClosePC;

                    break;
                case LayoutMode.DTCLLayout:
                    ShowDTCLLayout();
                    withCart = DTCLwithCart;
                    withOutCart = DTCLwithOutCart;
                    withCart = DTCLwithCart;
                    withOutCart = DTCLwithOutCart;
                    IterationCount = DTCLIterationCount;
                    DurationMin = DTCLDurationMin;
                    DurationSec = DTCLDurationSec;
                    TimeElapsed = DTCLTimeElapsed;
                    CurrentIteration = DTCLCurrentIteration;
                    PCResultDisplay = DTCLPCResultDisplay;
                    IterationSel = DTCLIterationSel;
                    DurationSel = DTCLDurationSel;
                    PCProgressBar = DTCLPCProgressBar;
                    InspectorName = DTCLInspectorName;
                    TestNumber = DTCLTestNumber;
                    DTCSINo[1] = DTCLDTCSINo;
                    DTCSINo[2] = DTCLDTCSINo;
                    DTCSINo[3] = DTCLDTCSINo;
                    DTCSINo[4] = DTCLDTCSINo;
                    L_DTCSINo[1] = L_DTCLDTCSINo;
                    L_DTCSINo[2] = L_DTCLDTCSINo;
                    L_DTCSINo[3] = L_DTCLDTCSINo;
                    L_DTCSINo[4] = L_DTCLDTCSINo;
                    UnitSINo = DTCLUnitSINo;
                    ConfirmLog = DTCLConfirmLog;
                    InitiatePC = DTCLInitiatePC;
                    StopPc = DTCLStopPc;
                    OldLog = DTCLOldLog;
                    NewLog = DTCLNewLog;
                    ClosePC = DTCLClosePC;
                    break;
            }
        }

        /// <summary>
        /// Shows the detailed layout (original with slots)
        /// </summary>
        void ShowDPSLayout()
        {
            // Show detailed cartridge section
            DetailedCartridgeSection.Visibility = Visibility.Visible;
            SimpleCartridgeSection.Visibility = Visibility.Collapsed;

            // Update title or any other UI elements specific to detailed mode
            Title = "Main Window";
            HeaderTitle.Content = "DPS 4 IN 1 Cartridge Loader";
            additionalMsgForDPS = " for slot-";
        }

        /// <summary>
        /// Shows the simple layout (DARIN cartridges)
        /// </summary>
        void ShowDTCLLayout()
        {
            DetailedCartridgeSection.Visibility = Visibility.Collapsed;
            SimpleCartridgeSection.Visibility = Visibility.Visible;

            Title = "Main Window";
            HeaderTitle.Content = "Data Transfer Cartridge Loader";
            additionalMsgForDPS = "";
        }

        void SetPeformanceCheckBlockVisibility(Visibility stat)
        {
            switch (_currentLayout)
            {
                case LayoutMode.DPSLayout:
                    DPSPerformanceCheckBlock.Visibility = stat;
                    break;
                case LayoutMode.DTCLLayout:
                    DTCLPerformanceCheckBlock.Visibility = stat;
                    break;
            }
        }

        public void OnHwDisconnected(object sender, EventArgs e)
        {
            Log.Log.Info("[EVT0000] [OnHwDisconnected] Port Closed");

            for (int itr = 1; itr <= 4; itr++)
                UpdateSlotEllipseColor(itr, Colors.ForestGreen);

            CommandsLabel.Visibility = Visibility.Hidden;
            buttonManager.ShowOnlyButtons(new List<Button> { Exit });

            _hardwareDetectionPopupDismissed = false;

            if (hwInfo.BoardId == "DTCL")
            {
                UpdateUserStatus("DTCL_Not_Detected_Msg");
                isWindowLoaded = true;
            }
            else
                UpdateUserStatus("DTCL_Not_Detected_Msg");

            if (!isPCMode)
            {
                if (hwInfo.BoardId == "DTCL")
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                else
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);

                buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });
            }

            Slot1CheckSel.IsChecked = false;
            Slot2CheckSel.IsChecked = false;
            Slot3CheckSel.IsChecked = false;
            Slot4CheckSel.IsChecked = false;
            Slot1CheckMst.IsChecked = false;
            Slot2CheckMst.IsChecked = false;
            Slot3CheckMst.IsChecked = false;
            Slot4CheckMst.IsChecked = false;
            Slot1CheckSlv.IsChecked = false;
            Slot2CheckSlv.IsChecked = false;
            Slot3CheckSlv.IsChecked = false;
            Slot4CheckSlv.IsChecked = false;
        }

        public void OnHwConnected(object sender, EventArgs e)
        {
            Log.Log.Info("[EVT0000] [OnHwConnected] Port Connected");
            CommandsLabel.Visibility = Visibility.Hidden;

            if (!commandInProgress)
            {
                if (hwInfo.BoardId == "DTCL")
                {
                    UpdateUserStatus("DTCL_Detected_Msg");
                    SwitchLayout(LayoutMode.DTCLLayout);
                }
                else
                {
                    UpdateUserStatus("DPS_Detected_Msg");
                    SwitchLayout(LayoutMode.DPSLayout);
                }
            }

            if (!isPCMode)
            {
                if (hwInfo.BoardId == "DTCL")
                {
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(200);
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Detected_Msg"), this);

                        _hardwareDetectionPopupDismissed = true;

                        if (hwInfo.IsConnected && hwInfo.DetectedCartTypeAtHw == CartType.Unknown && !isPCMode)
                        {
                            UpdateUserStatus("Insert_cart_Msg");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(200);
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DPS_Detected_Msg"), this);

                        _hardwareDetectionPopupDismissed = true;

                        if (hwInfo.IsConnected && hwInfo.DetectedCartTypeAtHw == CartType.Unknown && !isPCMode)
                        {
                            UpdateUserStatus("Insert_cart_Msg");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }

                buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });
            }
        }

        void InitializeDPSButtonManager()
        {
            buttonManager = DPSButtonManager.Instance;

            var buttons = new List<Button>
           {
            Erase,
            Write,
            Read,
            Copy,
            Compare_LoopBack,
            Format,
            Utility,
            AppButton,
            LoopBack,
            Close,
            PerformanceCheck
            };

            buttonManager.InitDPSButtonManager(buttons, Exit, LoopBack);

            buttonManager.ShowOnlyExitAtStart();

            KeyDown += Window_KeyDown;
        }

        async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            isPCMode = await buttonManager.HandleKeyDown(e, isPCMode, this);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        async void Write_Click(object sender, RoutedEventArgs e)
        {
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Start_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                return;

            CollapseMasterSlave(Visibility.Collapsed);

            try
            {
                // Get target slots based on hardware type
                var targetSlots = GetTargetSlotsForOperation();

                if (targetSlots.Count == 0)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Slot_Not_Selected_Msg"), this);
                    return;
                }

                var progress = new Progress<int>(value => OperationProgressBar.Value = value);

                // Execute write operation on target slots
                await ExecuteWriteOperationOnSlots(sender, targetSlots, progress);
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Write operation failed: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Failed_Msg"), this);
            }
        }

        /// <summary>
        /// Get target slots for operation based on hardware type
        /// DTCL: Returns active slot only
        /// DPS: Returns all checked/selected slots
        /// </summary>
        List<int> GetTargetSlotsForOperation()
        {
            var targetSlots = new List<int>();
            var hardwareInfo = HardwareInfo.Instance;

            switch (hardwareInfo.HardwareType)
            {
                case HardwareType.DTCL:
                    // DTCL: Only one active slot at a time
                    var activeSlot = hardwareInfo.GetActiveSlot();
                    if (activeSlot > 0 && hardwareInfo.SlotInfo[activeSlot].IsCartDetectedAtSlot)
                    {
                        targetSlots.Add(activeSlot);
                    }

                    break;

                case HardwareType.DPS2_4_IN_1:
                case HardwareType.DPS3_4_IN_1:
                    // DPS: All checked slots
                    targetSlots.AddRange(GetCheckedSlotsFromUI());
                    break;
            }

            return targetSlots;
        }

        /// <summary>
        /// Get checked slots from UI checkboxes (DPS only)
        /// </summary>
        List<int> GetCheckedSlotsFromUI()
        {
            var checkedSlots = new List<int>();

            // Check each slot checkbox and add to list if checked and operational
            var hardwareInfo = HardwareInfo.Instance;

            if (Slot1CheckSel?.IsChecked == true && hardwareInfo.SlotInfo[1].IsCartDetectedAtSlot)
                checkedSlots.Add(1);

            if (Slot2CheckSel?.IsChecked == true && hardwareInfo.SlotInfo[2].IsCartDetectedAtSlot)
                checkedSlots.Add(2);

            if (Slot3CheckSel?.IsChecked == true && hardwareInfo.SlotInfo[3].IsCartDetectedAtSlot)
                checkedSlots.Add(3);

            if (Slot4CheckSel?.IsChecked == true && hardwareInfo.SlotInfo[4].IsCartDetectedAtSlot)
                checkedSlots.Add(4);

            return checkedSlots;
        }

        /// <summary>
        /// Execute write operation on specified slots
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
        /// Execute write operation on a single slot
        /// </summary>
        async Task<int> ExecuteWriteOnSingleSlot(object sender, int slotNumber, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            await preCommandExeOper(sender, slotNumber);

            // Get appropriate cart instance for this slot
            var slotInfo = hardwareInfo.SlotInfo[slotNumber];
            var cartInstance = hardwareInfo.GetCartInstance(slotInfo.DetectedCartTypeAtSlot);

            if (cartInstance == null)
            {
                Log.Log
                    .Error($"No cart instance available for slot {slotNumber}, cart type: {slotInfo.DetectedCartTypeAtSlot}");

                return returnCodes.DTCL_NO_RESPONSE;
            }

            // Execute write operation
            return await cartInstance.WriteUploadFiles(
                GetUploadPathForCartType(slotInfo.DetectedCartTypeAtSlot),
                GetUserConfirmation,
                (byte)slotNumber,
                progress);
        }

        /// <summary>
        /// Process write operation result and show appropriate messages
        /// </summary>
        bool ProcessWriteResult(int result, int slotNumber, bool isLastSlot)
        {
            var hardwareInfo = HardwareInfo.Instance;
            var slotMessage = GetSlotMessage(slotNumber);

            if (isLastSlot)
            {
                // Last slot - show completion message only
                ShowCompletionMessage(result, slotMessage);
                return true;
            }
            else
            {
                // Not last slot - ask user if they want to continue
                return ShowContinueMessage(result, slotMessage);
            }
        }

        /// <summary>
        /// Show completion message for the last slot
        /// </summary>
        void ShowCompletionMessage(int result, string slotMessage)
        {
            if (result == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Complete_Msg"), this, slotMessage);
            }
            else if (result == returnCodes.DTCL_MISSING_HEADER)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Missing_Msg_Write"), this, slotMessage);
            }
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Failed_Msg"), this, slotMessage);
            }
        }

        /// <summary>
        /// Show continue message and return user choice
        /// </summary>
        bool ShowContinueMessage(int result, string slotMessage)
        {
            CustomMessageBox.MessageBoxResult userChoice;
            var continueMsg = slotMessage + " do you want to continue for next slot?";

            if (result == 0)
            {
                userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Complete_Continue_Msg"), this, continueMsg);
            }
            else if (result == returnCodes.DTCL_MISSING_HEADER)
            {
                userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Missing_Msg_Write_Continue"), this, continueMsg);
            }
            else
            {
                userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Write_Failed_Msg_Continue"), this, continueMsg);
            }

            return userChoice != CustomMessageBox.MessageBoxResult.No;
        }

        /// <summary>
        /// Get slot-specific message for DPS hardware
        /// </summary>
        string GetSlotMessage(int slotNumber)
        {
            var hardwareInfo = HardwareInfo.Instance;

            if (hardwareInfo.HardwareType != HardwareType.DTCL)
            {
                return " for Slot-" + slotNumber.ToString();
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Get upload path based on cart type
        /// </summary>
        string GetUploadPathForCartType(CartType cartType)
        {
            switch (cartType)
            {
                case CartType.Darin1:
                    return "c:\\mps\\DARIN1\\upload\\";
                case CartType.Darin2:
                    return "c:\\mps\\DARIN2\\upload\\";
                case CartType.Darin3:
                    return "c:\\mps\\DARIN3\\upload\\";
                default:
                    return "c:\\mps\\DARIN2\\upload\\"; // Default fallback
            }
        }

        CustomMessageBox.MessageBoxResult GetUserConfirmation(string msgID, string AdditionalMsg = "")
        {
            CustomMessageBox.MessageBoxResult result;

            if ((msgID == "Copy_completed_Msg") || (msgID == "Copy_Failed_Msg") || (msgID == "Compare_Completed_Msg") || (msgID == "Compare_Failed_Msg"))
            {
                result = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById(msgID), this, AdditionalMsg);
            }
            else
                result = CustomMessageBox.Show2(PopUpMessagesContainerObj.FindMessageById(msgID), this, AdditionalMsg);

            return result;
        }

        // Update the progress bar smoothly
        void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationProgressBar.Maximum = e.TotalBytes;
                OperationProgressBar.Value = e.BytesProcessed;
            });

            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        void Utility_Click(object sender, RoutedEventArgs e)
        {
            uint noOfSelSlots = 0;
            // Count selected slots
            for (int itr2 = 1; itr2 <= hwInfo.GetSlotCount(); itr2++)
                if (hwInfo.SlotInfo[itr2].IsSlotSelected_ByUser == true)
                    ++noOfSelSlots;

            if (noOfSelSlots > 1)
            {
                CustomMessageBox.MessageBoxResult userChoice2;
                userChoice2 = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_Slot_Utility_Msg"), this);
                return;
            }

            var sel = false;

            for (int itr = 1; itr <= hwInfo.GetSlotCount(); itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                {
                    sel = true;
                    var utility = new Utility(HardwareInfo.Instance.BoardId, (byte)hwInfo.SlotInfo[itr].SlotNumber, true);
                    utility.Show();
                    break;
                }
            }

            if (sel == false)
            {
                Log.Log.Warning("No cart Inserted for DTCL to use utility, hence display D2 as default Utility layout");
                var utility = new Utility(HardwareInfo.Instance.BoardId, 2);
                utility.Show();
            }
            AppButton.Visibility = Visibility.Visible;
            LoopBack.Visibility = Visibility.Visible;
            Utility.Visibility = Visibility.Hidden;
        }

        async void Read_Click(object sender, RoutedEventArgs e)
        {
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Start_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                return;

            uint noOfSelSlots = 0;

            for (int itr2 = 1; itr2 <= hwInfo.GetSlotCount(); itr2++)
                if (hwInfo.SlotInfo[itr2].IsSlotSelected_ByUser == true)
                    ++noOfSelSlots;

            if (noOfSelSlots > 1)
            {
                CustomMessageBox.MessageBoxResult userChoice2;
                userChoice2 = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_Slot_Read_Msg"), this);
                return;
            }

            CollapseMasterSlave(Visibility.Collapsed);
            var progress = new Progress<int>(value => OperationProgressBar.Value = value);

            var result = returnCodes.DTCL_NO_RESPONSE;
            var sel = false;

            uint processedSlots = 0;

            for (int itr = 1; itr <= hwInfo.GetSlotCount(); itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                {
                    sel = true;
                    processedSlots++;

                    await preCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);
                    result = await hwInfo.CartObj.ReadDownloadFiles(hwInfo.CartDownloadFilePath, GetUserConfirmation, (byte)hwInfo.SlotInfo[itr].SlotNumber, progress);

                    if (hwInfo.BoardId != "DTCL")
                        additionalMsgForDPS = " for Slot-" + itr.ToString();
                    else
                        additionalMsgForDPS = "";

                    var isLastSelectedSlot = (processedSlots == noOfSelSlots);

                    if (isLastSelectedSlot)
                    {
                        if (result == returnCodes.DTCL_SUCCESS)
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Complete_Msg"), this, additionalMsgForDPS);
                        else if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Blank_Cart_Msg"), this, additionalMsgForDPS);
                        else if (result == returnCodes.DTCL_MISSING_HEADER)
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Missing_Msg_Read"), this, additionalMsgForDPS);
                        else
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Failed_Msg"), this, additionalMsgForDPS);
                    }
                    else
                    {
                        CustomMessageBox.MessageBoxResult userChoice;

                        if (result == returnCodes.DTCL_SUCCESS)
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Complete_Continue_Msg"), this, additionalMsgForDPS + " do you want to continue for next slot?");
                        else if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Blank_Cart_Continue_Msg"), this, additionalMsgForDPS + " do you want to continue for next slot?");
                        else if (result == returnCodes.DTCL_MISSING_HEADER)
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Header_Missing_Continue_Msg_Read"), this, additionalMsgForDPS + " do you want to continue for next slot?");
                        else
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Read_Failed_Continue_Msg"), this, additionalMsgForDPS + " do you want to continue for next slot?");

                        if (userChoice == CustomMessageBox.MessageBoxResult.No)
                            break;
                    }

                    await postCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);
                    result = returnCodes.DTCL_NO_RESPONSE;
                }
            }

            if (!sel)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Slot_Not_Selected_Msg"), this);
            }
        }

        async void Erase_Click(object sender, RoutedEventArgs e)
        {
            CollapseMasterSlave(Visibility.Collapsed);
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Erase_Start_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                return;

            var progress = new Progress<int>(value => OperationProgressBar.Value = value);

            var result = returnCodes.DTCL_OPER_NOTSTARTED;
            var sel = false;
            uint noOfSelSlots = 0;

            // Count selected slots
            for (int itr2 = 1; itr2 <= hwInfo.GetSlotCount(); itr2++)
                if (hwInfo.SlotInfo[itr2].IsSlotSelected_ByUser == true)
                    ++noOfSelSlots;

            uint processedSlots = 0; // Track how many slots have been processed

            for (int itr = 1; itr <= hwInfo.GetSlotCount(); itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                {
                    sel = true;
                    processedSlots++;

                    await preCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);
                    result = await hwInfo.CartObj.EraseCartFiles(progress, (byte)hwInfo.SlotInfo[itr].SlotNumber, true);
                    await postCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);

                    if (hwInfo.BoardId != "DTCL")
                        additionalMsgForDPS = " for Slot-" + itr.ToString();
                    else
                        additionalMsgForDPS = "";

                    var isLastSelectedSlot = (processedSlots == noOfSelSlots);

                    if (isLastSelectedSlot)
                    {
                        if (result == 0)
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Erase_Complete_Msg"), this, additionalMsgForDPS);
                        else
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Erase_Failed_Msg"), this, additionalMsgForDPS);
                    }
                    else
                    {
                        CustomMessageBox.MessageBoxResult userChoice;

                        if (result == 0)
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Erase_Complete_Continue_Msg"), this, additionalMsgForDPS + " do you want to continue for next slot?");
                        else
                            userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Erase_Failed_Continue_Msg"), this, additionalMsgForDPS + " do you want to continue for next slot?");

                        if (userChoice == CustomMessageBox.MessageBoxResult.No)
                            break; 
                    }

                    result = returnCodes.DTCL_OPER_NOTSTARTED;
                }
            }

            if (!sel)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Slot_Not_Selected_Msg"), this);
            }
        }

        async void Copy_Click(object sender, RoutedEventArgs e)
        {
            var hardwareInfo = HardwareInfo.Instance;

            masterLabel.Text = "Master";
            slaveLabel.Text = "Slave";
            copyCompareOperation = true;

            var progress = new Progress<int>(value => OperationProgressBar.Value = value);

            try
            {
                if (!hardwareInfo.IsConnected)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Hardware_Not_Connected_Msg"), this);
                    return;
                }

                switch (hardwareInfo.HardwareType)
                {
                    case HardwareType.DTCL:
                        await PerformDTCLCopyOperation(sender, progress);
                        break;

                    case HardwareType.DPS2_4_IN_1:
                    case HardwareType.DPS3_4_IN_1:
                        CollapseMasterSlave(Visibility.Visible);
                        await PerformDPSCopyOperation(sender, progress);
                        break;

                    default:
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Unsupported_Hardware_Msg"), this);
                        break;
                }
            }
            finally
            {
                copyCompareOperation = false;
                CollapseMasterSlave(Visibility.Collapsed);
            }
        }

        async Task PerformDTCLCopyOperation(object sender, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            var activeSlot = hardwareInfo.GetActiveSlot();

            if (activeSlot == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("No_Active_Cart_Msg"), this);
                return;
            }

            var userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Copy_start_Msg"), this);

            if (userChoice != CustomMessageBox.MessageBoxResult.Ok)
            {
                return;
            }

            await preCommandExeOper(sender, activeSlot);

            var cartInstance = hardwareInfo.CartObj;

            if (cartInstance == null)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Cart_Not_Supported_Msg"), this);
                return;
            }
            
            byte[] Slaves = { (byte)activeSlot, 0, 0, 0 };
            var result = await cartInstance.CopyCartFiles(hwInfo.CartCopyFilePath, GetUserConfirmation, UpdateUserStatus, (byte)activeSlot, Slaves, progress);

            if (result == 0)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Copy_completed_Msg"), this);
            else if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Master_Blank_Msg2"), this);
            else
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Copy_Failed_Msg"), this);

            await postCommandExeOper(sender, activeSlot);
        }

        async Task PerformDPSCopyOperation(object sender, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            var masterSlot = hardwareInfo.GetMasterSlot();

            if (masterSlot == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Master_Not_Selected_Msg"), this);
                return;
            }

            var slaveSlots = hardwareInfo.GetSlaveSlots();

            if (slaveSlots.Length == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Slave_Not_Selected_Msg"), this);
                return;
            }

            // Validate all selected slots are operational
            if (!hardwareInfo.SlotInfo[masterSlot].IsCartDetectedAtSlot)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Master_Cart_Not_Detected_Msg"), this);
                return;
            }

            foreach (var slaveSlot in slaveSlots)
            {
                if (!hardwareInfo.SlotInfo[slaveSlot].IsCartDetectedAtSlot)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Slave_Cart_Not_Detected_Msg"), this, $" for Slot-{slaveSlot}");
                    return;
                }
            }

            // Get user confirmation
            var userChoice = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Copy_start_Msg"), this);

            if (userChoice != CustomMessageBox.MessageBoxResult.Ok)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Copy_Failed_Msg"), this);
                return;
            }

            // Perform copy operation
            await preCommandExeOper(sender, masterSlot);

            var cartInstance = hardwareInfo.CartObj;

            if (cartInstance == null)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Cart_Not_Supported_Msg"), this);
                return;
            }

            // Convert slave slots to byte array
            byte[] slavesArray = { 0, 0, 0, 0 };

            for (int i = 0; i < slaveSlots.Count() && i < 4; i++)
                slavesArray[i] = (byte)slaveSlots[i];

            var result = await cartInstance.CopyCartFiles(hwInfo.CartCopyFilePath, GetUserConfirmation, UpdateUserStatus, (byte)masterSlot, slavesArray, progress);

            // Display result
            if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Master_Blank_Msg2"), this);
            }
            else if (result == 0)
            {
                // Success handled by cart operation
            }
            else
            {
                // Error handled by cart operation
            }

            await postCommandExeOper(sender, masterSlot);
        }

        int GetMasterSlot()
        {
            // Check each slot to find which one is selected AND marked as master
            if (Slot1CheckSel.IsChecked == true && Slot1CheckMst.IsChecked == true) return 1;
            if (Slot2CheckSel.IsChecked == true && Slot2CheckMst.IsChecked == true) return 2;
            if (Slot3CheckSel.IsChecked == true && Slot3CheckMst.IsChecked == true) return 3;
            if (Slot4CheckSel.IsChecked == true && Slot4CheckMst.IsChecked == true) return 4;

            return 0; // No master slot found
        }

        List<int> GetSlaveSlots()
        {
            var slaveSlots = new List<int>();

            // Check each slot to find which ones are selected AND marked as slaves
            if (Slot1CheckSel.IsChecked == true && Slot1CheckSlv.IsChecked == true) slaveSlots.Add(1);
            if (Slot2CheckSel.IsChecked == true && Slot2CheckSlv.IsChecked == true) slaveSlots.Add(2);
            if (Slot3CheckSel.IsChecked == true && Slot3CheckSlv.IsChecked == true) slaveSlots.Add(3);
            if (Slot4CheckSel.IsChecked == true && Slot4CheckSlv.IsChecked == true) slaveSlots.Add(4);

            return slaveSlots;
        }

        async void Compare_Click(object sender, RoutedEventArgs e)
        {
            var hardwareInfo = HardwareInfo.Instance;

            // Setup UI for compare operation
            copyCompareOperation = true;
            masterLabel.Text = "1st Cart";
            slaveLabel.Text = "2nd Cart";

            // Initialize progress tracking
            var progress = new Progress<int>(value => OperationProgressBar.Value = value);

            try
            {
                // Validate hardware connection
                if (!hardwareInfo.IsConnected)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Hardware_Not_Connected_Msg"), this);
                    return;
                }

                // Handle different hardware types
                switch (hardwareInfo.HardwareType)
                {
                    case HardwareType.DTCL:
                        await PerformDTCLCompareOperation(sender, progress);
                        break;

                    case HardwareType.DPS2_4_IN_1:
                    case HardwareType.DPS3_4_IN_1:
                        CollapseMasterSlave(Visibility.Visible);
                        await PerformDPSCompareOperation(sender, progress);
                        break;

                    default:
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Unsupported_Hardware_Msg"), this);
                        break;
                }
            }
            finally
            {
                copyCompareOperation = false;
                CollapseMasterSlave(Visibility.Collapsed);
            }
        }

        async Task PerformDTCLCompareOperation(object sender, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            // For DTCL, only one cart can be active at a time
            var activeSlot = hardwareInfo.GetActiveSlot();

            if (activeSlot == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("No_Active_Cart_Msg"), this);
                return;
            }

            // DTCL compare: comparing single cart data
            await preCommandExeOper(sender, activeSlot);

            var cartInstance = hardwareInfo.CartObj;

            if (cartInstance == null)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Cart_Not_Supported_Msg"), this);
                return;
            }

            // DTCL compare: source and comparison target are the same cart
            byte[] Slaves = { (byte)activeSlot, 0, 0, 0 };
            var result = await cartInstance.CompareCartFiles(GetUserConfirmation, UpdateUserStatus, (byte)activeSlot, Slaves, progress);

            // Display result
            if (result == 0)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Compare_Completed_Msg"), this);
            else if (result == returnCodes.DTCL_CMD_ABORT)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Compare_Cancelled_Msg"), this);
            else if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Ist_Blank_Msg"), this);
            else if (result == returnCodes.DTCL_BLANK_CARTRIDGE2)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("SecondCart_Blank_Msg"), this);
            else
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Compare_Failed_Msg"), this);

            await postCommandExeOper(sender, activeSlot);
        }

        async Task PerformDPSCompareOperation(object sender, IProgress<int> progress)
        {
            var hardwareInfo = HardwareInfo.Instance;

            // Get first and second carts from UI (using master/slave checkboxes)
            var firstCart = hardwareInfo.GetMasterSlot();

            if (firstCart == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("1stCart_Not_Selected_Msg"), this);
                return;
            }

            var secondCarts = hardwareInfo.GetSlaveSlots();

            if (secondCarts.Length == 0)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("2ndCart_Not_Selected_Msg"), this);
                return;
            }

            // Validate all selected slots are operational
            if (!hardwareInfo.SlotInfo[firstCart].IsCartDetectedAtSlot)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("1stCart_Not_Detected_Msg"), this);
                return;
            }

            foreach (var secondCart in secondCarts)
            {
                if (!hardwareInfo.SlotInfo[secondCart].IsCartDetectedAtSlot)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("2ndCart_Not_Detected_Msg"), this, $" for Slot-{secondCart}");
                    return;
                }
            }

            // Perform compare operation
            await preCommandExeOper(sender, firstCart);

            var cartInstance = hardwareInfo.CartObj;

            if (cartInstance == null)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Cart_Not_Supported_Msg"), this);
                return;
            }

            // Convert second carts to byte array
            byte[] secondCartsArray = { 0, 0, 0, 0 };

            for (int i = 0; i < secondCarts.Count() && i < 4; i++)
                secondCartsArray[i] = (byte)secondCarts[i];

            var result = await cartInstance.CompareCartFiles(GetUserConfirmation, UpdateUserStatus, (byte)firstCart, secondCartsArray, progress);

            await postCommandExeOper(sender, firstCart);

            // Display result (only showing error messages for DPS)
            if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Ist_Blank_Msg"), this);
            }
            else if (result == returnCodes.DTCL_BLANK_CARTRIDGE2)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("SecondCart_Blank_Msg"), this);
            }
            // Success and other messages handled by cart operation itself
        }

        async void Format_Click(object sender, RoutedEventArgs e)
        {
            CollapseMasterSlave(Visibility.Collapsed);

            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Format_Start_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                return;

            var progress = new Progress<int>(value => OperationProgressBar.Value = value);

            for (int itr = 1; itr <= hwInfo.GetSlotCount(); itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                {
                    await preCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);

                    var result = await hwInfo.CartObj.Format(progress, (byte)hwInfo.SlotInfo[itr].SlotNumber);

                    if (result == 0)
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Format_Completed_Msg"), this);
                    else
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Format_Failed_Msg"), this);

                    await postCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);
                }
            }
        }

        void Exit_Click(object sender, RoutedEventArgs e)
        {
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Exit_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
            {
                Log.Log.Warning($"User chose to cancel exit");
                return;
            }

            var targetSlots = GetTargetSlotsForOperation();

            if ((targetSlots.Count == 0) && (hwInfo.BoardId == IspBoardId.DTCL.ToString()))
            {
                targetSlots.Add(0); // Log for slot 0 if no specific slots are selected
                PCLog.Instance.AddEntry("Performance Check Exited", hwInfo.SlotInfo[0]);
            }
            else
            {
                PCLog.Instance.AddEntry("Performance Check Exited", hwInfo.SlotInfo[targetSlots.Count]);
            }

            this.Close();
            Environment.Exit(0);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // If transitioning, just let it close without shutdown
            if (isTransitioning)
            {
                hwInfo.HardwareDetected -= OnHwConnected;
                hwInfo.HardwareDisconnected -= OnHwDisconnected;
                hwInfo.CartDetected -= OnCartDetected;
                // Don't shutdown, MuxWindow is taking over
                return;
            }

            var targetSlots = GetTargetSlotsForOperation();

            // Normal close - perform cleanup and shutdown
            foreach (int slotNumber in targetSlots)
                PCLog.Instance.AddEntry("Performance Test Exited", hwInfo.SlotInfo[slotNumber]);

            // Proper cleanup to ensure process termination
            PerformApplicationCleanup();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Performs comprehensive application cleanup to ensure proper process termination
        /// </summary>
        async void PerformApplicationCleanup()
        {
            try
            {
                // Unsubscribe from events to prevent memory leaks
                if (hwInfo != null)
                {
                    hwInfo.HardwareDetected -= OnHwConnected;
                    hwInfo.HardwareDisconnected -= OnHwDisconnected;
                    hwInfo.CartDetected -= OnCartDetected;

                    // Stop hardware scanning timer and dispose resources
                    await hwInfo.StopScanningAsync();
                    hwInfo.Dispose();
                }

                // Unsubscribe from other events
                if (DataHandlerIsp.Instance != null)
                {
                    DataHandlerIsp.Instance.ProgressChanged -= OnProgressChanged;
                }

                LedState.LedStateChanged -= OnLedStateChanged;

                // Close MUX window if open
                if (_muxWindow != null)
                {
                    _muxWindow.Close();
                    _muxWindow = null;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't prevent application shutdown
                Log.Log.Error($"Error during application cleanup: {ex.Message}");
            }
        }

        async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // await Task.Delay(500); // Wait for window to be fully rendered
            Darin1Ellipse.Fill = new SolidColorBrush(Colors.ForestGreen);
            Darin2Ellipse.Fill = new SolidColorBrush(Colors.ForestGreen);
            Darin3Ellipse.Fill = new SolidColorBrush(Colors.ForestGreen);

            // Show pending message for initial hardware detection
            if (!string.IsNullOrEmpty(_pendingMessageId))
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById(_pendingMessageId), this);
                _pendingMessageId = null; // Clear after showing
            }
            else if (hwInfo.IsConnected && !isPCMode)
            {
                // Handle case where hardware was already connected during startup
                if (hwInfo.BoardId == "DTCL")
                {
                    await hwInfo.StopScanningAsync();
                    UpdateUserStatus("DTCL_Detected_Msg");
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Detected_Msg"), this);
                    hwInfo.StartScanning();
                }
                else
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DPS_Detected_Msg"), this);
                }
            }
            else
            {
                if ("DTCL" == hwInfo.BoardId)
                {
                    UpdateUserStatus("DTCL_Not_Detected_Msg");
                    var result = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                }
                else
                {
                    UpdateUserStatus("DTCL_Not_Detected_Msg");
                    var result = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                }

                buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });
            }

            disableLogEntries();

            // Show pending message after window is completely loaded
            if (!string.IsNullOrEmpty(_pendingMessageId))
            {
                // Additional delay to ensure window is fully rendered
                await Task.Delay(500);

                // Show the message box
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById(_pendingMessageId), this);

                // Clear the pending message
                _pendingMessageId = null;
            }

            isWindowLoaded = true;
        }

        public void OnCartDetected(object sender, CartDetectionEventArgs e)
        {
            OnCartChanged(sender, e);

            if (e.CartType != CartType.MultiCart)
            {
                if (!copyCompareOperation && e.CartType != CartType.Unknown)
                    UpdateUserStatus("DTCL_Detected_Msg");

                Log.Log.Debug($"OnCartDetected isPCMode :{isPCMode}");

                if ((isPCMode) && (!isPcStarted) && e.CartType != CartType.Unknown)
                {
                    UpdateUserStatus("DTCL_Detected_Msg");
                }
                else if ((isPCMode) && (!isPcStarted) && e.CartType == CartType.Unknown)
                {
                    UpdateUserStatus("DTCL_Detected_Msg");
                }

                if ((isPCMode) && (isPcStarted))
                {
                    UpdateUserStatus("Exe_Progress_Msg");

                    isPcStartScan = true;
                    Log.Log.Debug($"OnCartDetected stopping scan");

                    if (withCart.IsChecked == true)
                    {
                        if (e.CartType == CartType.Darin3)
                        {
                            if (!isPCMode)
                            {
                                CommandsLabel.Visibility = Visibility.Visible;
                                buttonManager.ShowOrHideOnlyListButtons(new List<Button> { PerformanceCheck, Exit, Write, Erase, Read, Compare_LoopBack, Format }, true);
                            }
                            else
                            {
                                CommandsLabel.Visibility = Visibility.Visible;
                                buttonManager.ShowOrHideOnlyListButtons(new List<Button> { PerformanceCheck, Exit, Write, Erase, Read, Compare_LoopBack }, true);
                            }
                        }
                        else
                        {
                            CommandsLabel.Visibility = Visibility.Visible;
                            buttonManager.ShowOrHideOnlyListButtons(new List<Button> { PerformanceCheck, Exit, Write, Erase, Read, Compare_LoopBack }, true);
                        }
                    }
                    else
                    {
                        buttonManager.ShowOrHideOnlyListButtons(new List<Button> { Exit, PerformanceCheck, Compare_LoopBack }, true);
                        buttonManager.ShowOrHideOnlyListButtons(new List<Button> { Write, Erase, Read }, false);
                    }
                }
                else
                {
                    // When DTCL hardware is connected but no cart detected
                    if (hwInfo.IsConnected && hwInfo.DetectedCartTypeAtHw == CartType.Unknown && !isPCMode)
                    {
                        // Show appropriate message based on whether user has seen the hardware detection popup
                        if (_hardwareDetectionPopupDismissed)
                        {
                            CommandsLabel.Visibility = Visibility.Hidden;
                            buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });
                            UpdateUserStatus("Insert_cart_Msg");
                        }
                        else
                        {
                            UpdateUserStatus("DTCL_Detected_Msg");
                        }
                    }
                }
            }
            else
            {
                if (isPCMode)
                {
                    CommandsLabel.Visibility = Visibility.Hidden;

                    buttonManager.ShowOnlyButtons(new List<Button> { Exit, PerformanceCheck });

                    if (hwInfo.IsConnected == true)
                        UpdateUserStatus("Idle_Msg");
                }
                else
                {
                    CommandsLabel.Visibility = Visibility.Hidden;
                    buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });

                    if ((hwInfo.IsConnected) && ((e.CartType == CartType.MultiCart)))
                        UpdateUserStatus("Multi_cart_Msg");
                    else if (hwInfo.IsConnected && (e.CartType == CartType.Unknown))
                    {
                        CommandsLabel.Visibility = Visibility.Hidden;
                        buttonManager.ShowOnlyButtons(new List<Button> { Exit, LoopBack, AppButton });
                        UpdateUserStatus("Insert_cart_Msg");
                    }
                }
            }
        }

        void OnCartChanged(object sender, CartDetectionEventArgs e)
        {
            // Determine slot color based on detection status (same logic as UpdateSlotColor)
            Color slotColor;

            switch (e.Status)
            {
                case DetectionStatus.Detected:
                    slotColor = Colors.DodgerBlue;
                    break;
                case DetectionStatus.Error:
                    slotColor = Colors.Red;
                    break;
                default: // NotDetected or Unknown
                    slotColor = Colors.ForestGreen;
                    break;
            }

            if (hwInfo.BoardId == "DTCL")
            {
                switch (e.SlotNumber)
                {
                    case 1:
                        Darin1Ellipse.Fill = new SolidColorBrush(slotColor);
                        if (slotColor == Colors.DodgerBlue)
                        {
                            Darin1Ellipse.Visibility = Visibility.Visible;
                            Darin1Label.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            Darin1Ellipse.Visibility = Visibility.Collapsed;
                            Darin1Label.Visibility = Visibility.Collapsed;
                        }

                        break;
                    case 2:
                        Darin2Ellipse.Fill = new SolidColorBrush(slotColor);
                        break;
                    case 3:
                        Darin3Ellipse.Fill = new SolidColorBrush(slotColor);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (e.SlotNumber)
                {
                    case 1:
                        Slot1Ellipse.Fill = new SolidColorBrush(slotColor);
                        if (slotColor == Colors.ForestGreen)
                        {
                            Slot1CheckMst.IsChecked = false;
                            Slot1CheckSel.IsChecked = false;
                            Slot1CheckSlv.IsChecked = false;
                            DTCSINo[1].Visibility = Visibility.Collapsed;
                            L_DPSDTCSINo1.Visibility = Visibility.Collapsed;
                            Slot1CheckMst.IsEnabled = false;
                            Slot1CheckSel.IsEnabled = false;
                            Slot1CheckSlv.IsEnabled = false;
                        }
                        else
                        {
                            Slot1CheckMst.IsEnabled = true;
                            Slot1CheckSel.IsEnabled = true;
                            Slot1CheckSlv.IsEnabled = true;
                        }

                        break;
                    case 2:
                        Slot2Ellipse.Fill = new SolidColorBrush(slotColor);
                        if (slotColor == Colors.ForestGreen)
                        {
                            Slot2CheckMst.IsChecked = false;
                            Slot2CheckSel.IsChecked = false;
                            Slot2CheckSlv.IsChecked = false;
                            Slot2CheckMst.IsEnabled = false;
                            Slot2CheckSel.IsEnabled = false;
                            Slot2CheckSlv.IsEnabled = false;
                            DTCSINo[2].Visibility = Visibility.Collapsed;
                            L_DPSDTCSINo2.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            Slot2CheckMst.IsEnabled = true;
                            Slot2CheckSel.IsEnabled = true;
                            Slot2CheckSlv.IsEnabled = true;
                        }

                        break;
                    case 3:
                        Slot3Ellipse.Fill = new SolidColorBrush(slotColor);
                        if (slotColor == Colors.ForestGreen)
                        {
                            Slot3CheckMst.IsChecked = false;
                            Slot3CheckSel.IsChecked = false;
                            Slot3CheckSlv.IsChecked = false;
                            Slot3CheckMst.IsEnabled = false;
                            Slot3CheckSel.IsEnabled = false;
                            Slot3CheckSlv.IsEnabled = false;
                            DTCSINo[3].Visibility = Visibility.Collapsed;
                            L_DPSDTCSINo3.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            Slot3CheckMst.IsEnabled = true;
                            Slot3CheckSel.IsEnabled = true;
                            Slot3CheckSlv.IsEnabled = true;
                        }

                        break;
                    case 4:
                        Slot4Ellipse.Fill = new SolidColorBrush(slotColor);
                        if (slotColor == Colors.ForestGreen)
                        {
                            Slot4CheckMst.IsChecked = false;
                            Slot4CheckSel.IsChecked = false;
                            Slot4CheckSlv.IsChecked = false;
                            Slot4CheckMst.IsEnabled = false;
                            Slot4CheckSel.IsEnabled = false;
                            Slot4CheckSlv.IsEnabled = false;
                            DTCSINo[4].Visibility = Visibility.Collapsed;
                            L_DPSDTCSINo4.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            Slot4CheckMst.IsEnabled = true;
                            Slot4CheckSel.IsEnabled = true;
                            Slot4CheckSlv.IsEnabled = true;
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        void OnCommandChanged(object sender, CommandEventArgs e)
        {
            if (e.commandName != null)
            {
                // Check button names using if-else
                if (e.commandName == "Erase")
                {
                    Erase.Background = new SolidColorBrush(e.commandColor);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Compare_LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Write")
                {
                    Write.Background = new SolidColorBrush(e.commandColor);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Compare_LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Read")
                {
                    Read.Background = new SolidColorBrush(e.commandColor);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    Compare_LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "LoopBack")
                {
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    Compare_LoopBack.Background = new SolidColorBrush(e.commandColor);
                }
                else
                {
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    Compare_LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
            }
        }

        public string UpdateUserStatus(string Msg)
        {
            StatusTextBlock.FontSize = 17;
            StatusTextBlock.Text = PopUpMessagesContainerObj.FindStatusMsgById(Msg);
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            return StatusTextBlock.Text;
        }

        public async Task preCommandExeOper(object sender, int cartNo)
        {
            commandInProgress = true;

            var clickedButton = sender as Button;

            buttonManager.SetButtonColorState(clickedButton, Colors.DodgerBlue);

            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await hwInfo.StopScanningAsync();

            await LedState.DTCLAppCtrlLed();
            await LedState.LedBusySate(cartNo);

            UpdateUserStatus("Exe_Progress_Msg");

            if (hwInfo.BoardId != "DTCL")
            {
                Slot1CheckSel.IsEnabled = false;
                Slot2CheckSel.IsEnabled = false;
                Slot3CheckSel.IsEnabled = false;
                Slot4CheckSel.IsEnabled = false;

                Slot1CheckMst.IsEnabled = false;
                Slot2CheckMst.IsEnabled = false;
                Slot3CheckMst.IsEnabled = false;
                Slot4CheckMst.IsEnabled = false;

                Slot1CheckSlv.IsEnabled = false;
                Slot2CheckSlv.IsEnabled = false;
                Slot3CheckSlv.IsEnabled = false;
                Slot4CheckSlv.IsEnabled = false;
                /*switch(cartNo)
                {
                    case 1:
                        Slot1Ellipse.Fill = new SolidColorBrush(Colors.Red);
                        break;
                    case 2:
                        Slot2Ellipse.Fill = new SolidColorBrush(Colors.Red);
                        break;
                    case 3:
                        Slot3Ellipse.Fill = new SolidColorBrush(Colors.Red);
                        break;
                    case 4:
                        Slot4Ellipse.Fill = new SolidColorBrush(Colors.Red);
                        break;

                }*/
            }
        }

        public async Task postCommandExeOper(object sender, int cartNo)
        {
            if (hwInfo.BoardId != "DTCL")
            {
                switch (cartNo)
                {
                    case 1:
                        Slot1Ellipse.Fill = new SolidColorBrush(Colors.DodgerBlue);
                        break;
                    case 2:
                        Slot2Ellipse.Fill = new SolidColorBrush(Colors.DodgerBlue);
                        break;
                    case 3:
                        Slot3Ellipse.Fill = new SolidColorBrush(Colors.DodgerBlue);
                        break;
                    case 4:
                        Slot4Ellipse.Fill = new SolidColorBrush(Colors.DodgerBlue);
                        break;

                }

                if (((SolidColorBrush)Slot1Ellipse.Fill).Color == Colors.DodgerBlue)
                {
                    Slot1CheckSel.IsEnabled = true;
                    Slot1CheckMst.IsEnabled = true;
                    Slot1CheckSlv.IsEnabled = true;
                }

                if (((SolidColorBrush)Slot2Ellipse.Fill).Color == Colors.DodgerBlue)
                {
                    Slot2CheckSel.IsEnabled = true;
                    Slot2CheckMst.IsEnabled = true;
                    Slot2CheckSlv.IsEnabled = true;
                }

                if (((SolidColorBrush)Slot3Ellipse.Fill).Color == Colors.DodgerBlue)
                {
                    Slot3CheckSel.IsEnabled = true;
                    Slot3CheckMst.IsEnabled = true;
                    Slot3CheckSlv.IsEnabled = true;
                }

                if (((SolidColorBrush)Slot4Ellipse.Fill).Color == Colors.DodgerBlue)
                {
                    Slot4CheckSel.IsEnabled = true;
                    Slot4CheckMst.IsEnabled = true;
                    Slot4CheckSlv.IsEnabled = true;
                }
            }

            commandInProgress = false;
            // LedState.LedIdleSate();

            // LedState.RedLedOff();
            // LedState.GreenLedOn();
            var res = await LedState.FirmwareCtrlLed();
            await LedState.LedIdleSate(cartNo);

            OperationProgressBar.Value = 0;
            UpdateUserStatus("Idle_Msg");
            buttonManager.ResetButtonColorStates(defaultColor);
            await Task.Delay(1);
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            hwInfo.StartScanning();
        }

        async void PerformanceCheck_Click(object sender, RoutedEventArgs e)
        {
            if (hwInfo.IsConnected == false)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                return;
            }

            isLogSelected = false;
            isLogTypeSelected = false;
            isPcStarted = false;
            stopPcFlag = false;
            isPCMode = true;

            Log.Log.Info($"PCMode: {isPCMode}");

            var clickedButton = sender as Button;
            buttonManager.SetButtonColorState(clickedButton, Colors.DodgerBlue);

            buttonManager.ShowOnlyButtons(new List<Button> { Exit, PerformanceCheck });

            SetPeformanceCheckBlockVisibility(Visibility.Visible);
            ConfirmLog.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;
            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = false;
            EnableDtcSIFields();
        }

        async void OldLog_Click(object sender, RoutedEventArgs e)
        {
            await hwInfo.StopScanningAsync();
            withCart.IsEnabled = true;
            withOutCart.IsEnabled = true;

            OldLog.Background = new SolidColorBrush(Colors.DodgerBlue);
            NewLog.Background = new SolidColorBrush(defaultColor);

            isLogTypeSelected = true;
            // PCLog.Instance.SetCartType(hwInfo.detectedSlotInfo.cartType);
            // PCLog.Instance.SetSlotInfo(hwInfo.SlotInfo);
            PCLog.Instance.LogType = "Old";

            enableLogEntries();

            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;

            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Old_Log_Enter_Msg"), this);
            hwInfo.StartScanning();
        }

        async void NewLog_Click(object sender, RoutedEventArgs e)
        {
            await hwInfo.StopScanningAsync();
            withCart.IsEnabled = true;
            withOutCart.IsEnabled = true;

            NewLog.Background = new SolidColorBrush(Colors.DodgerBlue);
            OldLog.Background = new SolidColorBrush(defaultColor);

            isLogTypeSelected = true;
            // PCLog.Instance.SetCartType(hwInfo.detectedSlotInfo.cartType);
            // PCLog.Instance.SetSlotInfo(hwInfo.SlotInfo);

            PCLog.Instance.LogType = "New";

            enableLogEntries();

            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;

            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("New_Log_Enter_Msg"), this);
            hwInfo.StartScanning();
        }

        async void ConfirmLog_Click(object sender, RoutedEventArgs e)
        {
            var hardwareInfo = HardwareInfo.Instance;

            // Stop scanning during performance check log confirmation
            await hardwareInfo.StopScanningAsync();

            try
            {
                // Validate hardware connection
                if (!hardwareInfo.IsConnected)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Hardware_Not_Connected_Msg"), this);
                    return;
                }

                // Get selected slots from UI
                var selectedSlots = GetSelectedSlots();
                var hasSelection = selectedSlots.Count > 0;

                // Handle with cartridge mode
                if (withCart.IsChecked == true)
                {
                    if (hasSelection)
                    {
                        await HandleWithCartridgeMode(hardwareInfo, selectedSlots);
                    }
                    else
                    {
                        await HandleNoSelectionWithCartridge(hardwareInfo);
                    }
                }
                // Handle without cartridge mode
                else if (withOutCart.IsChecked == true)
                {
                    if (hasSelection)
                    {
                        await HandleWithoutCartridgeMode(hardwareInfo, selectedSlots);
                    }
                    else
                    {
                        await HandleNoSelectionWithoutCartridge(hardwareInfo);
                    }
                }
            }
            finally
            {
                // Always resume scanning
                hardwareInfo.StartScanning();
            }
        }

        List<int> GetSelectedSlots()
        {
            var selectedSlots = new List<int>();

            // Check which slots are selected in UI
            if (Slot1CheckSel.IsChecked == true) selectedSlots.Add(1);
            if (Slot2CheckSel.IsChecked == true) selectedSlots.Add(2);
            if (Slot3CheckSel.IsChecked == true) selectedSlots.Add(3);
            if (Slot4CheckSel.IsChecked == true) selectedSlots.Add(4);

            return selectedSlots;
        }

        async Task HandleWithCartridgeMode(HardwareInfo hardwareInfo, List<int> selectedSlots)
        {
            foreach (var slotNumber in selectedSlots)
            {
                var slot = hardwareInfo.SlotInfo[slotNumber];

                // Validate cart is detected and operational
                if (!slot.IsCartDetectedAtSlot || slot.DetectedCartTypeAtSlot == CartType.Unknown)
                {
                    Log.Log.Error("Performance check could not start as there is no cart detected");
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Insert_cart_Msg"), this);
                    return;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(TestNumber.Text) || string.IsNullOrEmpty(InspectorName.Text) ||
                    string.IsNullOrEmpty(UnitSINo.Text) || string.IsNullOrEmpty(DTCSINo[slotNumber].Text))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Name"), this);
                    return;
                }

                selectedCartForPC = slot.DetectedCartTypeAtSlot;

                // Setup UI and confirm log
                await SetupPerformanceCheckUI();
                await CreatePerformanceLog(slotNumber, DTCSINo[slotNumber].Text, slot);
                return; // Only process first selected slot
            }
        }

        async Task HandleWithoutCartridgeMode(HardwareInfo hardwareInfo, List<int> selectedSlots)
        {
            foreach (var slotNumber in selectedSlots)
            {
                var slot = hardwareInfo.SlotInfo[slotNumber];

                // Ensure no cart is detected for without-cartridge mode
                if (slot.IsCartDetectedAtSlot)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Please_Remove_Msg"), this);
                    Log.Log.Error($"Performance check could not start as there is detected: {slot.DetectedCartTypeAtSlot}");
                    return;
                }
            }

            // Validate required fields
            if (string.IsNullOrEmpty(TestNumber.Text) || string.IsNullOrEmpty(InspectorName.Text) ||
                string.IsNullOrEmpty(UnitSINo.Text))
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Name"), this);
                return;
            }

            selectedCartForPC = CartType.Unknown;

            // Setup UI and confirm log
            await SetupPerformanceCheckUI();
            await CreatePerformanceLog(selectedSlots[0], DTCSINo[selectedSlots[0]].Text, hardwareInfo.SlotInfo[selectedSlots[0]]);
        }

        async Task HandleNoSelectionWithCartridge(HardwareInfo hardwareInfo)
        {
            // Check if any cart is detected
            var anyCartDetected = false;

            for (int i = 1; i <= hardwareInfo.GetSlotCount(); i++)
            {
                if (hardwareInfo.SlotInfo[i].IsCartDetectedAtSlot)
                {
                    anyCartDetected = true;
                    break;
                }
            }

            // Handle DTCL multi-cart scenario
            if (hardwareInfo.HardwareType == HardwareType.DTCL)
            {
                // Check for multi-cart detection in DTCL
                var detectedCount = 0;

                for (int i = 1; i <= 3; i++)
                    if (hardwareInfo.SlotInfo[i].IsCartDetectedAtSlot) detectedCount++;

                if (detectedCount > 1)
                {
                    CommandsLabel.Visibility = Visibility.Hidden;
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_cart_Msg"), this);
                    UpdateUserStatus("Multi_cart_Msg");
                    return;
                }
            }

            if (!anyCartDetected)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Insert_cart_Msg"), this);
            }
            else
            {
                // Setup UI and confirm log for cartridge mode
                await SetupPerformanceCheckUI();

                for (int i = 1; i <= 3; i++)
                {
                    if (hardwareInfo.SlotInfo[i].IsCartDetectedAtSlot)
                    {
                        await CreatePerformanceLog(i, DTCSINo[i].Text, hardwareInfo.SlotInfo[i]);
                        break;
                    }
                }
            }
        }

        async Task HandleNoSelectionWithoutCartridge(HardwareInfo hardwareInfo)
        {
            // Check if any cart is detected
            var anyCartDetected = false;

            for (int i = 1; i <= hardwareInfo.GetSlotCount(); i++)
            {
                if (hardwareInfo.SlotInfo[i].IsCartDetectedAtSlot)
                {
                    anyCartDetected = true;
                    break;
                }
            }

            if (anyCartDetected)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Please_Remove_Msg"), this);
            }
            else
            {
                // Validate required fields
                if (string.IsNullOrEmpty(TestNumber.Text) || string.IsNullOrEmpty(InspectorName.Text) ||
                    string.IsNullOrEmpty(UnitSINo.Text))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Name"), this);
                    return;
                }

                selectedCartForPC = CartType.Unknown;

                // Setup UI and confirm log for no-cartridge mode
                await SetupPerformanceCheckUI();

                // Create a dummy slot for loopback testing when no slots are selected
                // var dummySlot = new SlotInfo(1); // Use slot 1 as default for loopback without cart

                hwInfo.SlotInfo[0].IsSlotSelected_ByUser = true;
                hwInfo.SlotInfo[0].DetectedCartTypeAtSlot = CartType.Unknown;
                await CreatePerformanceLog(1, "", hwInfo.SlotInfo[0]); // Use dummy slot instead of null
            }
        }

        async Task SetupPerformanceCheckUI()
        {
            // Validate log type selection
            if (!isLogTypeSelected)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Sel_Log_Type_Msg"), this);
                ConfirmLog.Background = new SolidColorBrush(defaultColor);
                return;
            }

            // Setup UI state
            ConfirmLog.Background = new SolidColorBrush(Colors.DodgerBlue);
            InitiatePC.IsEnabled = true;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = true;
            DurationMin.IsEnabled = true;
            DurationSec.IsEnabled = true;
            IterationSel.IsEnabled = true;
            DurationSel.IsEnabled = true;
            withCart.IsEnabled = false;
            withOutCart.IsEnabled = false;

            isLogSelected = true;
            IstRun = true;
            await Task.Delay(1);
        }

        async Task CreatePerformanceLog(int slotNumber, string dtcSerialNumber, SlotInfo slotInfo)
        {
            if (PCLog.Instance.LogType == "Old")
            {
                PCLog.Instance
                    .AppendToOldLog(TestNumber.Text, InspectorName.Text, dtcSerialNumber, UnitSINo.Text, withCart.IsChecked ?? false, slotInfo);

                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Old_Log_Sel_Msg"), this);
                OldLog.Background = new SolidColorBrush(defaultColor);
            }
            else
            {
                PCLog.Instance
                    .CreateNewLog(TestNumber.Text, InspectorName.Text, dtcSerialNumber, UnitSINo.Text, withCart.IsChecked ?? false, slotInfo);

                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("New_Log_Msg2"), this);
                NewLog.Background = new SolidColorBrush(defaultColor);
            }

            disableLogEntries();
            ConfirmLog.Background = new SolidColorBrush(defaultColor);
        }

        public void disableLogEntries()
        {
            if ("DTCL" == hwInfo.BoardId)
            {
                UnitSINo.IsEnabled = false;
                DTCSINo[1].IsEnabled = false;
                TestNumber.IsEnabled = false;
                InspectorName.IsEnabled = false;
                ConfirmLog.IsEnabled = false;
            }
            else
            {
                DPSUnitSINo.IsEnabled = false;
                DPSDTCSINo1.IsEnabled = false;
                DPSDTCSINo2.IsEnabled = false;
                DPSDTCSINo3.IsEnabled = false;
                DPSDTCSINo4.IsEnabled = false;
                DPSTestNumber.IsEnabled = false;
                DPSInspectorName.IsEnabled = false;
                DPSConfirmLog.IsEnabled = false;
            }
        }

        public void enableLogEntries()
        {
            if ("DTCL" == hwInfo.BoardId)
            {
                UnitSINo.IsEnabled = true;

                if (withCart.IsChecked == true)
                    DTCSINo[1].IsEnabled = true;
                else
                    DTCSINo[1].IsEnabled = false;

                TestNumber.IsEnabled = true;
                InspectorName.IsEnabled = true;
                ConfirmLog.IsEnabled = true;
            }
            else
            {
                DPSUnitSINo.IsEnabled = true;

                if (DPSwithCart.IsChecked == true)
                {
                    DPSDTCSINo1.IsEnabled = true;
                    DPSDTCSINo2.IsEnabled = true;
                    DPSDTCSINo3.IsEnabled = true;
                    DPSDTCSINo4.IsEnabled = true;
                }
                else
                {
                    DPSDTCSINo1.IsEnabled = false;
                    DPSDTCSINo2.IsEnabled = false;
                    DPSDTCSINo3.IsEnabled = false;
                    DPSDTCSINo4.IsEnabled = false;
                }

                DPSTestNumber.IsEnabled = true;
                DPSInspectorName.IsEnabled = true;
                DPSConfirmLog.IsEnabled = true;
            }
        }

        async void InitiatePC_Click(object sender, RoutedEventArgs e)
        {
            await hwInfo.StopScanningAsync();
            LedState.LedStateChanged -= OnLedStateChanged;

            if (withOutCart.IsChecked == true)
            {
            }
            else
            {
                if (!FileOperations.IsFileExist(hwInfo.CartUploadFilePath + @"DR.bin"))
                {
                    FileOperations.Copy(hwInfo.CartUploadFilePath + @"DR.bin", hwInfo.CartUploadFilePath + @"DR.bin");
                }
            }

            DisableEnableSelectionChecks(false);

            var selectedSlots = hwInfo.SlotInfo
                                                     .Where(slot => slot != null && slot.IsSlotSelected_ByUser)
                                                     .ToList();

            if ((!selectedSlots.Any()) && withCart.IsChecked == true)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("No_Slot_Selected"), this);
                DisableEnableSelectionChecks(true);
                return;
            }

            // For without cart mode with no slots selected, create a dummy slot for loopback test
            if ((!selectedSlots.Any()) && withOutCart.IsChecked == true)
            {
                // Create a dummy slot for loopback testing when no slots are selected
                // var dummySlot = new SlotInfo(1); // Use slot 1 as default for loopback
                // selectedSlots.Add(dummySlot);
                // selectedSlots = hwInfo.SlotInfo
                //                                      .Where(slot => slot != null && slot.IsSlotSelected_ByUser)
                //                                     .ToList();
            }

            UpdateUserStatus("Exe_Progress_Msg");
            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Start_Msg"), this);

            var iterationMode = IterationSel.IsChecked == true;
            var iterationCount = 0;
            var PCDurationTime = 0;

            if (withCart.IsChecked == true)
            {
                foreach (var slotInfo in selectedSlots)
                {
                    // Determine iteration or duration mode
                    if (IterationSel.IsChecked == true)
                    {
                        if (!int.TryParse(IterationCount.Text, out iterationCount) || iterationCount <= 0)
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Iter"), this);
                            postPCCommandExeOper(sender);
                            return;
                        }

                        PCProgressBar.Maximum = iterationCount;

                        if (IstRun == false)
                            PCLog.Instance.AddIterationDuration(iterationCount, 0, slotInfo);
                    }
                    else
                    {
                        int minutes, seconds = 0;

                        if (!int.TryParse(DurationMin.Text, out minutes) || !int.TryParse(DurationSec.Text, out seconds) ||
                        minutes < 0 || seconds < 0 || (minutes == 0 && seconds == 0))
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Duration"), this);
                            postPCCommandExeOper(sender);
                            return;
                        }

                        PCDurationTime = (minutes * 60) + seconds;
                        PCProgressBar.Maximum = PCDurationTime;

                        if (IstRun == false)
                            PCLog.Instance.AddIterationDuration(0, PCDurationTime, slotInfo);
                    }
                }
            }
            else
            {
                // Without cart mode - handle iteration and duration settings
                if (IterationSel.IsChecked == true)
                {
                    if (!int.TryParse(IterationCount.Text, out iterationCount) || iterationCount <= 0)
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Iter"), this);
                        postPCCommandExeOper(sender);
                        return;
                    }

                    PCProgressBar.Maximum = iterationCount;

                    // For loopback without cart, log using the first/dummy slot
                    if (selectedSlots.Any() && IstRun == false)
                    {
                        PCLog.Instance.AddIterationDuration(iterationCount, 0, selectedSlots[0]);
                    }
                }
                else
                {
                    int minutes, seconds = 0;

                    if (!int.TryParse(DurationMin.Text, out minutes) || !int.TryParse(DurationSec.Text, out seconds) ||
                    minutes < 0 || seconds < 0 || (minutes == 0 && seconds == 0))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Duration"), this);
                        postPCCommandExeOper(sender);
                        return;
                    }

                    PCDurationTime = (minutes * 60) + seconds;
                    PCProgressBar.Maximum = PCDurationTime;

                    // For loopback without cart, log using the first/dummy slot
                    if (selectedSlots.Any() && IstRun == false)
                    {
                        PCLog.Instance.AddIterationDuration(0, PCDurationTime, selectedSlots[0]);
                    }
                }
            }

            // Disable all controls except Stop button
            StopPc.IsEnabled = true;
            withCart.IsEnabled = false;
            withOutCart.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;
            IterationSel.IsEnabled = false;
            DurationSel.IsEnabled = false;

            if (!isLogSelected)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Sel_Log_Type_Msg2"), this);
                DisableEnableSelectionChecks(true);
                return;
            }

            var currentIteration = 0;
            var startTime = DateTime.Now;
            // bool stopFlag = false;

            if (await prePCCommandExeOper(sender) != true)
            {
                postPCCommandExeOper(sender);
                return;
            }

            foreach (var slotInfo in selectedSlots)
            {
                // Log initialization
                if (IstRun == true)
                {
                    PCLog.Instance.EditLogHeaderDateTime(slotInfo);

                    if (IterationSel.IsChecked == true)
                        PCLog.Instance.EditIterationDurationType(iterationCount, 0, slotInfo);
                    else
                        PCLog.Instance.EditIterationDurationType(0, PCDurationTime, slotInfo);

                    IstRun = false;
                }
            }

            while (!stopPcFlag && ((iterationMode && currentIteration < iterationCount) ||
                                 (!iterationMode && PCDurationTime > 0)))
            {
                foreach (var slotInfo in selectedSlots)
                {
                    if (!stopPcFlag)
                    {
                        isPcStarted = true;
                        await LedState.LedBusySate(slotInfo.SlotNumber);

                        // Cart validation and button visibility using original logic
                        if (withCart.IsChecked == true)
                        {
                            CommandsLabel.Visibility = Visibility.Visible;
                            buttonManager.ShowOrHideOnlyListButtons(new List<Button> { PerformanceCheck, Exit, Write, Erase, Read, Compare_LoopBack }, true);
                        }
                        else
                        {
                            CommandsLabel.Visibility = Visibility.Hidden;
                            buttonManager.ShowOrHideOnlyListButtons(new List<Button> { Exit, PerformanceCheck, Compare_LoopBack }, true);
                            buttonManager.ShowOrHideOnlyListButtons(new List<Button> { Write, Erase, Read }, false);
                        }

                        var res = await startPC(sender, e, slotInfo, currentIteration);
                        await LedState.LedIdleSate(slotInfo.SlotNumber);

                        PCProgressBar.Value = iterationMode ? currentIteration : (DateTime.Now - startTime).TotalSeconds;

                        // Calculate total elapsed time
                        var totalElapsedTime = DateTime.Now - startTime;
                        var elapsedSeconds = (int)totalElapsedTime.TotalSeconds;

                        // Update the duration if in time-based mode
                        if (IterationSel.IsChecked == false)
                        {
                            PCDurationTime = (int)Math.Max(PCProgressBar.Maximum - elapsedSeconds, 0);
                        }

                        TimeElapsed.Text = ((int)(DateTime.Now - startTime).TotalSeconds).ToString();
                    }
                }

                if (selectedSlots.Count == 0)
                {
                    var res = await startPC(sender, e, null, currentIteration);
                }

                currentIteration++;
                CurrentIteration.Text = currentIteration.ToString();
                isPcStarted = false;
            }

            // Cleanup for each slot after all iterations
            foreach (var slotInfo in selectedSlots)
            {
                if (slotInfo.DetectedCartTypeAtSlot != CartType.Unknown)
                {
                    if (hwInfo.CartObj != null)
                        await hwInfo.CartObj.EraseCartPCFiles(null, (byte)slotInfo.SlotNumber);
                }

                // Post-performance check messages
                if (stopPcFlag)
                {
                    if (hwInfo.CartObj != null)
                        await hwInfo.CartObj.EraseCartPCFiles(null, (byte)slotInfo.SlotNumber);

                    PCLog.Instance.AddEntry("Performance Check Stopped", slotInfo);
                }
                else
                {
                    if (hwInfo.CartObj != null)
                        await hwInfo.CartObj.EraseCartPCFiles(null, (byte)slotInfo.SlotNumber);

                    PCProgressBar.Value = PCProgressBar.Maximum;
                    PCLog.Instance.AddEntry("Performance Check Completed", slotInfo);
                }
            }

            if (stopPcFlag)
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Stopped_Msg2"), this);
            else
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Completed_Msg"), this);

            // Re-enable everything back after completion
            StopPc.IsEnabled = false;
            withCart.IsEnabled = true;
            withOutCart.IsEnabled = true;
            IterationCount.IsEnabled = true;
            DurationMin.IsEnabled = true;
            DurationSec.IsEnabled = true;
            IterationSel.IsEnabled = true;
            DurationSel.IsEnabled = true;

            // Hide command buttons again
            buttonManager.ShowOrHideOnlyListButtons(new List<Button> { Compare_LoopBack, Read, Write, Erase }, false);

            IterationCount.Text = "";
            DurationMin.Text = "";
            DurationSec.Text = "";

            postPCCommandExeOper(sender);

            hwInfo.StartScanning();
        }

        async Task<bool> startPC(object sender, RoutedEventArgs e, SlotInfo slotInfo, int iterationNo)
        {
            var PC_Obj = new PerformanceCheck();
            PCResult result;

            disableLogEntries();
            try
            {
                if ((hwInfo.IsConnected) && ((slotInfo.DetectedCartTypeAtSlot != CartType.Unknown) || !(withCart.IsChecked ?? true)))
                {
                    isPcStarted = true;

                    if (hwInfo.CartObj == null)
                    {
                        PerformanceCheck PC;
                        PC = new PerformanceCheck();
                        Compare_LoopBack.Background = new SolidColorBrush(Colors.DodgerBlue);
                        result = await PC.doLoopBackTest(1);

                        PC = null;
                    }
                    else
                    {
                        hwInfo.CartObj.CommandInProgress += OnCommandChanged; // Track command progress

                        result = await hwInfo.CartObj.ExecutePC(withCart.IsChecked ?? false, slotInfo.DetectedCartTypeAtSlot, (byte)slotInfo.SlotNumber);
                    }

                    await Task.Delay(2);

                    PCLog.Instance.AddPerformanceResponse(withCart.IsChecked ?? false, result, iterationNo + 1, slotInfo);
                }
                else
                {
                    Log.Log.Error("DTCL Connection Lost During Performance check");

                    result = new PCResult
                    {
                        loopBackResult = "FAIL",
                        readResult = "FAIL",
                        writeResult = "FAIL",
                        eraseResult = "FAIL"
                    };

                    PCLog.Instance.AddPerformanceResponse(withCart.IsChecked ?? false, result, iterationNo + 1, slotInfo);

                    await Task.Delay(1000);

                    if (!isPcStartScan)
                        hwInfo.StartScanning();
                }

                PCResultDisplay.Text = result.loopBackResult.Equals("PASS") && result.readResult.Equals("PASS") &&
                                                    result.writeResult.Equals("PASS") && result.eraseResult.Equals("PASS") ? "PASS" : "FAIL";
            }
            finally
            {
                if (hwInfo.CartObj != null)
                    hwInfo.CartObj.CommandInProgress -= OnCommandChanged; // Remove handler after execution
            }

            return true;
        }

        public async void UpdatePCProgress(int iterationCount, int PCDurationTime, int elapsedTime, int counter, PCResult result)
        {
            // Set the result display
            if (result.eraseResult.Equals("PASS") && result.writeResult.Equals("PASS") && result.readResult.Equals("PASS") && result.loopBackResult.Equals("PASS"))
            {
                PCResultDisplay.Text = "PASS";
            }
            else
            {
                PCResultDisplay.Text = "FAIL";
            }

            // Update the progress bar
            if (IterationSel.IsChecked == true)
            {
                PCProgressBar.Value = PCProgressBar.Maximum - iterationCount;
                TimeElapsed.Text = $"{(int)(elapsedTime)}";
            }
            else
            {
                PCProgressBar.Value = PCProgressBar.Maximum - PCDurationTime;
                TimeElapsed.Text = $"{(int)(PCProgressBar.Maximum - PCDurationTime)}";
            }

            // Update both iteration and elapsed time
            CurrentIteration.Text = counter.ToString();

            // Refresh the UI
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await Task.Delay(1);
        }

        void StopPc_Click(object sender, RoutedEventArgs e)
        {
            StopPc.Background = new SolidColorBrush(Colors.DodgerBlue);

            if (isPcStarted == true)
            {
                stopPcFlag = true;
                isPcStarted = false;
                // CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Stopped_Msg2"), this);
                StopPc.IsEnabled = false;
                InitiatePC.IsEnabled = false;
                NewLog.IsEnabled = false;
                OldLog.IsEnabled = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;
            }
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Start_Pc_Fail_Msg"), this);
            }

            StopPc.Background = new SolidColorBrush(defaultColor);
        }

        public async Task<bool> prePCCommandExeOper(object sender)
        {
            commandInProgress = true;

            await hwInfo.StopScanningAsync();

            await LedState.DTCLAppCtrlLed();

            // await LedState.LedBusySate(slotInfo.SlotNumber);

            var clickedButton = sender as Button;

            Compare_LoopBack.Name = "LoopBack";
            Compare_LoopBack.Content = "LOOPBACK";

            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            buttonManager.SetOnlyButtonColorState(clickedButton, Colors.DodgerBlue);
            NewLog.IsEnabled = false;
            OldLog.IsEnabled = false;
            ConfirmLog.IsEnabled = false;
            ClosePC.IsEnabled = false;

            UpdateUserStatus("Exe_Progress_Msg");

            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            while ((hwInfo.CartObj == null) && (withCart.IsChecked == true))
                await Task.Delay(10);

            return true;
        }

        public async void postPCCommandExeOper(object sender)
        {
            commandInProgress = false;
            stopPcFlag = false;
            isPcStarted = false;
            var clickedButton = sender as Button;

            buttonManager.ResetOnlyButtonColorState(clickedButton, defaultColor);

            NewLog.IsEnabled = true;
            OldLog.IsEnabled = true;
            ClosePC.IsEnabled = true;

            InitiatePC.IsEnabled = true;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = true;
            DurationMin.IsEnabled = true;
            DurationSec.IsEnabled = true;
            ConfirmLog.IsEnabled = false;
            IterationSel.IsEnabled = true;
            DurationSel.IsEnabled = true;
            TimeElapsed.Text = "";
            CurrentIteration.Text = "";
            DurationMin.Text = "0";
            IterationCount.Text = "1";
            DurationSec.Text = "10";
            disableLogEntries();

            PCResultDisplay.Text = "";

            PCProgressBar.Value = 0;
            UpdateUserStatus("Idle_Msg");
            await Task.Delay(1);
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await LedState.FirmwareCtrlLed();
        }

        void AppButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = false;

            if (!hwInfo.IsConnected)
            {
                if (hwInfo.BoardId == "DTCL")
                {
                    UpdateUserStatus("DTCL_Not_Detected_Msg");
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                    return;
                }
                else
                {
                    UpdateUserStatus("DTCL_Not_Detected_Msg");
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DTCL_Not_Detected_Msg"), this);
                    return;
                }
            }

            Compare_LoopBack.Name = "Compare";
            Compare_LoopBack.Content = "COMPARE";

            for (int itr = 1; itr <= hwInfo.GetSlotCount(); itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser)
                {
                    if ((hwInfo.SlotInfo[itr].DetectedCartTypeAtSlot != CartType.Unknown) && (hwInfo.SlotInfo[itr].DetectedCartTypeAtSlot != CartType.MultiCart))
                    {
                        if (hwInfo.SlotInfo[itr].DetectedCartTypeAtSlot != CartType.Darin3)
                            buttonManager.ShowAllButtonsExcept("Utility", "PerformanceCheck", "Format");
                        else
                            buttonManager.ShowAllButtonsExcept("Utility", "PerformanceCheck");

                        AppButton.Visibility = Visibility.Collapsed;
                        CommandsLabel.Visibility = Visibility.Visible;
                        UpdateUserStatus("Idle_Msg");
                        sel = true;
                        break;
                    }
                }
            }

            if (hwInfo.BoardId == "DTCL")
            {
                if ((sel == false) && (hwInfo.DetectedCartTypeAtHw == CartType.MultiCart))
                {
                    CommandsLabel.Visibility = Visibility.Hidden;
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_cart_Msg"), this);
                    UpdateUserStatus("Multi_cart_Msg");
                }
                else if ((hwInfo.DetectedCartTypeAtHw == CartType.Unknown))
                {
                    CommandsLabel.Visibility = Visibility.Hidden;
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Insert_cart_Msg"), this);
                    UpdateUserStatus("Insert_cart_Msg");
                }
            }
            else
            {
                if ((sel == false) && (hwInfo.DetectedCartTypeAtHw != CartType.Unknown))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("DPS_Sel_Slot_Msg"), this);
                }
                else if ((hwInfo.DetectedCartTypeAtHw == CartType.Unknown))
                {
                    CommandsLabel.Visibility = Visibility.Hidden;
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Insert_cart_Msg"), this);
                    UpdateUserStatus("Insert_cart_Msg");
                }
            }
        }

        void Close_Click(object sender, RoutedEventArgs e)
        {
            buttonManager.ShowOnlyExitAtStart();
            LoopBack.Visibility = Visibility.Visible;
            Close.Visibility = Visibility.Collapsed;
            CommandsLabel.Visibility = Visibility.Hidden;
            AppButton.Visibility = Visibility.Visible;
            CollapseAllDtcSIFields();
        }

        async void ClosePC_Click(object sender, RoutedEventArgs e)
        {
            CommandsLabel.Visibility = Visibility.Hidden;
            NewLog.Background = new SolidColorBrush(defaultColor);
            OldLog.Background = new SolidColorBrush(defaultColor);
            withCart.IsEnabled = true;
            withOutCart.IsEnabled = true;
            isLogSelected = false;
            isLogTypeSelected = false;
            isPcStarted = false;
            stopPcFlag = false;
            isPCMode = false;
            IstRun = false;

            Compare_LoopBack.Name = "Compare";
            Compare_LoopBack.Content = "COMPARE";

            Log.Log.Info($"PCMode: {isPCMode}");

            SetPeformanceCheckBlockVisibility(Visibility.Collapsed);
            buttonManager.ShowOnlyExitAtStart();

            for (int itr = 1; itr < 5; itr++)
                await postCommandExeOper(sender, hwInfo.SlotInfo[itr].SlotNumber);

            LoopBack.Visibility = Visibility.Visible;
            AppButton.Visibility = Visibility.Visible;
            hwInfo.SlotInfo[0].IsSlotSelected_ByUser = false;
            LedState.LedStateChanged += OnLedStateChanged;
        }

        public void DisplayMultiUnit()
        {
        }

        public Button GetButtonByName(string name)
        {
            var myButton = (Button)FindName(name);

            return myButton;
        }

        async void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                await LedState.FirmwareCtrlLed();
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error during LED cleanup: {ex.Message}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        async void Logo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var data = await LedState.GetVersionNumber();

            if (data == null)
            {
                MessageBox.Show($"S-WAVE Internal DPS_DTCL Version Number {GUI_VERSION} ");
            }
            else
            {
                var version = System.Text.Encoding.ASCII.GetString(data);
                MessageBox.Show($"S-WAVE Internal DPS_DTCL Version Number {GUI_VERSION} and Firmware Version is {version}");
            }
        }

        async void LoopBack_Click(object sender, RoutedEventArgs e)
        {
            Log.Log.Info("Starting LoopBack Test");

            commandInProgress = true;

            var clickedButton = sender as Button;

            buttonManager.SetButtonColorState(clickedButton, Colors.DodgerBlue);

            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await hwInfo.StopScanningAsync();
            var res = await LedState.DTCLAppCtrlLed();

            if (await LedState.LoopBackTestAll() == false)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("LoopBack_Fail"), this);
            }
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("LoopBack_Pass"), this);
            }

            commandInProgress = false;

            res = await LedState.FirmwareCtrlLed();

            OperationProgressBar.Value = 0;
            buttonManager.ResetButtonColorStates(defaultColor);
            await Task.Delay(1);
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            hwInfo.StartScanning();
        }

        void MasterCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is SlotInfo slot)
            {
                try
                {
                    // Rule 1: Can't be both Master and Slave
                    if (slot.IsSlotRole_ByUser == SlotRole.Slave)
                        throw new InvalidOperationException($"Slot {slot.SlotNumber} is already marked as Slave and cannot be Master.");

                    // Rule 2: Only one master allowed
                    for (int itr = 1; itr < 5; itr++)
                    {
                        if (hwInfo.SlotInfo[itr] != null && hwInfo.SlotInfo[itr].IsSlotRole_ByUser == SlotRole.Master && hwInfo.SlotInfo[itr] != slot)
                            throw new InvalidOperationException($"Slot {hwInfo.SlotInfo[itr].SlotNumber} is already marked as Master. Only one master is allowed.");
                    }

                    slot.IsSlotRole_ByUser = SlotRole.Master;
                    // Update HardwareInfo with new role
                    hwInfo.SetSlotRole(slot.SlotNumber, SlotRole.Master);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Error);
                    cb.IsChecked = false;
                }
            }
        }

        void MasterCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is SlotInfo slot)
            {
                slot.IsSlotRole_ByUser = SlotRole.None;
                // Update HardwareInfo with new role
                hwInfo.SetSlotRole(slot.SlotNumber, SlotRole.None);
            }
        }

        void SlaveCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is SlotInfo slot)
            {
                try
                {
                    if (slot.IsSlotRole_ByUser == SlotRole.Master)
                        throw new InvalidOperationException($"Slot {slot.SlotNumber} is already marked as Master and cannot be Slave.");

                    slot.IsSlotRole_ByUser = SlotRole.Slave;
                    // Update HardwareInfo with new role
                    hwInfo.SetSlotRole(slot.SlotNumber, SlotRole.Slave);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Error);
                    cb.IsChecked = false;
                }
            }
        }

        void SlaveCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is SlotInfo slot)
            {
                slot.IsSlotRole_ByUser = SlotRole.None;
                // Update HardwareInfo with new role
                hwInfo.SetSlotRole(slot.SlotNumber, SlotRole.None);
            }
        }

        void Slot2CheckSel_Click(object sender, RoutedEventArgs e)
        {
            if (Slot2CheckSel.IsChecked == true)
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo2.Visibility = Visibility.Visible;
                    L_DPSDTCSINo2.Visibility = Visibility.Visible;
                }

                hwInfo.SetSlotRole(2, SlotRole.Slave);
            }
            else
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo2.Visibility = Visibility.Collapsed;
                    L_DPSDTCSINo2.Visibility = Visibility.Collapsed;
                    hwInfo.SetSlotRole(2, SlotRole.None);
                }
            }

            if (Slot2CheckSel.IsChecked == true && withOutCart.IsChecked == true)
            {
                DPSDTCSINo2.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo2.Visibility = Visibility.Collapsed;
            }
        }

        void Slot1CheckSel_Click(object sender, RoutedEventArgs e)
        {
            if (Slot1CheckSel.IsChecked == true)
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo1.Visibility = Visibility.Visible;
                    L_DPSDTCSINo1.Visibility = Visibility.Visible;
                }

                hwInfo.SetSlotRole(1, SlotRole.Slave);
            }
            else
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo1.Visibility = Visibility.Collapsed;
                    L_DPSDTCSINo1.Visibility = Visibility.Collapsed;
                }

                hwInfo.SetSlotRole(1, SlotRole.None);
            }

            if (Slot1CheckSel.IsChecked == true && withOutCart.IsChecked == true)
            {
                DPSDTCSINo1.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo1.Visibility = Visibility.Collapsed;
            }
        }

        void Slot3CheckSel_Click(object sender, RoutedEventArgs e)
        {
            if (Slot3CheckSel.IsChecked == true)
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo3.Visibility = Visibility.Visible;
                    L_DPSDTCSINo3.Visibility = Visibility.Visible;
                }

                hwInfo.SetSlotRole(3, SlotRole.Slave);
            }
            else
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo3.Visibility = Visibility.Collapsed;
                    L_DPSDTCSINo3.Visibility = Visibility.Collapsed;
                }

                hwInfo.SetSlotRole(3, SlotRole.None);
            }

            if (Slot3CheckSel.IsChecked == true && withOutCart.IsChecked == true)
            {
                DPSDTCSINo3.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo3.Visibility = Visibility.Collapsed;
            }
        }

        void Slot4CheckSel_Click(object sender, RoutedEventArgs e)
        {
            if (Slot4CheckSel.IsChecked == true)
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo4.Visibility = Visibility.Visible;
                    L_DPSDTCSINo4.Visibility = Visibility.Visible;
                }

                hwInfo.SetSlotRole(4, SlotRole.Slave);
            }
            else
            {
                if (hwInfo.BoardId != "DTCL")
                {
                    DPSDTCSINo4.Visibility = Visibility.Collapsed;
                    L_DPSDTCSINo4.Visibility = Visibility.Collapsed;
                }

                hwInfo.SetSlotRole(4, SlotRole.None);
            }

            if (Slot4CheckSel.IsChecked == true && withOutCart.IsChecked == true)
            {
                DPSDTCSINo4.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo4.Visibility = Visibility.Collapsed;
            }
        }

        void CollapseAllDtcSIFields()
        {
            if (hwInfo.BoardId != "DTCL")
            {
                DPSDTCSINo1.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo1.Visibility = Visibility.Collapsed;
                DPSDTCSINo2.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo2.Visibility = Visibility.Collapsed;
                DPSDTCSINo3.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo3.Visibility = Visibility.Collapsed;
                DPSDTCSINo4.Visibility = Visibility.Collapsed;
                L_DPSDTCSINo4.Visibility = Visibility.Collapsed;
            }
            else
            {
                DTCLDTCSINo.Visibility = Visibility.Collapsed;
                L_DTCLDTCSINo.Visibility = Visibility.Collapsed;
            }
        }

        void CollapseMasterSlave(Visibility visibility)
        {
            Slot1CheckMst.Visibility = visibility;
            Slot2CheckMst.Visibility = visibility;
            Slot3CheckMst.Visibility = visibility;
            Slot4CheckMst.Visibility = visibility;

            Slot1CheckSlv.Visibility = visibility;
            Slot2CheckSlv.Visibility = visibility;
            Slot3CheckSlv.Visibility = visibility;
            Slot4CheckSlv.Visibility = visibility;
            masterLabel.Visibility = visibility;
            slaveLabel.Visibility = visibility;

            if (Visibility.Collapsed == visibility)
            {
                Slot1CheckMst.IsChecked = false;
                Slot2CheckMst.IsChecked = false;
                Slot3CheckMst.IsChecked = false;
                Slot4CheckMst.IsChecked = false;

                Slot1CheckSlv.IsChecked = false;
                Slot2CheckSlv.IsChecked = false;
                Slot3CheckSlv.IsChecked = false;
                Slot4CheckSlv.IsChecked = false;
            }
        }

        void EnableDtcSIFields()
        {
            if (hwInfo.BoardId != "DTCL")
            {
                if ((Slot1CheckSel.IsChecked == true) && (withCart.IsChecked == true))
                {
                    DTCSINo[1].Visibility = Visibility.Visible;
                    L_DTCSINo[1].Visibility = Visibility.Visible;
                }
                else
                {
                    DTCSINo[1].Visibility = Visibility.Collapsed;
                    L_DTCSINo[1].Visibility = Visibility.Collapsed;
                }

                if (Slot2CheckSel.IsChecked == true && (withCart.IsChecked == true))
                {
                    DTCSINo[2].Visibility = Visibility.Visible;
                    L_DTCSINo[2].Visibility = Visibility.Visible;
                }
                else
                {
                    DTCSINo[2].Visibility = Visibility.Collapsed;
                    L_DTCSINo[2].Visibility = Visibility.Collapsed;
                }

                if (Slot3CheckSel.IsChecked == true && (withCart.IsChecked == true))
                {
                    DTCSINo[3].Visibility = Visibility.Visible;
                    L_DTCSINo[3].Visibility = Visibility.Visible;
                }
                else
                {
                    DTCSINo[3].Visibility = Visibility.Collapsed;
                    L_DTCSINo[3].Visibility = Visibility.Collapsed;
                }

                if (Slot4CheckSel.IsChecked == true && (withCart.IsChecked == true))
                {
                    DTCSINo[4].Visibility = Visibility.Visible;
                    L_DTCSINo[4].Visibility = Visibility.Visible;
                }
                else
                {
                    DTCSINo[4].Visibility = Visibility.Collapsed;
                    L_DTCSINo[4].Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                foreach (var slotInfo in hwInfo.SlotInfo)
                {
                    if (slotInfo == null)
                        continue;

                    if ((slotInfo.IsSlotSelected_ByUser == true) && (withCart.IsChecked == true))
                    {
                        DTCLDTCSINo.Visibility = Visibility.Visible;
                        L_DTCLDTCSINo.Visibility = Visibility.Visible;
                        break;
                    }
                    else
                    {
                        DTCLDTCSINo.Visibility = Visibility.Collapsed;
                        L_DTCLDTCSINo.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        void DisableEnableSelectionChecks(bool stat)
        {
            if (Slot1CheckSel.IsChecked == true)
            {
                Slot1CheckSel.IsEnabled = stat;
                Slot1CheckMst.IsEnabled = stat;
                Slot1CheckSlv.IsEnabled = stat;
            }

            if (Slot2CheckSel.IsChecked == true)
            {
                Slot2CheckSel.IsEnabled = stat;
                Slot2CheckMst.IsEnabled = stat;
                Slot2CheckSlv.IsEnabled = stat;
            }

            if (Slot3CheckSel.IsChecked == true)
            {
                Slot3CheckSel.IsEnabled = stat;
                Slot3CheckMst.IsEnabled = stat;
                Slot3CheckSlv.IsEnabled = stat;
            }

            if (Slot4CheckSel.IsChecked == true)
            {
                Slot4CheckSel.IsEnabled = stat;
                Slot4CheckMst.IsEnabled = stat;
                Slot4CheckSlv.IsEnabled = stat;
            }
        }

        void withCart_Click(object sender, RoutedEventArgs e)
        {
            if (withCart.IsChecked == true)
                EnableDtcSIFields();
        }

        void withOutCart_Click(object sender, RoutedEventArgs e)
        {
            if (withOutCart.IsChecked == true)
                CollapseAllDtcSIFields();
        }

        /// <summary>
        /// Re-activate hardware event handlers that were disabled when MuxWindow was opened
        /// Called when returning from MuxWindow to MainWindow
        /// </summary>
        public void ReactivateHardwareEventHandlers()
        {
            try
            {
                Log.Log.Info("Reactivating hardware event handlers after returning from MuxWindow");

                // Re-subscribe to hardware detection events
                hwInfo.HardwareDetected += OnHwConnected;
                hwInfo.HardwareDisconnected += OnHwDisconnected;
                hwInfo.CartDetected += OnCartDetected;
                hwInfo.StartScanning();

                Log.Log.Info("Hardware event handlers reactivated successfully");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error reactivating hardware event handlers: {ex.Message}");
            }
        }
    }

    public class DPSButtonManager
    {
        List<Button> _buttons;
        Button _exitButton;
        Button _loopBackButton;

        static DPSButtonManager _instance;
        static readonly object _lockObject = new object();

        // Singleton instance
        public static DPSButtonManager Instance
        {
            get
            {
                // Ensure thread safety when creating the instance
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new DPSButtonManager();
                        }
                    }
                }

                return _instance;
            }
        }

        private DPSButtonManager()
        {
        }

        public void InitDPSButtonManager(List<Button> buttons, Button exitButton, Button LoopBack)
        {
            _buttons = buttons;
            _exitButton = exitButton;
            _loopBackButton = LoopBack;
        }

        // Method to change the color of a specific button and disable others except exit
        public void SetButtonColorState(Button buttonToActivate, Color color)
        {
            // Disable all buttons except the exit button
            foreach (var button in _buttons)
            {
                button.IsEnabled = false;
                button.Background = new SolidColorBrush(Colors.DarkGray);
            }

            // Change color of the button to activate
            buttonToActivate.Background = new SolidColorBrush(color);
            _exitButton.IsEnabled = true; // Keep the exit button enabled
        }

        public void SetOnlyButtonColorState(Button buttonToActivate, Color color)
        {
            // Change color of the button to activate
            buttonToActivate.Background = new SolidColorBrush(color);
            buttonToActivate.IsEnabled = false; // Keep the exit button enabled
        }

        public void ResetOnlyButtonColorState(Button buttonToActivate, Color color)
        {
            // Change color of the button to activate
            buttonToActivate.Background = new SolidColorBrush(color);
            buttonToActivate.IsEnabled = true; // Keep the exit button enabled
        }

        public void SetButtonListColorState(List<Button> buttonsToShow, Color color)
        {
            // Disable all buttons except the exit button
            foreach (var button in _buttons)
            {
                button.IsEnabled = false;
                button.Background = new SolidColorBrush(Colors.DarkGray);
            }

            foreach (var button in buttonsToShow)
            {
                button.Background = buttonsToShow.Contains(button) ? new SolidColorBrush(color) : new SolidColorBrush(Colors.DarkGray);
            }

            // Change color of the button to activate
            _exitButton.IsEnabled = true; // Keep the exit button enabled
        }

        // Method to reset all button states (enable all buttons and reset colors)
        public void ResetButtonColorStates(Color defaultColor)
        {
            foreach (var button in _buttons)
            {
                button.IsEnabled = true;
                button.Background = new SolidColorBrush(defaultColor);
            }
        }

        public void SetButtonColor(Button buttonToActivate, Color color)
        {
            // Change color of the button to activate
            buttonToActivate.Background = new SolidColorBrush(color);
            _exitButton.IsEnabled = true; // Keep the exit button enabled
        }

        public void SetButtonColorByName(string name, Color color)
        {
            var m = new MainWindow();
            var button = m.GetButtonByName(name);

            button.Background = new SolidColorBrush(color);
        }

        // Method to reset all button states (enable all buttons and reset colors)
        public void ResetButtonColor(Color defaultColor)
        {
            foreach (var button in _buttons)
                button.Background = new SolidColorBrush(defaultColor);
        }

        // Generic method to show only specific buttons
        public void ShowOnlyButtons(List<Button> buttonsToShow)
        {
            foreach (var button in _buttons)
            {
                button.Visibility = buttonsToShow.Contains(button) ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public void ShowOrHideOnlyListButtons(List<Button> buttonsToShow, bool stat)
        {
            foreach (var button in buttonsToShow)
                button.Visibility = stat == true ? Visibility.Visible : Visibility.Hidden;
        }

        public void DisableOnlyButtons(List<Button> buttonsTodisable)
        {
            foreach (var button in _buttons)
                button.IsEnabled = buttonsTodisable.Contains(button) ? false : true;
        }

        // Method to handle key-down events to toggle button visibility
        public async Task<bool> HandleKeyDown(KeyEventArgs e, bool isPCMode, MainWindow sender)
        {
            var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (ctrl && e.Key == Key.P)
            {
                isPCMode = true;

                ShowOnlyButtons(new List<Button>
                {
                   _exitButton,
                   _buttons.Find(b => b.Name == "PerformanceCheck")
                });
            }
            else if (ctrl && e.Key == Key.U)
            {
                isPCMode = false;

                ShowOnlyButtons(new List<Button>
                {
                    _exitButton,
                    _buttons.Find(b => b.Name == "Utility")
                });
            }
            else if (ctrl && e.Key == Key.A)
            {
                isPCMode = false;

                ShowOnlyButtons(new List<Button>
                {
                    _exitButton,
                    _loopBackButton,
                    _buttons.Find(b => b.Name == "AppButton")
                 });
            }
            else if (ctrl && e.Key == Key.D)
            {
                isPCMode = false;

                var data = await LedState.GetVersionNumber();

                if (data == null)
                {
                    return isPCMode;
                }

                var version = System.Text.Encoding.ASCII.GetString(data);
                string checksum;

                switch (version)
                {
                    case "1.0":
                        checksum = GetFileHash(@"DPS_DTCL_FIRMWARE_V1_0.elf");
                        break;
                    case "1.1":
                        checksum = GetFileHash(@"DPS_DTCL_FIRMWARE_V1_1.elf");
                        break;
                    case "3.6":
                        if (System.IO.File.Exists(@"DDC.elf"))
                            checksum = GetFileHash(@"DDC.elf");
                        else if (System.IO.File.Exists(@"DTCL_FIRMWARE_V3_6.elf"))
                            checksum = GetFileHash(@"DTCL_FIRMWARE_V3_6.elf");
                        else
                            checksum = null;
                        version = "1.0";
                        break;
                    default:
                        checksum = null; // unofficial
                        break;
                }

                var msg = checksum != null
                    ? $"DTCL DDC Version {version} and CheckSum is {checksum}"
                    : $"Unofficial DDC Version {version}";

                MessageBox.Show(msg);
            }
            else if (ctrl && e.Key == Key.M)
            {
                if (sender.isCreatingMuxWindow)
                {
                    Log.Log.Info("Ctrl+M pressed but MuxWindow creation already in progress");
                    e.Handled = true;
                    return false;
                }

                if (sender._muxWindow == null)
                {
                    Log.Log.Info("Ctrl+M pressed - Creating MuxWindow");

                    // Set flag to prevent double creation
                    sender.isCreatingMuxWindow = true;

                    await sender.hwInfo.StopScanningAsync();

                    // Create MuxWindow
                    sender._muxWindow = new MuxWindow();

                    // Handle MuxWindow closed event - close DPSMainWindow and shutdown
                    /*sender._muxWindow.Closed += (s, args) => {
                        Log.Log.Info("MuxWindow closed - closing DPSMainWindow and shutting down");
                        sender._muxWindow = null;
                        sender.isCreatingMuxWindow = false; // Reset flag

                        // Close DPSMainWindow which will trigger shutdown
                        sender.isTransitioning = false; // Allow shutdown
                        sender.Close();
                    };*/

                    // Show MuxWindow
                    sender._muxWindow.Show();

                    Log.Log.Info($"MuxWindow shown, hiding DPSMainWindow");

                    sender.hwInfo.HardwareDetected -= sender.OnHwConnected;
                    sender.hwInfo.HardwareDisconnected -= sender.OnHwDisconnected;
                    sender.hwInfo.CartDetected -= sender.OnCartDetected;

                    if (sender.hwInfo._transport != null)
                    {
                        sender.hwInfo._transport.Dispose();
                        sender.hwInfo._transport = null;
                    }

                    // Hide DPSMainWindow instead of closing it
                    sender.Hide();

                    // Reset flag after window is shown
                    sender.isCreatingMuxWindow = false;
                    sender._muxWindow = null;
                }
                else
                {
                    // MuxWindow already exists, just bring it to front
                    Log.Log.Info("MuxWindow already exists, activating it");
                    sender._muxWindow.Activate();
                    sender._muxWindow.Focus();
                }

                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.G)
            {
                var guiChecksum = GetFileHash(@"DTCL.exe");
                MessageBox.Show($"DTCL GUI Interface Version V1.0 and checksum is {guiChecksum}");
            }

            return isPCMode;
        }

        public string GetFileHash(string filePath, string algorithm = "SHA256")
        {
            // Prepare the PowerShell command
            var command = $"Get-FileHash -Path \"{filePath}\" -Algorithm {algorithm} | Select-Object -ExpandProperty Hash";

            // Set up the PowerShell process
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-Command \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Start the process and read the output
            using (Process process = Process.Start(processInfo))
            {
                using (System.IO.StreamReader reader = process.StandardOutput)
                {
                    var result = reader.ReadToEnd().Trim();
                    return result;
                }
            }
        }

        // Method to show only exit and app buttons at startup
        public void ShowOnlyExitAtStart() => ShowOnlyButtons(new List<Button> { _exitButton });

        // Method to show all buttons except specific ones
        public void ShowAllButtonsExcept(params string[] buttonNamesToExclude)
        {
            foreach (var button in _buttons)
            {
                button.Visibility = buttonNamesToExclude.Contains(button.Name) ? Visibility.Hidden : Visibility.Visible;
            }
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // We only need one-way conversion in this scenario
            throw new NotSupportedException();
        }
    }

    public class CartTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CartType currentCartType && parameter is CartType targetCartType)
            {
                // Make visible if current cart type matches the target or if it's a Darin type (Darin1, Darin2, Darin3)
                // You might need to refine this logic based on which specific CartTypes should show the DTC S.No.
                // For simplicity, this example makes it visible for Darin1, Darin2, Darin3.
                if (currentCartType == CartType.Darin1 ||
                    currentCartType == CartType.Darin2 ||
                    currentCartType == CartType.Darin3)
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AndToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return Visibility.Collapsed;

            var value1 = values[0] is bool && (bool)values[0];
            var value2 = values[1] is bool && (bool)values[1];

            return (value1 && value2) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}