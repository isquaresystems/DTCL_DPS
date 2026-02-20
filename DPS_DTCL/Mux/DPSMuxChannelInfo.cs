using DTCL.Transport;
using System.Collections.Generic;
using System.ComponentModel;

namespace DTCL.Mux
{
    /// <summary>
    /// Data model for a single DPS MUX channel with 4 slots
    /// Supports DPS2_4_IN_1 (4 NAND slots) or DPS3_4_IN_1 (4 CF slots)
    /// </summary>
    public class DPSMuxChannelInfo : INotifyPropertyChanged
    {
        // Channel identification (1-8)
        public int Channel { get; set; }

        // Connection state
        private bool _isDPSConnected;
        public bool isDPSConnected
        {
            get => _isDPSConnected;
            set
            {
                if (_isDPSConnected != value)
                {
                    _isDPSConnected = value;
                    OnPropertyChanged(nameof(isDPSConnected));
                }
            }
        }

        // Hardware identification
        private string _hardwareType;
        public string HardwareType
        {
            get => _hardwareType;
            set
            {
                if (_hardwareType != value)
                {
                    _hardwareType = value;
                    OnPropertyChanged(nameof(HardwareType));
                }
            }
        }

        // Cart type (Darin2 or Darin3)
        private string _cartType;
        public string CartType
        {
            get => _cartType;
            set
            {
                if (_cartType != value)
                {
                    _cartType = value;
                    OnPropertyChanged(nameof(CartType));
                }
            }
        }

        // User selection - Select Port checkbox
        private bool _isUserSelected;
        public bool isUserSelected
        {
            get => _isUserSelected;
            set
            {
                if (_isUserSelected != value)
                {
                    _isUserSelected = value;
                    OnPropertyChanged(nameof(isUserSelected));
                }
            }
        }

        // Unit Serial Number
        private string _unitSno;
        public string UnitSno
        {
            get => _unitSno;
            set
            {
                if (_unitSno != value)
                {
                    _unitSno = value;
                    OnPropertyChanged(nameof(UnitSno));
                }
            }
        }

        // 4 Slot DTC Serial Numbers [0]=unused, [1-4]=slot data
        public string[] DTCSerialNumbers { get; set; } = new string[5];

        // 4 Slot Selection Checkboxes [0]=unused, [1-4]=slot data
        public bool[] IsSlotSelected { get; set; } = new bool[5];

        // 4 Slot Cart Detection Status [0]=unused, [1-4]=slot data
        public bool[] IsCartDetected { get; set; } = new bool[5];

        // 4 Slot Detected Cart Types [0]=unused, [1-4]=slot data
        public CartType[] DetectedCartTypes { get; set; } = new CartType[5];

        // Performance Check Results per slot [0]=unused, [1-4]=slot data
        public string[] PCStatus { get; set; } = new string[5];

        // Overall Performance Check Status for this channel
        private string _overallPCStatus;
        public string OverallPCStatus
        {
            get => _overallPCStatus;
            set
            {
                if (_overallPCStatus != value)
                {
                    _overallPCStatus = value;
                    OnPropertyChanged(nameof(OverallPCStatus));
                }
            }
        }

        // UI State - Yellow highlight during PC execution
        private bool _isInProgress;
        public bool isInProgress
        {
            get => _isInProgress;
            set
            {
                if (_isInProgress != value)
                {
                    _isInProgress = value;
                    OnPropertyChanged(nameof(isInProgress));
                }
            }
        }

        // Internal slot info data [0]=unused, [1-4]=slot data
        public SlotInfo[] channel_SlotInfo { get; set; } = new SlotInfo[5];

        // Persistent log file paths (not cleared during channel reset)
        // Key: slot number (1-4), Value: log file path
        public Dictionary<int, string> SlotLogPaths { get; set; } = new Dictionary<int, string>();

        // Constructor
        public DPSMuxChannelInfo(int channelNumber)
        {
            Channel = channelNumber;
            _isDPSConnected = false;
            _hardwareType = "";
            _cartType = "";
            _isUserSelected = false;
            _unitSno = "999";
            _overallPCStatus = "";
            _isInProgress = false;

            // Initialize arrays with default values
            for (int i = 1; i <= 4; i++)
            {
                DTCSerialNumbers[i] = "999";
                IsSlotSelected[i] = false;
                IsCartDetected[i] = false;
                DetectedCartTypes[i] = DTCL.CartType.Unknown;
                PCStatus[i] = "";
                channel_SlotInfo[i] = new SlotInfo(i);
            }
        }

        // Helper method to update overall PC status based on individual slots
        public void UpdateOverallPCStatus()
        {
            bool hasPass = false;
            bool hasFail = false;

            for (int i = 1; i <= 4; i++)
            {
                if (IsSlotSelected[i])
                {
                    if (PCStatus[i] == "PASS")
                        hasPass = true;
                    else if (PCStatus[i] == "FAIL")
                        hasFail = true;
                }
            }

            // Determine overall status
            if (hasFail)
                OverallPCStatus = "FAIL";
            else if (hasPass)
                OverallPCStatus = "PASS";
            else
                OverallPCStatus = "";
        }

        // Helper method to clear all results
        public void ClearResults()
        {
            for (int i = 1; i <= 4; i++)
            {
                PCStatus[i] = "";
            }
            OverallPCStatus = "";
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
