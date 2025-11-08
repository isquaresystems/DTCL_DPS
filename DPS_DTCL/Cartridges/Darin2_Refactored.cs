using DTCL.Common;
using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using VennaProtocol;

namespace DTCL.Cartridges
{
    /// <summary>
    /// Refactored version of Darin2 class with reduced code repetition
    /// </summary>
    public class Darin2_Refactored : ICart
    {
        #region Constants and Fields
        
        public UploadMessageInfoContainer uMessageContainerObj;
        public DownloadMessageInfoContainer dMessageContainerObj;
        public JsonParser<UploadMessageInfoContainer> uMessageParserObj;
        public JsonParser<DownloadMessageInfoContainer> dMessageParserObj;

        public event EventHandler<CommandEventArgs> CommandInProgress;

        // Flash memory constants
        public const int FORMAT_HEADER_BLOCK_NO = 0;
        public const int NO_OF_PAGES_IN_FORMAT_HEADER = 5;
        public const int HEADER_PAGE_NO = 2;
        public const int NO_OF_BYTES_IN_BLOCK = 512 * 32;
        public const int PAGE_SIZE = 512;

        // Block allocation constants
        private readonly Dictionary<string, uint> BLOCK_ALLOCATIONS = new Dictionary<string, uint>
        {
            { "NAV1", 200 }, { "NAV2", 100 }, { "NAV3", 100 },
            { "UPDATE", 1 }, { "MISSION1", 10 }, { "MISSION2", 10 },
            { "LRU", 50 }, { "USAGE", 1 }, { "SPJDL", 100 },
            { "RWRDL", 100 }, { "SPJUL", 10 }, { "RWRUL", 10 },
            { "FPLUL", 9 }, { "WP", 1 }, { "STR", 1 },
            { "HEADER", 1 }, { "THT", 1 }
        };

        public bool uploadSpjFlag = false;
        public bool uploadRwrFlag = false;
        public uint m_NoOfPages = 0;
        public uint m_LastPageSize = 0;

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Generic JSON initialization method to reduce repetition
        /// </summary>
        private T InitializeFromJson<T>(string jsonPath, JsonParser<T> parser, string errorMessage) where T : class, new()
        {
            // Check if the JSON file exists
            if (!File.Exists(jsonPath))
            {
                Log.Log.Error($"JSON file not found at path: {jsonPath}");
                MessageBox.Show($"{jsonPath} file is missing");
                return null;
            }

            // Check if the JSON file is empty
            if (new FileInfo(jsonPath).Length == 0)
            {
                Log.Log.Error($"JSON file is empty at path: {jsonPath}");
                MessageBox.Show($"{jsonPath} file length is zero");
                return null;
            }

            try
            {
                return parser.Deserialize(jsonPath);
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Failed to deserialize {jsonPath}: {ex.Message}");
                MessageBox.Show($"Failed to load {jsonPath}");
                return null;
            }
        }

        public bool InitializeUploadMessages(string msgPath)
        {
            uMessageContainerObj = new UploadMessageInfoContainer();
            uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

            string jsonFile = "D2\\D2UploadMessageDetails.json";
            uMessageContainerObj = InitializeFromJson(jsonFile, uMessageParserObj, "Upload messages");
            
            if (uMessageContainerObj == null) return false;

            // Reset all message properties
            ResetUploadMessageProperties();

            Log.Log.Info("Upload Messages Initialized");
            SaveUploadMessages();
            return true;
        }

        public bool InitializeDownloadMessages()
        {
            FileOperations.deleteAndCreateDir(DpsInfo.Instance.cartDownloadTempFilePath);

            dMessageContainerObj = new DownloadMessageInfoContainer();
            dMessageParserObj = new JsonParser<DownloadMessageInfoContainer>();

            string jsonFile = "D2\\D2DownloadMessageDetails.json";
            dMessageContainerObj = InitializeFromJson(jsonFile, dMessageParserObj, "Download messages");

            Log.Log.Info("Download Messages Initialized");
            return dMessageContainerObj != null;
        }

        private void ResetUploadMessageProperties()
        {
            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                msg.Nob = 0;
                msg.HeaderFileSize = 0;
                msg.ActualFileSize = 0;
                msg.ActualFileNOB = 0;
                msg.ActualFileLastPageSize = 0;
                msg.ActualFilePageSize = 0;
                msg.ActualFileNoOfPages = 0;
                msg.ActualFileNoOfPagesLastBlock = 0;
                msg.isDefinedInHeader = false;
                msg.isFileValid = false;
                msg.isFileExists = false;
                msg.NoOfBlocks = 0;
            }
        }

        #endregion

        #region Message Processing Methods

        /// <summary>
        /// Generic method to process messages and reduce repetitive loops
        /// </summary>
        private void ProcessMessages<T>(int startMsg, int endMsg, Func<int, T> findMessage, Action<int, T> processMessage) 
            where T : class
        {
            for (int msg = startMsg; msg <= endMsg; msg++)
            {
                // Skip specific message IDs if needed
                if ((typeof(T) == typeof(DownloadMessageInfo) && (msg == 5 || msg == 6)) ||
                    (typeof(T) == typeof(UploadMessageInfo) && msg == 6))
                    continue;

                T messageInfo = findMessage(msg);
                if (messageInfo != null)
                {
                    processMessage(msg, messageInfo);
                }
            }
        }

        public int InitDwnMsgWithHeaderSpaceDetails()
        {
            // Process download messages using generic method
            ProcessMessages<DownloadMessageInfo>(2, 17, 
                msgId => (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msgId),
                (msg, messageInfo) => ProcessDownloadMessageHeader(msg, messageInfo));

            SaveDownloadMessages();
            
            int totalSize = uMessageContainerObj.MessageInfoList.Sum(msg => msg.HeaderFileSize);
            DataHandlerVenna.Instance.SetProgressValues(totalSize + 15000, 0);

            return returnCodes.DTCL_SUCCESS;
        }

        private void ProcessDownloadMessageHeader(int msg, DownloadMessageInfo messageInfo)
        {
            int row_no = 10 + msg;
            int no_of_words = CartHeader.cartridgeHeader[row_no, 3];

            byte lsb = CartHeader.cartridgeHeader[row_no, 5];
            byte msb = CartHeader.cartridgeHeader[row_no, 4];
            int no_of_blocks = (msb << 8) | lsb;

            messageInfo.HeaderFileSize = no_of_words * 2 * no_of_blocks;
            messageInfo.Nob = no_of_blocks;

            // Calculate block and page information
            CalculateBlockAndPageInfo(messageInfo, messageInfo.HeaderFileSize);

            lsb = CartHeader.cartridgeHeader[row_no, 7];
            msb = CartHeader.cartridgeHeader[row_no, 6];
            messageInfo.fsb = (msb << 8) | lsb;
        }

        private void CalculateBlockAndPageInfo(dynamic messageInfo, int fileSize)
        {
            messageInfo.NoOfBlocks = fileSize / NO_OF_BYTES_IN_BLOCK;
            if ((fileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                messageInfo.NoOfBlocks++;

            messageInfo.ActualFileLastPageSize = PAGE_SIZE;
            if ((fileSize % PAGE_SIZE) != 0)
                messageInfo.ActualFileLastPageSize = fileSize % PAGE_SIZE;

            messageInfo.ActualFileNoOfPages = fileSize / PAGE_SIZE;
            if ((fileSize % PAGE_SIZE) != 0)
                messageInfo.ActualFileNoOfPages++;

            messageInfo.ActualFileNoOfPagesLastBlock = 32;
            if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);
        }

        #endregion

        #region FPL File Handling

        /// <summary>
        /// Process FPL files using a loop instead of repetitive code
        /// </summary>
        private void ProcessFPLFiles(byte[] pageData)
        {
            for (int fplNo = 1; fplNo <= 9; fplNo++)
            {
                string filename = $"fpl{fplNo}.bin";
                var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByFileName(filename);
                
                if (uMessageInfo != null)
                {
                    // Check validity bit
                    int bitIndex = 14 + (fplNo * 2);
                    uMessageInfo.isFileValid = (pageData[bitIndex] & 0x08) != 0;
                    
                    // Set NOB
                    int nobIndex = 13 + (fplNo * 2);
                    uMessageInfo.Nob = pageData[nobIndex];
                }
            }
        }

        #endregion

        #region Read/Write Operations

        /// <summary>
        /// Unified write operation for both normal write and copy
        /// </summary>
        public async Task<int> WriteUploadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, 
            byte cartNo, IProgress<int> progress)
        {
            return await WriteUploadFilesInternal(path, handleInvalidFile, cartNo, progress, true);
        }

        public async Task<int> WriteUploadFilesForCopy(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, 
            byte cartNo, IProgress<int> progress)
        {
            return await WriteUploadFilesInternal(path, handleInvalidFile, cartNo, progress, false);
        }

        private async Task<int> WriteUploadFilesInternal(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile,
            byte cartNo, IProgress<int> progress, bool performFullErase)
        {
            // Erase if needed
            if (performFullErase)
            {
                int ret = await EraseCartFiles(progress, cartNo);
                if (ret != returnCodes.DTCL_SUCCESS) return ret;
            }
            else
            {
                await EraseBlockNo(0, cartNo);
            }

            // Initialize
            DataHandlerVenna.Instance.ResetProgressValues();
            if (!InitializeMessages(path, performFullErase)) 
                return returnCodes.DTCL_FILE_NOT_FOUND;

            // Process header
            int result = await ProcessHeaderForWrite(path, cartNo, performFullErase);
            if (result != returnCodes.DTCL_SUCCESS) return result;

            // Write files
            result = await WriteFilesToCart(path, cartNo, progress, handleInvalidFile);
            if (result != returnCodes.DTCL_SUCCESS) return result;

            // Finalize
            await HandleFPLData(DpsInfo.Instance.D2UploadTempFilePath, cartNo, progress);
            await WriteHeaderSpaceDetails(cartNo);

            // Verify if full write
            if (performFullErase)
            {
                result = await VerifyWrittenFiles(path, handleInvalidFile, cartNo, progress);
            }

            return result;
        }

        /// <summary>
        /// Unified read operation to eliminate duplicate ReadDownloadFiles methods
        /// </summary>
        public async Task<int> ReadDownloadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile,
            byte cartNo, IProgress<int> progress, bool checkHeaderInfo = true)
        {
            DataHandlerVenna.Instance.ResetProgressValues();
            uploadRwrFlag = false;
            uploadSpjFlag = false;

            FileOperations.deleteAndCreateDir(path);

            // Read and process header
            int result = await ReadAndProcessHeader(cartNo, checkHeaderInfo, handleInvalidFile);
            if (result == returnCodes.DTCL_BLANK_CARTRIDGE || result == returnCodes.DTCL_CMD_ABORT)
                return result;

            // Find end markers
            await FindEnd(DpsInfo.Instance.D2DownloadTempFilePath, cartNo, progress);

            // Process FPL if header exists
            if (result == returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Debug("Reading and Creating FPL data, since header is present");
                await ReadCreateFPLData(cartNo, progress);
            }
            else
            {
                CreateEmptyFPL();
            }

            // Process SPJ/RWR files
            result = await ProcessSpecialFiles(cartNo, progress);
            if (result != returnCodes.DTCL_SUCCESS) return result;

            // Copy messages
            if (checkHeaderInfo)
            {
                if (!CopyValidUploadMessages(path, handleInvalidFile))
                    return returnCodes.DTCL_CMD_ABORT;
                CopyValidDownloadMessages(path);
            }
            else
            {
                CopyAllMessages(path);
            }

            return returnCodes.DTCL_SUCCESS;
        }

        #endregion

        #region Helper Methods

        private bool InitializeMessages(string path, bool fullInit)
        {
            bool res = InitializeUploadMessages(path);
            if (!res) return false;

            if (fullInit)
            {
                res = InitializeUploadMessagesFrom_DR(path);
                if (!res) return false;
            }

            return InitializeDownloadMessages();
        }

        private async Task<int> ProcessHeaderForWrite(string path, byte cartNo, bool checkHeader)
        {
            if (checkHeader)
            {
                allocate_space();
            }

            int ret = await ReadHeaderSpaceDetails(cartNo);
            if ((ret == returnCodes.DTCL_SUCCESS) || (ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                ret = InitUpdMsgWithHeaderSpaceDetails(ret, path);
            }

            return ret;
        }

        private async Task<int> WriteFilesToCart(string path, byte cartNo, IProgress<int> progress,
            Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            SplitFPLToIndividualFiles(path);

            int ret = CopyValidFilesToTempFolder(path, handleInvalidFile);
            if (ret != returnCodes.DTCL_SUCCESS) return ret;

            // Write each message file
            for (int msg_number = 3; msg_number <= 9; msg_number++)
            {
                if (msg_number == 6) continue;

                ret = await WriteMessageFile(msg_number, cartNo, progress);
                if (ret != returnCodes.DTCL_SUCCESS) return ret;
            }

            return returnCodes.DTCL_SUCCESS;
        }

        private async Task<int> WriteMessageFile(int msgNumber, byte cartNo, IProgress<int> progress)
        {
            var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msgNumber);

            if (!FileOperations.IsFileExist(DpsInfo.Instance.D2UploadTempFilePath + uMessageInfo.FileName))
            {
                Log.Log.Info($"{uMessageInfo.FileName} does not exist. Skipping.");
                return returnCodes.DTCL_SUCCESS;
            }

            if (uMessageInfo == null || uMessageInfo.NoOfBlocks <= 0)
            {
                Log.Log.Error($"No blocks found for MessageID-{msgNumber}. Skipping.");
                return returnCodes.DTCL_SUCCESS;
            }

            int present_block_no = uMessageInfo.fsb;
            Log.Log.Info($"Writing MessageID-{msgNumber} MessageName-{uMessageInfo.FileName}, " +
                        $"Block Address: {present_block_no}, Actual size: {uMessageInfo.ActualFileSize} " +
                        $"Total Blocks: {uMessageInfo.NoOfBlocks}");

            for (int blocks_written = 0; blocks_written < uMessageInfo.NoOfBlocks; blocks_written++)
            {
                bool isLastBlock = (blocks_written + 1 == uMessageInfo.NoOfBlocks);
                int no_of_pages = isLastBlock ? uMessageInfo.ActualFileNoOfPagesLastBlock : 32;
                int last_page_size = isLastBlock ? uMessageInfo.ActualFileLastPageSize : PAGE_SIZE;

                int ret = ReadBlockDataFromFile(DpsInfo.Instance.D2UploadTempFilePath, uMessageInfo, no_of_pages, last_page_size);
                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Error reading block data for MessageID-{msgNumber}, Block {blocks_written}");
                    return ret;
                }

                ret = await WriteD2BlockData(present_block_no + blocks_written, no_of_pages, last_page_size, cartNo, progress);
                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Error writing block data for MessageID-{msgNumber}, Block {blocks_written}");
                    return ret;
                }
            }

            return returnCodes.DTCL_SUCCESS;
        }

        private async Task<int> ReadAndProcessHeader(byte cartNo, bool checkHeaderInfo,
            Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            int ret = await ReadHeaderSpaceDetails(cartNo);
            if (ret == returnCodes.DTCL_BLANK_CARTRIDGE) return ret;

            ret = InitUpdMsgWithHeaderSpaceDetails(ret);
            if (ret == returnCodes.DTCL_SUCCESS)
            {
                ret = InitDwnMsgWithHeaderSpaceDetails();
            }

            ret = await ReadDecodeHeader(cartNo, null);
            if (ret != returnCodes.DTCL_SUCCESS && checkHeaderInfo)
            {
                var shouldContinue = handleInvalidFile("Header_Missing_Msg2", "");
                if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                {
                    Log.Log.Warning("User chose to stop operation due to missing header file");
                    return returnCodes.DTCL_CMD_ABORT;
                }
            }

            return ret;
        }

        private async Task<int> ProcessSpecialFiles(byte cartNo, IProgress<int> progress)
        {
            // Process SPJ file
            if (!checkDownloadSPJRWRFileSize(DpsInfo.Instance.cartDownloadTempFilePath, 16))
            {
                int ret = await ReadUploadSpjRwr(DpsInfo.Instance.cartDownloadTempFilePath, cartNo, progress, 8);
                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error("Reading of upload SPJ failed");
                    return ret;
                }
                uploadSpjFlag = true;
            }

            // Process RWR file
            if (!checkDownloadSPJRWRFileSize(DpsInfo.Instance.cartDownloadTempFilePath, 17))
            {
                int ret = await ReadUploadSpjRwr(DpsInfo.Instance.cartDownloadTempFilePath, cartNo, progress, 9);
                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error("Reading of upload RWR failed");
                    return ret;
                }
                uploadRwrFlag = true;
            }

            return returnCodes.DTCL_SUCCESS;
        }

        private async Task<int> VerifyWrittenFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile,
            byte cartNo, IProgress<int> progress)
        {
            int ret = await ReadDownloadFiles(DpsInfo.Instance.cartDownloadTempFilePath, handleInvalidFile, cartNo, progress, false);
            if (ret != returnCodes.DTCL_SUCCESS) return ret;

            Log.Log.Info("Start Comparing");
            return FileOperations.compareD2Dir_2(DpsInfo.Instance.cartUploadTempFilePath, 
                DpsInfo.Instance.cartDownloadTempFilePath, uMessageContainerObj);
        }

        private void CreateEmptyFPL()
        {
            Log.Log.Debug("Skip Reading and Creating FPL data, since header is absent, creating 0kb FPL");
            using (FileStream fs = new FileStream(DpsInfo.Instance.D2DownloadTempFilePath + "FPL.bin", 
                FileMode.OpenOrCreate, FileAccess.Write))
            {
                // Empty file created
            }
        }

        private void SaveUploadMessages()
        {
            string serializedJson = uMessageParserObj.Serialize(uMessageContainerObj);
            File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);
        }

        private void SaveDownloadMessages()
        {
            string serializedJson = dMessageParserObj.Serialize(dMessageContainerObj);
            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);
        }

        #endregion

        #region Remaining Interface Methods (Stubs for completeness)

        public bool InitializeUploadMessagesFrom_DR(string msgPath)
        {
            Log.Log.Info("Upload Messages Initialized From DR");
            bool res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, msgPath, uMessageContainerObj);
            if (!res) return res;

            int totalSize = uMessageContainerObj.MessageInfoList.Sum(msg => msg.ActualFileSize);
            DataHandlerVenna.Instance.totalDataSize = totalSize;
            DataHandlerVenna.Instance.totalDataProcessed = 0;

            SaveUploadMessages();
            return res;
        }

        public async Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            // Implementation would go here
            throw new NotImplementedException();
        }

        public async Task<int> EraseCartPCFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            // Implementation would go here
            throw new NotImplementedException();
        }

        public async Task<int> CopyCartFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile,
            Func<string, string> displayUserStatus, byte masterCartNo, byte slaveCartNo, IProgress<int> progress)
        {
            // Use the existing implementation from original file
            throw new NotImplementedException();
        }

        public async Task<int> CopyCartFilesToMultipleSlaves(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile,
            Func<string, string> displayUserStatus, byte masterCartNo, byte[] slaveCartNumbers, IProgress<int> progress)
        {
            // Use the implementation already created in original file
            throw new NotImplementedException();
        }

        public async Task<int> CompareCartFiles(Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation,
            Func<string, string> displayUserStatus, byte masterCartNo, byte slaveCartNo, IProgress<int> progress)
        {
            // Implementation would go here
            throw new NotImplementedException();
        }

        public async Task<int> Format(IProgress<int> progress, byte cartNo)
        {
            // Not applicable for D2
            return returnCodes.DTCL_CMD_NOT_SUPPORTED;
        }

        public async Task<PCResult> ExecutePC(bool withCart, CartType cartType, byte cartNo)
        {
            // Implementation would go here
            throw new NotImplementedException();
        }

        public async Task<VennaSubCmdResponse> ProcessSubCommand(VennaProtocolFrame request)
        {
            // Implementation would go here
            throw new NotImplementedException();
        }

        // Additional helper methods would need to be implemented...
        private async Task<int> ReadHeaderSpaceDetails(byte cartNo) => throw new NotImplementedException();
        private int InitUpdMsgWithHeaderSpaceDetails(int cartStatus, string path = "") => throw new NotImplementedException();
        private void allocate_space() => throw new NotImplementedException();
        private int CopyValidFilesToTempFolder(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile) => throw new NotImplementedException();
        private void SplitFPLToIndividualFiles(string path) => throw new NotImplementedException();
        private int ReadBlockDataFromFile(string path, UploadMessageInfo messageInfo, int noOfPages, int lastPageSize) => throw new NotImplementedException();
        private async Task<int> WriteD2BlockData(int blockNo, int noOfPages, int lastPageSize, byte cartNo, IProgress<int> progress) => throw new NotImplementedException();
        private async Task HandleFPLData(string path, byte cartNo, IProgress<int> progress) => throw new NotImplementedException();
        private async Task WriteHeaderSpaceDetails(byte cartNo) => throw new NotImplementedException();
        private async Task<int> EraseBlockNo(int blockNo, byte cartNo) => throw new NotImplementedException();
        private async Task<int> ReadDecodeHeader(byte cartNo, IProgress<int> progress) => throw new NotImplementedException();
        private async Task FindEnd(string path, byte cartNo, IProgress<int> progress) => throw new NotImplementedException();
        private async Task<int> ReadCreateFPLData(byte cartNo, IProgress<int> progress) => throw new NotImplementedException();
        private bool checkDownloadSPJRWRFileSize(string path, int msgId) => throw new NotImplementedException();
        private async Task<int> ReadUploadSpjRwr(string path, byte cartNo, IProgress<int> progress, int msgId) => throw new NotImplementedException();
        private bool CopyValidUploadMessages(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile) => throw new NotImplementedException();
        private void CopyValidDownloadMessages(string path) => throw new NotImplementedException();
        private void CopyAllMessages(string path) => throw new NotImplementedException();
        private void CombineFpl() => throw new NotImplementedException();

        #endregion
    }
}