using DTCL.Cartridges;
using DTCL.JsonParser;
using DTCL.Messages;
using DTCL.Transport;
using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Security.Cryptography;
using IspProtocol;

namespace DTCL
{
    public partial class Utility : Window
    {
        // Observable collection to store hex rows
        public ObservableCollection<HexRow> HexRows { get; set; }
        public ObservableCollection<HexRow> ManualHexRows { get; set; }
        public ObservableCollection<HexRow> RepeatedHexRows { get; set; }

        public UploadMessageInfoContainer uMessageContainerObj;
        public DownloadMessageInfoContainer dMessageContainerObj;
        public JsonParser<UploadMessageInfoContainer> uMessageParserObj;
        public JsonParser<DownloadMessageInfoContainer> dMessageParserObj;
        // CartType cartType;
        string _boardId;
        byte _cartNo;

        HardwareInfo hwInfo = HardwareInfo.Instance;
        public Utility(string boardId, byte cartNo, bool isCartDetected = false)
        {
            _boardId = boardId;
            _cartNo = cartNo;
            // cartType = _cartType;
            InitializeComponent();

            // Initialize the collection with some dummy data
            HexRows = new ObservableCollection<HexRow>();
            /*for (int i = 0; i < 10; i++) // Adding 10 rows of data
            {
                HexRows.Add(new HexRow
                {
                    Hex0 = "00",
                    Hex1 = "01",
                    Hex2 = "02",
                    Hex3 = "03",
                    Hex4 = "04",
                    Hex5 = "05",
                    Hex6 = "06",
                    Hex7 = "07",
                    Hex8 = "08",
                    Hex9 = "09",
                    HexA = "0A",
                    HexB = "0B",
                    HexC = "0C",
                    HexD = "0D",
                    HexE = "0E",
                    HexF = "0F"
                });
            }*/

            DisplayDefaultBlockData();
            BlockNumber.Text = "0";
            PageNumber.Text = "5";
            // Set the DataContext for the DataGrid binding
            DataContext = this;

            // Initialize collections
            ManualHexRows = new ObservableCollection<HexRow>();
            RepeatedHexRows = new ObservableCollection<HexRow>();

            // Bind data to grids
            ManualDataEntryGrid.ItemsSource = ManualHexRows;
            RepeatedHexDataGrid.ItemsSource = RepeatedHexRows;

            // Load manual data entry with default 16 bytes of data
            for (int i = 0; i < 1; i++)
            {
                ManualHexRows.Add(new HexRow
                {
                    Hex0 = "00",
                    Hex1 = "11",
                    Hex2 = "22",
                    Hex3 = "33",
                    Hex4 = "44",
                    Hex5 = "55",
                    Hex6 = "66",
                    Hex7 = "77",
                    Hex8 = "88",
                    Hex9 = "99",
                    HexA = "1A",
                    HexB = "2B",
                    HexC = "3C",
                    HexD = "4D",
                    HexE = "5E",
                    HexF = "6F"
                });
            }

            if ((_boardId == "DPS2_4_IN_1") || (_boardId == "DTCL" && _cartNo == 2))
            {
                uMessageContainerObj = new UploadMessageInfoContainer();

                uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

                uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");

                Log.Log.Info("D2 Upload Messages Initialized");

                dMessageContainerObj = new DownloadMessageInfoContainer();

                dMessageParserObj = new JsonParser<DownloadMessageInfoContainer>();

                dMessageContainerObj = dMessageParserObj.Deserialize("D2\\D2DownloadMessageDetails.json");

                Log.Log.Info("D2 Download Messages Initialized");

                HexDataGrid.Visibility = Visibility.Visible;
                BlockNumber.Visibility = Visibility.Visible;
                PageNumber.Visibility = Visibility.Visible;
                ReadD2Block.Visibility = Visibility.Visible;
                WriteD2Block.Visibility = Visibility.Visible;
                DataLabel.Visibility = Visibility.Visible;
                BlockNoLabel.Visibility = Visibility.Visible;
                NoOfPagesLabel.Visibility = Visibility.Visible;
                WriteData.Visibility = Visibility.Visible;
                DataToWrite.Visibility = Visibility.Visible;
            }
            else if (_boardId == "DPS3_4_IN_1" || (_boardId == "DTCL" && _cartNo == 3))
            {
                uMessageContainerObj = new UploadMessageInfoContainer();

                uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

                uMessageContainerObj = uMessageParserObj.Deserialize("D3\\D3UploadMessageDetails.json");

                Log.Log.Info("D3 Upload Messages Initialized");

                dMessageContainerObj = new DownloadMessageInfoContainer();

                dMessageParserObj = new JsonParser<DownloadMessageInfoContainer>();

                dMessageContainerObj = dMessageParserObj.Deserialize("D3\\D3DownloadMessageDetails.json");

                Log.Log.Info("D3 Download Messages Initialized");

                HexDataGrid.Visibility = Visibility.Collapsed;
                BlockNumber.Visibility = Visibility.Collapsed;
                PageNumber.Visibility = Visibility.Collapsed;
                ReadD2Block.Visibility = Visibility.Collapsed;
                WriteD2Block.Visibility = Visibility.Collapsed;
                DataLabel.Visibility = Visibility.Collapsed;
                BlockNoLabel.Visibility = Visibility.Collapsed;
                NoOfPagesLabel.Visibility = Visibility.Collapsed;
                WriteData.Visibility = Visibility.Collapsed;
                DataToWrite.Visibility = Visibility.Collapsed;
            }

            PopulateFileSelections();

            if (!isCartDetected)
            {
                WriteD2Block.IsEnabled = false;
                HexDataGrid.IsEnabled = false;
                ReadD2Block.IsEnabled = false;
                WriteDwnFiles.IsEnabled = false;
            }
        }

        void PopulateFileSelections()
        {
            // Populate upload file selection panel
            foreach (var message in uMessageContainerObj.MessageInfoList)
            {
                var uploadFileOption = new RadioButton
                {
                    Content = message.FileName,
                    GroupName = "UploadFileSelection",
                    Tag = message
                };

                uploadFileOption.Checked += FileOption_Checked;

                UploadFileSelectionPanel.Children.Add(uploadFileOption);
            }

            // Populate download file selection panel
            foreach (var message in dMessageContainerObj.MessageInfoList)
            {
                var downloadFileOption = new RadioButton
                {
                    Content = message.FileName,
                    GroupName = "DownloadFileSelection",
                    Tag = message
                };

                downloadFileOption.Checked += FileOption_Checked;

                DownloadFileSelectionPanel.Children.Add(downloadFileOption);
            }
        }

        void FileOption_Checked(object sender, RoutedEventArgs e)
        {
            var selectedFile = (IMessageInfo)((RadioButton)sender).Tag;
            NOBValue.Text = selectedFile.Nob.ToString();
        }

        void CreateFileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = UploadFileSelectionPanel.Children.OfType<RadioButton>()
                             .FirstOrDefault(r => r.IsChecked == true)?.Tag as IMessageInfo ??
                           DownloadFileSelectionPanel.Children.OfType<RadioButton>()
                             .FirstOrDefault(r => r.IsChecked == true)?.Tag as IMessageInfo;

            if (selectedFile == null)
            {
                MessageBox.Show("Please select a file.");
                return;
            }

            int dataSize;

            if (!int.TryParse(NOBValue.Text, out dataSize))
            {
                MessageBox.Show("Invalid NOB value.");
                return;
            }

            RepeatedHexRows.Clear();

            // Repeat the data based on the selected file and NOB
            for (int i = 0; i < dataSize * selectedFile.NobSize; i++)
            {
                foreach (var row in ManualHexRows)
                {
                    RepeatedHexRows.Add(new HexRow
                    {
                        Hex0 = row.Hex0,
                        Hex1 = row.Hex1,
                        Hex2 = row.Hex2,
                        Hex3 = row.Hex3,
                        Hex4 = row.Hex4,
                        Hex5 = row.Hex5,
                        Hex6 = row.Hex6,
                        Hex7 = row.Hex7,
                        Hex8 = row.Hex8,
                        Hex9 = row.Hex9,
                        HexA = row.HexA,
                        HexB = row.HexB,
                        HexC = row.HexC,
                        HexD = row.HexD,
                        HexE = row.HexE,
                        HexF = row.HexF
                    });
                }
            }

            DeleteExtraCellsFromRepeatDataGrid(dataSize * selectedFile.NobSize);

            MessageBox.Show($"File {selectedFile.FileName} generated with {dataSize} blocks.");
        }

        void DeleteExtraCellsFromRepeatDataGrid(int totalBytes)
        {
            var requiredRows = (int)Math.Ceiling(totalBytes / 16.0);

            while (RepeatedHexRows.Count > requiredRows)
                RepeatedHexRows.RemoveAt(RepeatedHexRows.Count - 1);

            // Adjust individual rows to match the exact byte requirement
            if (RepeatedHexRows.Count > 0)
            {
                var remainingBytes = totalBytes % 16;

                if (remainingBytes > 0)
                {
                    var lastRow = RepeatedHexRows[RepeatedHexRows.Count - 1];
                    ClearExtraCellsInRow(lastRow, remainingBytes);
                }
            }

            // MessageBox.Show($"Adjusted Repeat Data Grid to {totalBytes} bytes.");
        }

        void ClearExtraCellsInRow(HexRow row, int requiredBytes)
        {
            var properties = new List<Action<string>>
           {
              value => row.Hex0 = value,
              value => row.Hex1 = value,
              value => row.Hex2 = value,
              value => row.Hex3 = value,
              value => row.Hex4 = value,
              value => row.Hex5 = value,
              value => row.Hex6 = value,
              value => row.Hex7 = value,
              value => row.Hex8 = value,
              value => row.Hex9 = value,
              value => row.HexA = value,
              value => row.HexB = value,
              value => row.HexC = value,
              value => row.HexD = value,
              value => row.HexE = value,
              value => row.HexF = value
           };

            for (int i = requiredBytes; i < properties.Count; i++)
                properties[i](null);
        }

        async void ReadD2Block_Click(object sender, RoutedEventArgs e)
        {
            DisableEntries();
            await hwInfo.StopScanningAsync();

            var progress = new Progress<int>(value => UpdateProgress.Value = value);

            int blockNo;
            int noOfPages;

            if (!int.TryParse(BlockNumber.Text, out blockNo) || blockNo < 0 || blockNo > 1024)
            {
                MessageBox.Show("Block Number must be between 0 and 1024.");
                return;
            }

            if (!int.TryParse(PageNumber.Text, out noOfPages) || noOfPages < 1 || noOfPages > 32)
            {
                MessageBox.Show("Number of Pages must be between 1 and 32.");
                return;
            }

            // DataHandler.Instance.ResetProgressValues();
            // DataHandler.Instance.SetProgressValues((noOfPages * 512), 0);

            var d2Obj = (Darin2)hwInfo.CartObj;
            var ret = await d2Obj.ReadD2BlockData(blockNo, noOfPages, 512, _cartNo, progress);
            await Task.Delay(10);

            if (ret == returnCodes.DTCL_SUCCESS)
            {
                DisplayBlockData(noOfPages);
                MessageBox.Show("Read Block Success");
            }
            else
            {
                MessageBox.Show("Failed to Read Block");
            }

            hwInfo.StartScanning();
            EnableEntries();
        }

        public void DisplayBlockData(int noOfPages)
        {
            HexRows.Clear();

            for (int pageNo = 1; pageNo <= noOfPages; pageNo++)
            {
                var pageData = DataBlock.GetPageData(pageNo, 512);

                // Verify we have enough data
                if (pageData == null || pageData.Length < 512)
                {
                    Log.Log.Warning($"Warning: Page {pageNo} data is less than expected 512 bytes.");
                    continue;
                }

                // Process 512 bytes in rows of 16
                for (int i = 0; i < pageData.Length; i += 16)
                {
                    // Create and fill a new HexRow for each set of 16 bytes
                    var hexRow = new HexRow();

                    for (int j = 0; j < 16; j++)
                    {
                        // Use reflection to set properties dynamically
                        hexRow.GetType().GetProperty($"Hex{j:X}")?.SetValue(hexRow, pageData[i + j].ToString("X2"));
                    }

                    HexRows.Add(hexRow);
                }
            }
        }

        public void FillBlockDataFromRows(int noOfPages)
        {
            for (int pageNo = 1; pageNo <= noOfPages; pageNo++)
            {
                var pageData = new byte[512];

                // Locate the rows for the current page
                var startRowIndex = (pageNo - 1) * 32; // Each page has 32 rows (16 bytes each) to fill 512 bytes

                for (int i = 0; i < 32; i++) // Loop for each row in a page
                {
                    // Check if we have the row for this index; if not, assume zero-filled data
                    if (startRowIndex + i >= HexRows.Count)
                    {
                        // Manually fill remaining data with zeros if rows are missing
                        for (int k = i * 16; k < 512; k++)
                            pageData[k] = 0;

                        break;
                    }

                    var hexRow = HexRows[startRowIndex + i];

                    // Extract each hex value from HexRow and convert to byte
                    for (int j = 0; j < 16; j++) // Each row represents 16 bytes
                    {
                        var hexValue = hexRow.GetType().GetProperty($"Hex{j:X}")?.GetValue(hexRow)?.ToString();

                        if (string.IsNullOrEmpty(hexValue))
                        {
                            pageData[i * 16 + j] = 0; // Set to zero if the hex value is empty or null
                        }
                        else if (byte.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out byte byteValue))
                        {
                            pageData[i * 16 + j] = byteValue;
                        }
                        else
                        {
                            pageData[i * 16 + j] = 0; // Default to zero if parsing fails
                        }
                    }
                }

                DataBlock.UpdatePageData(pageNo, pageData);
            }
        }

        public void DisplayDefaultBlockData(int NoOfPages = 1, int data = 0)
        {
            HexRows.Clear();

            for (int pangeNo = 1; pangeNo <= NoOfPages; pangeNo++)
            {
                for (int i = 0, j = 0; i < 32; i++)
                {
                    HexRows.Add(new HexRow
                    {
                        Hex0 = data.ToString("X2"),
                        Hex1 = data.ToString("X2"),
                        Hex2 = data.ToString("X2"),
                        Hex3 = data.ToString("X2"),
                        Hex4 = data.ToString("X2"),
                        Hex5 = data.ToString("X2"),
                        Hex6 = data.ToString("X2"),
                        Hex7 = data.ToString("X2"),
                        Hex8 = data.ToString("X2"),
                        Hex9 = data.ToString("X2"),
                        HexA = data.ToString("X2"),
                        HexB = data.ToString("X2"),
                        HexC = data.ToString("X2"),
                        HexD = data.ToString("X2"),
                        HexE = data.ToString("X2"),
                        HexF = data.ToString("X2")
                    });

                    j = j + 16;
                }
            }
        }

        void Header_Click(object sender, RoutedEventArgs e)
        {
            if (_boardId == "DPS2_4_IN_1" || (_boardId == "DTCL" && _cartNo == 2))
            {
                var d = new HeaderInfoDisplay(CartType.Darin2);
                d.Show();
            }
            else if (_boardId == "DPS3_4_IN_1" || (_boardId == "DTCL" && _cartNo == 3))
            {
                var d = new HeaderInfoDisplay(CartType.Darin3);
                d.Show();
            }
        }

        async void WriteDwnFiles_Click(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show("TDB");
            DisableEntries();
            await HardwareInfo.Instance.StopScanningAsync();
            var progress = new Progress<int>(value => UpdateProgress.Value = value);

            if ((_boardId == "DPS2_4_IN_1") || (_boardId == "DTCL" && _cartNo == 2))
            {
                var path = @"C:\mps\darin2\TestDownloadFiles\";

                var d2Obj = (Darin2)hwInfo.CartObj;

                d2Obj.InitializeUploadMessages(path);
                // d2Obj.InitializeUploadMessagesFrom_DR(path);
                d2Obj.InitializeDownloadMessages();

                d2Obj.allocate_space(); // assign fsb

                // Read header and initialize messages

                var ret = await d2Obj.ReadHeaderSpaceDetails(2);

                if ((ret == returnCodes.DTCL_BLANK_CARTRIDGE))
                {
                    ret = d2Obj.InitUpdMsgWithHeaderSpaceDetails(ret);

                    for (int itr = 1; itr < 5; itr++)
                    {
                        if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                            await d2Obj.WriteHeaderSpaceDetails((byte)hwInfo.SlotInfo[itr].SlotNumber);
                    }
                }

                // if (ret != returnCodes.DTCL_SUCCESS)
                // {
                //    DTCLInfo.Instance.StartScanningPorts(); 
                //   return;
                // }

                ret = d2Obj.InitDwnMsgForWritingToCart(path);

                Log.Log.Info("Calculation size of TestDownloadFiles directory");

                var size = FileOperations.getDirectorySize(path);

                // DataHandler.Instance.ResetProgressValues();
                // DataHandler.Instance.SetProgressValues(size, 0);

                for (int msg_number = 2; msg_number <= 17; msg_number++)
                {
                    if ((msg_number == 5) || (msg_number == 6) || (msg_number == 14))
                        continue;

                    var dMessageInfo = (DownloadMessageInfo)d2Obj.dMessageContainerObj.FindMessageByMsgId(msg_number);

                    if (dMessageInfo == null || dMessageInfo.NoOfBlocks <= 0)
                    {
                        Log.Log.Error($"No blocks found for MessageID-{msg_number}. Skipping.");
                        continue;
                    }

                    var present_block_no = dMessageInfo.fsb;

                    Log.Log
                        .Info($"Writing MessageID-{msg_number} MessageName-{dMessageInfo.FileName}, Block Address: {present_block_no}, Actual size: {dMessageInfo.ActualFileSize} Total Blocks: {dMessageInfo.NoOfBlocks}");

                    var blocks_written = 0;

                    while (blocks_written < dMessageInfo.NoOfBlocks)
                    {
                        var no_of_pages = (blocks_written + 1 == dMessageInfo.NoOfBlocks) ?
                                            dMessageInfo.ActualFileNoOfPagesLastBlock : 32;

                        var last_page_size = (blocks_written + 1 == dMessageInfo.NoOfBlocks) ?
                                              dMessageInfo.ActualFileLastPageSize : 512;

                        ret = d2Obj.ReadBlockDataFromFile(path, dMessageInfo, 1, no_of_pages, last_page_size);

                        if (ret == returnCodes.DTCL_FILE_NOT_FOUND)
                        {
                            Log.Log.Error($"Error reading block data for MessageID-{msg_number}, file missing.");
                            break;
                        }

                        else if (ret != returnCodes.DTCL_SUCCESS)
                        {
                            Log.Log.Error($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                            MessageBox.Show($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                            HardwareInfo.Instance.StartScanning();
                            EnableEntries();
                            return;
                        }

                        for (int itr = 1; itr < 5; itr++)
                        {
                            if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                                await d2Obj.WriteD2BlockData(present_block_no, no_of_pages, last_page_size, (byte)hwInfo.SlotInfo[itr].SlotNumber, progress);
                        }

                        if (ret != returnCodes.DTCL_SUCCESS)
                        {
                            Log.Log.Error($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                            MessageBox.Show($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                            HardwareInfo.Instance.StartScanning();
                            EnableEntries();
                            return;
                        }

                        present_block_no++;
                        blocks_written++;
                    }
                }

                MessageBox.Show("Download Files Write Success");
            }
            else if ((_boardId == "DPS3_4_IN_1") || (_boardId == "DTCL" && _cartNo == 3))
            {
                var path = @"C:\mps\darin3\TestDownloadFiles\";
                var size = FileOperations.getDirectorySize(path);

                // DataHandler.Instance.ResetProgressValues();
                // DataHandler.Instance.SetProgressValues(size, 0);

                var d3Obj = (Darin3)hwInfo.CartObj;

                var res = await d3Obj.powerCycle();

                d3Obj.InitializeDownloadMessages();

                var ret = await d3Obj.PerformUploadOperation_DwnFiles(path, _cartNo, progress);

                if (ret == returnCodes.DTCL_SUCCESS)
                    MessageBox.Show("Download Files Write Success");
                else
                    MessageBox.Show("Download Files Write Failed");
            }

            EnableEntries();
            HardwareInfo.Instance.StartScanning();
        }

        void Exit_Click(object sender, RoutedEventArgs e) => Close();

        async void WriteD2Block_Click(object sender, RoutedEventArgs e)
        {
            DisableEntries();
            await HardwareInfo.Instance.StopScanningAsync();

            var progress = new Progress<int>(value => UpdateProgress.Value = value);

            int blockNo;
            int noOfPages;

            if (!int.TryParse(BlockNumber.Text, out blockNo) || blockNo < 0 || blockNo > 1024)
            {
                MessageBox.Show("Block Number must be between 0 and 1024.");
                EnableEntries();
                return;
            }

            if (!int.TryParse(PageNumber.Text, out noOfPages) || noOfPages < 1 || noOfPages > 32)
            {
                MessageBox.Show("Number of Pages must be between 1 and 32.");
                EnableEntries();
                return;
            }

            var d2Obj = (Darin2)hwInfo.CartObj;

            FillBlockDataFromRows(noOfPages);

            // DataHandler.Instance.ResetProgressValues();
            // DataHandler.Instance.SetProgressValues((noOfPages * 512), 0);

            var ret = -1;

            for (int itr = 1; itr < 5; itr++)
            {
                if (hwInfo.SlotInfo[itr].IsSlotSelected_ByUser == true)
                    ret = await d2Obj.WriteD2BlockData(blockNo, noOfPages, 512, (byte)hwInfo.SlotInfo[itr].SlotNumber, progress);
            }

            if (ret == 0)
            {
                MessageBox.Show("Write Block Success");
            }
            else
                MessageBox.Show("Write Block Failed");

            EnableEntries();
            HardwareInfo.Instance.StartScanning();
        }

        void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = UploadFileSelectionPanel.Children.OfType<RadioButton>()
                            .FirstOrDefault(r => r.IsChecked == true)?.Tag as IMessageInfo ??
                          DownloadFileSelectionPanel.Children.OfType<RadioButton>()
                            .FirstOrDefault(r => r.IsChecked == true)?.Tag as IMessageInfo;

            if (selectedFile == null)
            {
                MessageBox.Show("Please select a file.");
                EnableEntries();
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Binary Files (*.bin)|*.bin",
                FileName = selectedFile.FileName,
                Title = "Save Binary File"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                try
                {
                    SaveDataAsBinary(filePath);
                    MessageBox.Show($"File saved successfully at: {filePath}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            EnableEntries();
        }

        void SaveDataAsBinary(string filePath)
        {
            var data = new List<byte>();

            foreach (var item in RepeatedHexDataGrid.Items)
            {
                if (item is HexRow hexRow)
                {
                    if (hexRow.Hex0 != null) data.Add(ConvertHexToByte(hexRow.Hex0));
                    if (hexRow.Hex1 != null) data.Add(ConvertHexToByte(hexRow.Hex1));
                    if (hexRow.Hex2 != null) data.Add(ConvertHexToByte(hexRow.Hex2));
                    if (hexRow.Hex3 != null) data.Add(ConvertHexToByte(hexRow.Hex3));
                    if (hexRow.Hex4 != null) data.Add(ConvertHexToByte(hexRow.Hex4));
                    if (hexRow.Hex5 != null) data.Add(ConvertHexToByte(hexRow.Hex5));
                    if (hexRow.Hex6 != null) data.Add(ConvertHexToByte(hexRow.Hex6));
                    if (hexRow.Hex7 != null) data.Add(ConvertHexToByte(hexRow.Hex7));
                    if (hexRow.Hex8 != null) data.Add(ConvertHexToByte(hexRow.Hex8));
                    if (hexRow.Hex9 != null) data.Add(ConvertHexToByte(hexRow.Hex9));
                    if (hexRow.HexA != null) data.Add(ConvertHexToByte(hexRow.HexA));
                    if (hexRow.HexB != null) data.Add(ConvertHexToByte(hexRow.HexB));
                    if (hexRow.HexC != null) data.Add(ConvertHexToByte(hexRow.HexC));
                    if (hexRow.HexD != null) data.Add(ConvertHexToByte(hexRow.HexD));
                    if (hexRow.HexE != null) data.Add(ConvertHexToByte(hexRow.HexE));
                    if (hexRow.HexF != null) data.Add(ConvertHexToByte(hexRow.HexF));
                }
            }

            System.IO.File.WriteAllBytes(filePath, data.ToArray());
        }

        byte ConvertHexToByte(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return 0;

            return Convert.ToByte(hex, 16);
        }

        void FileType_Checked(object sender, RoutedEventArgs e)
        {
            if (UploadFileSelectionPanel != null && DownloadFileSelectionPanel != null)
            {
                UploadFileSelectionPanel.IsEnabled = UploadRadioButton.IsChecked ?? false;
                DownloadFileSelectionPanel.IsEnabled = DownloadRadioButton.IsChecked ?? false;
            }
        }

        void WriteData_TextChanged(object sender, TextChangedEventArgs e)
        {
            int blockNo;
            int noOfPages;

            // Validate Block Number
            if (!int.TryParse(BlockNumber.Text, out blockNo) || blockNo < 0 || blockNo > 1024)
            {
                MessageBox.Show("Block Number must be between 0 and 1024.");
                return;
            }

            // Validate Page Number
            if (!int.TryParse(PageNumber.Text, out noOfPages) || noOfPages < 1 || noOfPages > 32)
            {
                MessageBox.Show("Number of Pages must be between 1 and 32.");
                return;
            }

            // Validate WriteData as hexadecimal
            var hexValue = WriteData.Text;

            if (!IsHexValueValid(hexValue))
            {
                MessageBox.Show("Data must be a valid hexadecimal value (00 to FF).");
                return;
            }

            // Convert hex string to integer value
            var data = Convert.ToInt32(hexValue, 16);
            DisplayDefaultBlockData(noOfPages, (byte)data);
        }

        // Helper method to check if a string is a valid hex value
        bool IsHexValueValid(string hexValue)
        {
            if (string.IsNullOrEmpty(hexValue) || hexValue.Length > 2) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(hexValue, @"\A\b[0-9a-fA-F]+\b\Z");
        }

        public void DisableEntries()
        {
            ReadD2Block.IsEnabled = false;
            WriteD2Block.IsEnabled = false;
            WriteDwnFiles.IsEnabled = false;
            Header.IsEnabled = false;
            BlockNumber.IsEnabled = false;
            PageNumber.IsEnabled = false;
            WriteData.IsEnabled = false;
        }

        public void EnableEntries()
        {
            ReadD2Block.IsEnabled = true;
            WriteD2Block.IsEnabled = true;
            WriteDwnFiles.IsEnabled = true;
            Header.IsEnabled = true;
            BlockNumber.IsEnabled = true;
            PageNumber.IsEnabled = true;
            WriteData.IsEnabled = true;
        }

        void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a File",
                Filter = "All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
            }
        }

        void CalculateChecksumButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    MessageBox.Show("Please select a valid file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var checksum = GetFileHash(FilePathTextBox.Text);
                ChecksumResultTextBox.Text = checksum;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating checksum: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void ClearChecksumInputButton_Click(object sender, RoutedEventArgs e)
        {
            ChecksumResultTextBox.Text = "";
        }

        public string GetFileHash(string filePath, string algorithm = "SHA256")
        {
            try
            {
                // Validate file existence
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("The specified file does not exist.", filePath);
                }

                // Select the hashing algorithm
                HashAlgorithm hashAlgorithm = SHA256.Create();

                // Compute the hash
                var fileStream = File.OpenRead(filePath);
                var hashBytes = hashAlgorithm.ComputeHash(fileStream);

                // Convert hash to hexadecimal string
                return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating hash: {ex.Message}");
                return string.Empty;
            }
        }

        public void utility_Closed(object sender, EventArgs e)
        {
            MessageBox.Show("Utility Window closed.");
        }

        // Example event handler for the Read button (you can expand it with logic)
        /* private void ReadButton_Click(object sender, RoutedEventArgs e)
         {
             MessageBox.Show("Read button clicked.");
             // Add your logic to read data here
         }

         // Example event handler for the Write button (you can expand it with logic)
         private void WriteButton_Click(object sender, RoutedEventArgs e)
         {
             MessageBox.Show("Write button clicked.");
             // Add your logic to write data here
         }

         // Example event handler for the Header Info button (you can expand it with logic)
         private void HeaderInfoButton_Click(object sender, RoutedEventArgs e)
         {
             MessageBox.Show("Header Info button clicked.");
             // Add your logic to show header info here
         }*/
    }

    // HexRow class representing each row of hex data
    public class HexRow : INotifyPropertyChanged
    {
        string hex0;
        string hex1;
        string hex2;
        string hex3;
        string hex4;
        string hex5;
        string hex6;
        string hex7;
        string hex8;
        string hex9;
        string hexA;
        string hexB;
        string hexC;
        string hexD;
        string hexE;
        string hexF;

        // Properties for each hex column
        public string Hex0
        {
            get { return hex0; }
            set
            {
                hex0 = value;
                OnPropertyChanged(nameof(Hex0));
            }
        }

        public string Hex1
        {
            get { return hex1; }
            set
            {
                hex1 = value;
                OnPropertyChanged(nameof(Hex1));
            }
        }

        public string Hex2
        {
            get { return hex2; }
            set
            {
                hex2 = value;
                OnPropertyChanged(nameof(Hex2));
            }
        }

        public string Hex3
        {
            get { return hex3; }
            set
            {
                hex3 = value;
                OnPropertyChanged(nameof(Hex3));
            }
        }

        public string Hex4
        {
            get { return hex4; }
            set
            {
                hex4 = value;
                OnPropertyChanged(nameof(Hex4));
            }
        }

        public string Hex5
        {
            get { return hex5; }
            set
            {
                hex5 = value;
                OnPropertyChanged(nameof(Hex5));
            }
        }

        public string Hex6
        {
            get { return hex6; }
            set
            {
                hex6 = value;
                OnPropertyChanged(nameof(Hex6));
            }
        }

        public string Hex7
        {
            get { return hex7; }
            set
            {
                hex7 = value;
                OnPropertyChanged(nameof(Hex7));
            }
        }

        public string Hex8
        {
            get { return hex8; }
            set
            {
                hex8 = value;
                OnPropertyChanged(nameof(Hex8));
            }
        }

        public string Hex9
        {
            get { return hex9; }
            set
            {
                hex9 = value;
                OnPropertyChanged(nameof(Hex9));
            }
        }

        public string HexA
        {
            get { return hexA; }
            set
            {
                hexA = value;
                OnPropertyChanged(nameof(HexA));
            }
        }

        public string HexB
        {
            get { return hexB; }
            set
            {
                hexB = value;
                OnPropertyChanged(nameof(HexB));
            }
        }

        public string HexC
        {
            get { return hexC; }
            set
            {
                hexC = value;
                OnPropertyChanged(nameof(HexC));
            }
        }

        public string HexD
        {
            get { return hexD; }
            set
            {
                hexD = value;
                OnPropertyChanged(nameof(HexD));
            }
        }

        public string HexE
        {
            get { return hexE; }
            set
            {
                hexE = value;
                OnPropertyChanged(nameof(HexE));
            }
        }

        public string HexF
        {
            get { return hexF; }
            set
            {
                hexF = value;
                OnPropertyChanged(nameof(HexF));
            }
        }

        // Event for property changes
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}